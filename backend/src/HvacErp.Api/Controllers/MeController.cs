using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>
/// The signed-in caller's profile. The one business endpoint that answers someone with no tenant:
/// the frontend needs it to say "you have no access yet" and to offer the tenant / store switcher.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.SignedIn)]
[Route("api/me")]
public class MeController(IAccessService access) : ControllerBase
{
    /// <summary>The caller's e-mail, their role in the current tenant, the current tenant and store, and every tenant and store they may switch to.</summary>
    [HttpGet]
    public async Task<ActionResult<CurrentUserDto>> Get(CancellationToken cancellationToken) =>
        Ok(await access.GetCurrentUserAsync(cancellationToken));
}
