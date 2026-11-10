using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>
/// A tenant's own administration (<c>/api/admin</c>): its stores and its members. Admin of the
/// current tenant only; everything acts on that tenant and no other.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/admin")]
public class TenantAdminController(ITenantAdministrationService administration, IConfiguration configuration) : ControllerBase
{
    /// <summary>Every store of the tenant, inactive ones included.</summary>
    [HttpGet("stores")]
    public async Task<ActionResult<List<StoreDto>>> GetStores(CancellationToken cancellationToken) =>
        Ok(await administration.GetStoresAsync(cancellationToken));

    /// <summary>Adds a store. 409 when the name is taken.</summary>
    [HttpPost("stores")]
    public async Task<ActionResult<StoreDto>> CreateStore(UpsertStoreRequest request, CancellationToken cancellationToken) =>
        Ok(await administration.CreateStoreAsync(request, cancellationToken));

    /// <summary>Edits a store; deactivating it hides it from the switcher and keeps its records. The last active store cannot be deactivated.</summary>
    [HttpPut("stores/{id:int}")]
    public async Task<ActionResult<StoreDto>> UpdateStore(int id, UpsertStoreRequest request, CancellationToken cancellationToken) =>
        Ok(await administration.UpdateStoreAsync(id, request, cancellationToken));

    /// <summary>The tenant's members with their role and stores.</summary>
    [HttpGet("members")]
    public async Task<ActionResult<List<MemberDto>>> GetMembers(CancellationToken cancellationToken) =>
        Ok(await administration.GetMembersAsync(cancellationToken));

    /// <summary>
    /// Adds a member by e-mail. Someone with no account is invited by e-mail and chooses a password
    /// from the link; 409 when they are already a member.
    /// </summary>
    [HttpPost("members")]
    public async Task<ActionResult<MemberDto>> AddMember(AddMemberRequest request, CancellationToken cancellationToken) =>
        Ok(await administration.AddMemberAsync(request, InviteLanding.For(Request, configuration), cancellationToken));

    /// <summary>Changes a member's role and stores. 400 when it would leave the tenant without an Admin.</summary>
    [HttpPut("members/{id:int}")]
    public async Task<ActionResult<MemberDto>> UpdateMember(int id, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        Ok(await administration.UpdateMemberAsync(id, request, cancellationToken));

    /// <summary>Removes a member from the tenant. 400 when it would leave the tenant without an Admin.</summary>
    [HttpDelete("members/{id:int}")]
    public async Task<IActionResult> RemoveMember(int id, CancellationToken cancellationToken)
    {
        await administration.RemoveMemberAsync(id, cancellationToken);
        return NoContent();
    }
}
