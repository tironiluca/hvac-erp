using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Application.Dtos;
using HvacErp.Domain.Enums;

namespace HvacErp.Api.Controllers;

/// <summary>
/// The signed-in caller's app profile, built from the claims left by
/// <c>SupabaseUserClaimsTransformation</c>. The frontend uses <c>role</c> to gate admin-only UI.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    /// <summary>The caller's email and app role (<c>Admin</c> / <c>Sales</c> / <c>Warehouse</c>), used to gate UI.</summary>
    [HttpGet]
    public ActionResult<CurrentUserDto> Get()
    {
        var email = User.FindFirstValue("email") ?? string.Empty;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? nameof(UserRole.Sales);
        return Ok(new CurrentUserDto(email, role));
    }
}
