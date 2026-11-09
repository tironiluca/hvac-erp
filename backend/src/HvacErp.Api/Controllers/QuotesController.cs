using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Erp;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Enums;

namespace HvacErp.Api.Controllers;

/// <summary>HVAC installation quotes (<c>/api/quotes</c>): CRUD, lifecycle transitions, PDF generation and photo attachments.</summary>
[ApiController]
[Authorize]
[Route("api/quotes")]
public class QuotesController(
    IQuoteService quotes,
    IQuotePdfService pdf,
    IQuoteAttachmentService attachments,
    IErpExportService erp) : ControllerBase
{
    /// <summary>Lists quotes newest first, optionally for one client.</summary>
    [HttpGet]
    public async Task<ActionResult<List<QuoteListItemDto>>> GetList([FromQuery] int? clientId, CancellationToken cancellationToken) =>
        Ok(await quotes.GetListAsync(clientId, cancellationToken));

    /// <summary>Gets the full quote: lines, ducted units, extras, status history and attachments.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteDetailDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await quotes.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a quote: assigns Number/Version/Revision and prices it server-side. 400 on incompatible units.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<QuoteDetailDto>> Create(UpsertQuoteRequest request, CancellationToken cancellationToken)
    {
        var created = await quotes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replaces the quote's content and re-prices it. 400 once the quote is confirmed.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<QuoteDetailDto>> Update(int id, UpsertQuoteRequest request, CancellationToken cancellationToken) =>
        Ok(await quotes.UpdateAsync(id, request, cancellationToken));

    /// <summary>Records that the site survey happened.</summary>
    [HttpPost("{id:int}/survey")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> MarkSurveyDone(int id, CancellationToken cancellationToken)
    {
        await quotes.MarkSurveyDoneAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Records a customer recall, with optional notes.</summary>
    [HttpPost("{id:int}/recall")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Recall(int id, RecallQuoteRequest request, CancellationToken cancellationToken)
    {
        await quotes.RecallAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Marks the quote confirmed (terminal; the quote becomes read-only). Sales/Admin only.</summary>
    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        await quotes.ConfirmAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// The quote's export to the main ERP (status, order number, last error); 204 when it has none
    /// (not confirmed yet, or confirmed before the integration was switched on).
    /// </summary>
    [HttpGet("{id:int}/erp-export")]
    public async Task<ActionResult<ErpExportDto>> GetErpExport(int id, CancellationToken cancellationToken) =>
        await erp.GetAsync(id, cancellationToken) is { } export ? Ok(export) : NoContent();

    /// <summary>
    /// Sends a confirmed quote to the main ERP now (creating the export if needed, retrying a failed
    /// one) and returns the outcome. 400 when no ERP is configured or the quote is not confirmed.
    /// </summary>
    [HttpPost("{id:int}/erp-export")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ErpExportDto>> SendToErp(int id, CancellationToken cancellationToken) =>
        Ok(await erp.SendNowAsync(id, cancellationToken));

    /// <summary>
    /// Flips the customer's acceptance of an incentive that needs it (e.g. an invoice discount);
    /// allowed even on a confirmed quote. 404 when the quote does not carry the scheme, 400 when the
    /// scheme needs no confirmation.
    /// </summary>
    [HttpPost("{id:int}/incentives/{schemeId:int}/confirmation")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> ToggleIncentiveConfirmation(int id, int schemeId, CancellationToken cancellationToken)
    {
        await quotes.ToggleIncentiveConfirmationAsync(id, schemeId, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a quote and its child rows.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await quotes.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Renders the customer quote PDF with the active template and stores it; returns its public URL.</summary>
    [HttpPost("{id:int}/pdf")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<QuotePdfResponse>> GeneratePdf(int id, CancellationToken cancellationToken)
    {
        var url = await pdf.GenerateAndStoreAsync(id, cancellationToken);
        return Ok(new QuotePdfResponse(url));
    }

    /// <summary>Uploads a photo / document to the quote (20 MB max).</summary>
    [HttpPost("{id:int}/attachments")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<QuoteAttachmentDto>> UploadAttachment(int id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var attachment = await attachments.UploadAsync(id, file.FileName, stream, file.ContentType, cancellationToken);
        return Ok(attachment);
    }

    /// <summary>Removes an attachment from the quote and from storage.</summary>
    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId, CancellationToken cancellationToken)
    {
        await attachments.DeleteAsync(id, attachmentId, cancellationToken);
        return NoContent();
    }
}
