using HvacErp.Domain.Entities;

namespace HvacErp.Application.Interfaces;

/// <summary>
/// Assigns Number/Version/Revision to a new quote. Replaces the legacy app's single
/// 8-character "NNNNNVRR" id with three real columns: same client + same external unit
/// bumps the revision; same client + a different external unit bumps the version; a new
/// client allocates the next unused number. Call with the quote's Products already
/// attached (in-memory is fine — the quote need not be saved yet).
/// </summary>
public interface IQuoteNumberingService
{
    /// <summary>Sets <see cref="Domain.Entities.Quote.Number"/>, <c>Version</c>, <c>Revision</c> and, for follow-ups, <c>ParentQuoteId</c>.</summary>
    Task AssignNumberingAsync(Quote quote, CancellationToken cancellationToken = default);
}
