using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>Scheduled installations for confirmed quotes (<c>/api/installations</c>), backing the shared real-time calendar.</summary>
[ApiController]
[Authorize]
[Route("api/installations")]
public class InstallationsController(IInstallationService installations, IInstallationPdfService pdf) : ControllerBase
{
    /// <summary>Lists scheduled installations, optionally within an inclusive <paramref name="from"/>–<paramref name="to"/> date window.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ScheduledInstallationDto>>> GetList(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken) =>
        Ok(await installations.GetListAsync(from, to, cancellationToken));

    /// <summary>Gets one scheduled installation.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ScheduledInstallationDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await installations.GetByIdAsync(id, cancellationToken));

    /// <summary>Schedules the installation of a confirmed quote (one per quote, not in the past) and broadcasts it to calendar viewers.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ScheduledInstallationDto>> Create(UpsertScheduledInstallationRequest request, CancellationToken cancellationToken)
    {
        var created = await installations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Edits date, time band, crew, notes and floor-plan sketch of a scheduled installation.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ScheduledInstallationDto>> Update(int id, UpsertScheduledInstallationRequest request, CancellationToken cancellationToken) =>
        Ok(await installations.UpdateAsync(id, request, cancellationToken));

    /// <summary>Drag-and-drop reschedule from the shared calendar (date / slot / crew). 409 if another user moved it first.</summary>
    [HttpPatch("{id:int}/schedule")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ScheduledInstallationDto>> Reschedule(int id, RescheduleInstallationRequest request, CancellationToken cancellationToken) =>
        Ok(await installations.RescheduleAsync(id, request, cancellationToken));

    /// <summary>Cancels a scheduled installation and broadcasts the removal.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await installations.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>A link to the installation's generated work order that works for a few minutes. 404 when none has been generated.</summary>
    [HttpGet("{id:int}/pdf-url")]
    public async Task<ActionResult<DocumentUrlDto>> GetPdfUrl(int id, CancellationToken cancellationToken) =>
        Ok(new DocumentUrlDto(await pdf.GetUrlAsync(id, cancellationToken)));

    /// <summary>Renders the field work order (with barcode) and stores it; returns a link to it that works for a few minutes.</summary>
    [HttpPost("{id:int}/pdf")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<InstallationPdfResponse>> GeneratePdf(int id, CancellationToken cancellationToken)
    {
        var url = await pdf.GenerateAndStoreAsync(id, cancellationToken);
        return Ok(new InstallationPdfResponse(url));
    }
}
