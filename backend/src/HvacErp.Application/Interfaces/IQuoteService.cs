using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Quote use cases: CRUD plus the Draft → SurveyDone / Recalled → Confirmed lifecycle.</summary>
public interface IQuoteService
{
    /// <summary>Quotes newest first, optionally for one client.</summary>
    Task<List<QuoteListItemDto>> GetListAsync(int? clientId = null, CancellationToken cancellationToken = default);
    /// <summary>Gets the full quote.</summary>
    Task<QuoteDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Creates, numbers and prices a quote.</summary>
    Task<QuoteDetailDto> CreateAsync(UpsertQuoteRequest request, CancellationToken cancellationToken = default);
    /// <summary>Replaces the quote's content and re-prices it; refused once confirmed.</summary>
    Task<QuoteDetailDto> UpdateAsync(int id, UpsertQuoteRequest request, CancellationToken cancellationToken = default);
    /// <summary>Appends a SurveyDone event.</summary>
    Task MarkSurveyDoneAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Appends a Recalled event with optional notes.</summary>
    Task RecallAsync(int id, RecallQuoteRequest request, CancellationToken cancellationToken = default);
    /// <summary>Marks the quote confirmed (terminal, locks editing). Sales/Admin only.</summary>
    Task ConfirmAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Flips the SF acceptance flag (allowed on confirmed quotes). Sales/Admin only.</summary>
    Task ToggleSfConfirmationAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Deletes a quote and its children.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
