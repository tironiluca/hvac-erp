using HvacErp.Domain.Entities;

namespace HvacErp.Application.Interfaces;

/// <summary>
/// Ports the legacy InsPrev.vb pricing engine: unit-compatibility validation plus the
/// machines/installation/VAT-blended pricing formula. Mutates and prices the quote's
/// MachinesPrice/InstallationPrice/FinalPrice — call after Products/DuctedUnits/ServiceItems
/// are attached to the quote (whether new or already tracked by the DbContext).
/// </summary>
public interface IQuotePricingService
{
    /// <summary>Validates unit compatibility and sets the quote's machines / installation / final / SF prices.</summary>
    Task PriceAsync(Quote quote, CancellationToken cancellationToken = default);
}
