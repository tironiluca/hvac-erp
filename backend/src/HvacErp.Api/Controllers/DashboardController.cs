using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>Home dashboard aggregates (<c>/api/dashboard</c>), readable by any signed-in user.</summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>
    /// Quote pipeline, open repairs by status, the week's installations from <paramref name="weekStart"/>
    /// (a local date-only value; defaults to the current week's Monday) and recent activity.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DateTime? weekStart, CancellationToken cancellationToken) =>
        Ok(await dashboard.GetAsync(weekStart, cancellationToken));
}
