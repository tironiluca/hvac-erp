using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>F-Gas refrigerant-handling records attached to a quote or a repair (<c>/api/fgas-records</c>).</summary>
[ApiController]
[Authorize]
[Route("api/fgas-records")]
public class FGasRecordsController(IFGasRecordService fgasRecords) : ControllerBase
{
    /// <summary>Lists the F-Gas records of exactly one quote (<paramref name="quoteId"/>) or repair (<paramref name="repairId"/>).</summary>
    [HttpGet]
    public async Task<ActionResult<List<FGasRecordDto>>> GetList([FromQuery] int? quoteId, [FromQuery] int? repairId, CancellationToken cancellationToken)
    {
        if (quoteId is null == repairId is null)
        {
            return BadRequest("Specify exactly one of quoteId or repairId.");
        }

        var records = quoteId is { } q
            ? await fgasRecords.GetForQuoteAsync(q, cancellationToken)
            : await fgasRecords.GetForRepairAsync(repairId!.Value, cancellationToken);

        return Ok(records);
    }

    /// <summary>Appends an F-Gas record to a quote or a repair (exactly one parent).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<FGasRecordDto>> Create(UpsertFGasRecordRequest request, CancellationToken cancellationToken)
    {
        var created = await fgasRecords.CreateAsync(request, cancellationToken);
        return Ok(created);
    }

    /// <summary>Deletes an F-Gas record entered in error.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await fgasRecords.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
