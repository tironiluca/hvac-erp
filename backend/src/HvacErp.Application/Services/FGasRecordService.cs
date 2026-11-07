using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IFGasRecordService"/> over EF Core.</summary>
public class FGasRecordService(IAppDbContext context) : IFGasRecordService
{
    public async Task<List<FGasRecordDto>> GetForQuoteAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var records = await context.FGasRecords
            .Where(f => f.QuoteId == quoteId)
            .OrderByDescending(f => f.RecordedAt)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<List<FGasRecordDto>> GetForRepairAsync(int repairId, CancellationToken cancellationToken = default)
    {
        var records = await context.FGasRecords
            .Where(f => f.RepairId == repairId)
            .OrderByDescending(f => f.RecordedAt)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<FGasRecordDto> CreateAsync(UpsertFGasRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.QuoteId is null == request.RepairId is null)
        {
            throw new BusinessRuleException("An F-Gas record must be linked to exactly one of a quote or a repair.");
        }

        if (request.QuoteId is { } quoteId)
        {
            _ = await context.Quotes.FindAsync([quoteId], cancellationToken)
                ?? throw new NotFoundException(nameof(Quote), quoteId);
        }

        if (request.RepairId is { } repairId)
        {
            _ = await context.Repairs.FindAsync([repairId], cancellationToken)
                ?? throw new NotFoundException(nameof(Repair), repairId);
        }

        var record = new FGasRecord
        {
            QuoteId = request.QuoteId,
            RepairId = request.RepairId,
            GasType = request.GasType,
            KilogramsOfGas = request.KilogramsOfGas,
            IsRefill = request.IsRefill,
            HasExtraCharge = request.HasExtraCharge,
        };

        context.FGasRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(record);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await context.FGasRecords.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(FGasRecord), id);

        context.FGasRecords.Remove(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static FGasRecordDto ToDto(FGasRecord f) =>
        new(f.Id, f.QuoteId, f.RepairId, f.GasType, f.KilogramsOfGas, f.IsRefill, f.HasExtraCharge, f.RecordedAt);
}
