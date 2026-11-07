using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Enums;

namespace HvacErp.Api.Controllers;

/// <summary>
/// Admin-only management of the PDF layout templates the QuestPDF generators render against, plus a
/// live preview endpoint the in-app editor calls on every change.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/pdf-templates")]
public class PdfTemplatesController(IPdfTemplateService templates) : ControllerBase
{
    /// <summary>Lists templates, optionally for one document type.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PdfTemplateListItemDto>>> GetList(
        [FromQuery] PdfDocumentType? documentType, CancellationToken cancellationToken) =>
        Ok(await templates.ListAsync(documentType, cancellationToken));

    /// <summary>The sections, columns and options the editor may use for a document type.</summary>
    [HttpGet("catalog")]
    public ActionResult<PdfTemplateCatalogDto> GetCatalog([FromQuery] PdfDocumentType documentType) =>
        Ok(templates.GetCatalog(documentType));

    /// <summary>Gets one template with its full definition.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PdfTemplateDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await templates.GetAsync(id, cancellationToken));

    /// <summary>Creates a template after validating it against the catalog.</summary>
    [HttpPost]
    public async Task<ActionResult<PdfTemplateDto>> Create(
        UpsertPdfTemplateRequest request, CancellationToken cancellationToken)
    {
        var created = await templates.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replaces a template's name and definition after validation.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PdfTemplateDto>> Update(
        int id, UpsertPdfTemplateRequest request, CancellationToken cancellationToken) =>
        Ok(await templates.UpdateAsync(id, request, cancellationToken));

    /// <summary>Copies a template under a new name.</summary>
    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<PdfTemplateDto>> Duplicate(
        int id, DuplicatePdfTemplateRequest request, CancellationToken cancellationToken)
    {
        var copy = await templates.DuplicateAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = copy.Id }, copy);
    }

    /// <summary>Makes a template the active one for its document type.</summary>
    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<PdfTemplateDto>> Activate(int id, CancellationToken cancellationToken) =>
        Ok(await templates.SetActiveAsync(id, cancellationToken));

    /// <summary>Resets a template's definition to the built-in defaults.</summary>
    [HttpPost("{id:int}/restore-defaults")]
    public async Task<ActionResult<PdfTemplateDto>> RestoreDefaults(int id, CancellationToken cancellationToken) =>
        Ok(await templates.RestoreDefaultsAsync(id, cancellationToken));

    /// <summary>Deletes a template that is neither active nor a system default.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await templates.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Renders a preview PDF from an unsaved definition against sample data.</summary>
    [HttpPost("preview")]
    [Produces("application/pdf")]
    public ActionResult Preview(PdfTemplatePreviewRequest request)
    {
        var pdf = templates.RenderPreview(request);
        return File(pdf, "application/pdf");
    }
}
