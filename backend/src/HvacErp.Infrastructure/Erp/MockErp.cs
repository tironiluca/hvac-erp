using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HvacErp.Application.Erp;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Infrastructure.Erp;

/// <summary>An order received by the mock ERP (its own storage, not part of the business model).</summary>
public class MockErpOrder
{
    public int Id { get; set; }

    /// <summary>Number assigned by the mock, <c>ERP-yyyy-nnnnnn</c>.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>The sender's reference (<see cref="ErpOrder.ExternalReference"/>); unique, for idempotency.</summary>
    public string ExternalReference { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public decimal FinalPrice { get; set; }
    public int LineCount { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The document as received.</summary>
    public string PayloadJson { get; set; } = "{}";
}

/// <summary>Outcome of a push to the mock ERP.</summary>
public sealed record MockErpReceipt(string OrderNumber, bool Duplicate);

/// <summary>
/// The mock ERP: stores pushed orders like a real ERP would, assigns order numbers, deduplicates by
/// external reference and can fail on purpose (<see cref="MockErpOptions.FailureRate"/>).
/// </summary>
public sealed class MockErpService(AppDbContext db, IOptionsMonitor<MockErpOptions> options, TimeProvider clock)
{
    /// <summary>Whether the mock is enabled and has an API key.</summary>
    public bool Enabled => options.CurrentValue is { Enabled: true, ApiKey.Length: > 0 };

    /// <summary>Constant-time check of a caller's key.</summary>
    public bool IsAuthorized(string? key)
    {
        var expected = options.CurrentValue.ApiKey;
        return !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(expected)
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(key), System.Text.Encoding.UTF8.GetBytes(expected));
    }

    /// <summary>True when this push should be failed on purpose.</summary>
    public bool ShouldFail() => Random.Shared.NextDouble() < options.CurrentValue.FailureRate;

    /// <summary>Stores an order (or returns the existing one for a repeated external reference).</summary>
    public async Task<MockErpReceipt> ReceiveAsync(ErpOrder order, string payloadJson, CancellationToken cancellationToken)
    {
        var existing = await db.MockErpOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.ExternalReference == order.ExternalReference, cancellationToken);
        if (existing is not null)
        {
            return new MockErpReceipt(existing.OrderNumber, Duplicate: true);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var row = new MockErpOrder
        {
            ExternalReference = order.ExternalReference,
            CustomerName = order.Customer.Name,
            FinalPrice = order.Totals.FinalPrice,
            LineCount = order.Lines.Count,
            ReceivedAt = now,
            PayloadJson = payloadJson,
        };
        db.MockErpOrders.Add(row);
        await db.SaveChangesAsync(cancellationToken);

        row.OrderNumber = $"ERP-{now:yyyy}-{row.Id:000000}";
        await db.SaveChangesAsync(cancellationToken);
        return new MockErpReceipt(row.OrderNumber, Duplicate: false);
    }

    /// <summary>The most recent orders received.</summary>
    public Task<List<MockErpOrder>> ListAsync(int take, CancellationToken cancellationToken) =>
        db.MockErpOrders.AsNoTracking().OrderByDescending(o => o.ReceivedAt).Take(take).ToListAsync(cancellationToken);

    /// <summary>Indented JSON for display.</summary>
    public static string Pretty(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
