using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>
/// The tenants of the installation (<c>/api/platform/tenants</c>), for platform administrators —
/// whether or not they are a member of any tenant themselves.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.PlatformAdmin)]
[Route("api/platform/tenants")]
public class PlatformTenantsController(IPlatformTenantService tenants, IConfiguration configuration) : ControllerBase
{
    /// <summary>Every tenant, suspended ones included, with how many stores and members each has.</summary>
    [HttpGet]
    public async Task<ActionResult<List<TenantDto>>> GetList(CancellationToken cancellationToken) =>
        Ok(await tenants.GetTenantsAsync(cancellationToken));

    /// <summary>
    /// Creates a company with its first store, the defaults its quotes need and its first Admin, who
    /// is invited by e-mail when they have no account yet. 409 when the name is taken.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create(CreateTenantRequest request, CancellationToken cancellationToken) =>
        Ok(await tenants.CreateTenantAsync(request, InviteLanding.For(Request, configuration), cancellationToken));

    /// <summary>Edits a tenant's name and company data, or suspends / resumes it. A suspended tenant's members have no access; its data is kept.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TenantDto>> Update(int id, UpdateTenantRequest request, CancellationToken cancellationToken) =>
        Ok(await tenants.UpdateTenantAsync(id, request, cancellationToken));
}
