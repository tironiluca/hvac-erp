using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Erp;

/// <summary>Status of a quote's ERP export.</summary>
public sealed record ErpExportDto(
    int QuoteId,
    string Status,
    string Connector,
    int Attempts,
    string? ErpOrderNumber,
    string? LastError,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? NextAttemptAt);

/// <summary>Sends confirmed quotes to the main ERP through the outbox (<see cref="ErpOrderExport"/>).</summary>
public interface IErpExportService
{
    /// <summary>Whether a connector is configured.</summary>
    bool Enabled { get; }

    /// <summary>
    /// Adds the export row for a quote to the context (no save: the caller saves it with the
    /// confirmation, which is not in the database yet, hence <paramref name="confirmedAt"/>). No-op
    /// when integration is off or the quote already has one.
    /// </summary>
    Task EnqueueAsync(int quoteId, DateTime confirmedAt, CancellationToken cancellationToken = default);

    /// <summary>The quote's export, or null when it has none.</summary>
    Task<ErpExportDto?> GetAsync(int quoteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a confirmed quote now (creating its export if needed, e.g. for a quote confirmed before
    /// the integration existed) and returns the outcome. A failed export is retried from scratch.
    /// </summary>
    Task<ErpExportDto> SendNowAsync(int quoteId, CancellationToken cancellationToken = default);

    /// <summary>Sends up to <paramref name="batchSize"/> due exports; returns how many were attempted.</summary>
    Task<int> ProcessDueAsync(int batchSize = 20, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IErpExportService"/>.</summary>
public class ErpExportService(
    IAppDbContext context,
    IErpConnectorResolver connectors,
    ICurrentUser currentUser,
    TimeProvider clock) : IErpExportService
{
    /// <summary>Attempts before an export with temporary failures is marked failed.</summary>
    public const int MaxAttempts = 8;

    public bool Enabled => connectors.Current is not null;

    public async Task EnqueueAsync(int quoteId, DateTime confirmedAt, CancellationToken cancellationToken = default)
    {
        var connector = connectors.Current;
        if (connector is null || await context.ErpOrderExports.AnyAsync(e => e.QuoteId == quoteId, cancellationToken))
        {
            return;
        }

        context.ErpOrderExports.Add(await BuildExportAsync(quoteId, connector.Name, confirmedAt, cancellationToken));
    }

    public async Task<ErpExportDto?> GetAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var export = await context.ErpOrderExports.AsNoTracking()
            .FirstOrDefaultAsync(e => e.QuoteId == quoteId, cancellationToken);
        return export is null ? null : ToDto(export);
    }

    public async Task<ErpExportDto> SendNowAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        currentUser.EnsureInRole("send a quote to the ERP", UserRole.Sales, UserRole.Admin);
        var connector = connectors.Current
            ?? throw new BusinessRuleException("No ERP connection is configured.");

        var quote = await context.Quotes.AsNoTracking()
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.CurrentStatus })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), quoteId);
        if (quote.CurrentStatus != QuoteStatusCodes.Confirmed)
        {
            throw new BusinessRuleException("Only a confirmed quote can be sent to the ERP.");
        }

        var export = await context.ErpOrderExports.FirstOrDefaultAsync(e => e.QuoteId == quoteId, cancellationToken);
        if (export is null)
        {
            export = await BuildExportAsync(quoteId, connector.Name, confirmedAt: null, cancellationToken);
            context.ErpOrderExports.Add(export);
        }
        else if (export.Status == ErpExportStatuses.Sent)
        {
            return ToDto(export);
        }
        else if (export.Status == ErpExportStatuses.Failed)
        {
            export.Status = ErpExportStatuses.Pending;
            export.Attempts = 0;
        }

        await PushAsync(export, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(export);
    }

    public async Task<int> ProcessDueAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await context.ErpOrderExports
            .Where(e => e.Status == ErpExportStatuses.Pending && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.NextAttemptAt ?? e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var export in due)
        {
            await PushAsync(export, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    /// <summary>Back-off before attempt <paramref name="attempt"/> + 1: 1, 2, 4 … minutes, capped at an hour.</summary>
    public static TimeSpan BackOff(int attempt) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempt - 1))));

    private async Task PushAsync(ErpOrderExport export, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        export.Attempts++;

        var connector = connectors.ByName(export.Connector);
        ErpPushResult result;
        if (connector is null)
        {
            result = ErpPushResult.TryLater($"Connector '{export.Connector}' is not configured.");
        }
        else
        {
            try
            {
                result = await connector.PushOrderAsync(ErpOrder.FromJson(export.PayloadJson), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = ErpPushResult.TryLater(ex.Message);
            }
        }

        if (result.Success)
        {
            export.Status = ErpExportStatuses.Sent;
            export.ErpOrderNumber = result.ErpOrderNumber;
            export.LastError = null;
            export.SentAt = now;
            export.NextAttemptAt = null;
        }
        else if (result.Retryable && export.Attempts < MaxAttempts)
        {
            export.Status = ErpExportStatuses.Pending;
            export.LastError = result.Error;
            export.NextAttemptAt = now + BackOff(export.Attempts);
        }
        else
        {
            export.Status = ErpExportStatuses.Failed;
            export.LastError = result.Error;
            export.NextAttemptAt = null;
        }
    }

    private async Task<ErpOrderExport> BuildExportAsync(
        int quoteId, string connectorName, DateTime? confirmedAt, CancellationToken cancellationToken)
    {
        var query = context.Quotes.AsNoTracking();
        foreach (var path in ErpOrderMapper.IncludeForErp)
        {
            query = query.Include(path);
        }
        var quote = await query.FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), quoteId);
        var vatLabel = await context.VatOptions.Where(v => v.Code == quote.Vat).Select(v => v.Label).FirstOrDefaultAsync(cancellationToken);

        return new ErpOrderExport
        {
            QuoteId = quoteId,
            Connector = connectorName,
            Status = ErpExportStatuses.Pending,
            PayloadJson = ErpOrderMapper.FromQuote(quote, vatLabel, confirmedAt).ToJson(),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
    }

    private static ErpExportDto ToDto(ErpOrderExport e) => new(
        e.QuoteId, e.Status, e.Connector, e.Attempts, e.ErpOrderNumber, e.LastError, e.CreatedAt, e.SentAt, e.NextAttemptAt);
}
