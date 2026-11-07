using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>Repairs and periodic revisions (<c>/api/repairs</c>): CRUD, status changes, appointments and the work-order PDF.</summary>
[ApiController]
[Authorize]
[Route("api/repairs")]
public class RepairsController(IRepairService repairs, IRepairPdfService pdf) : ControllerBase
{
    /// <summary>Lists repairs newest first, optionally for one client.</summary>
    [HttpGet]
    public async Task<ActionResult<List<RepairListItemDto>>> GetList([FromQuery] int? clientId, CancellationToken cancellationToken) =>
        Ok(await repairs.GetListAsync(clientId, cancellationToken));

    /// <summary>Gets the full repair with service lines, appointments and status history.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RepairDetailDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await repairs.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a repair or revision (a revision is never urgent and always predisposed).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<RepairDetailDto>> Create(UpsertRepairRequest request, CancellationToken cancellationToken)
    {
        var created = await repairs.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replaces the repair's content and service lines.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<RepairDetailDto>> Update(int id, UpsertRepairRequest request, CancellationToken cancellationToken) =>
        Ok(await repairs.UpdateAsync(id, request, cancellationToken));

    /// <summary>Moves the repair to another status and appends it to the history.</summary>
    [HttpPost("{id:int}/status")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeRepairStatusRequest request, CancellationToken cancellationToken)
    {
        await repairs.ChangeStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Books an appointment for the repair, optionally assigned to an installer.</summary>
    [HttpPost("{id:int}/schedule")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<RepairScheduleEntryDto>> AddScheduleEntry(int id, AddRepairScheduleEntryRequest request, CancellationToken cancellationToken) =>
        Ok(await repairs.AddScheduleEntryAsync(id, request, cancellationToken));

    /// <summary>Deletes a repair and its child rows (lines, appointments, history, F-Gas records).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await repairs.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Renders the work-order PDF and stores it; returns its public URL.</summary>
    [HttpPost("{id:int}/pdf")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<RepairPdfResponse>> GeneratePdf(int id, CancellationToken cancellationToken)
    {
        var url = await pdf.GenerateAndStoreAsync(id, cancellationToken);
        return Ok(new RepairPdfResponse(url));
    }
}
