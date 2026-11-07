using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>F-Gas record use cases (append-mostly log on quotes and repairs).</summary>
public interface IFGasRecordService
{
    /// <summary>Records attached to a quote.</summary>
    Task<List<FGasRecordDto>> GetForQuoteAsync(int quoteId, CancellationToken cancellationToken = default);
    /// <summary>Records attached to a repair.</summary>
    Task<List<FGasRecordDto>> GetForRepairAsync(int repairId, CancellationToken cancellationToken = default);
    /// <summary>Appends a record to exactly one quote or repair.</summary>
    Task<FGasRecordDto> CreateAsync(UpsertFGasRecordRequest request, CancellationToken cancellationToken = default);
    /// <summary>Deletes a record entered in error.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
