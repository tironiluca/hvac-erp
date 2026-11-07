using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Api.Authorization;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>Customer registry (<c>/api/clients</c>).</summary>
[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController(IClientService clients) : ControllerBase
{
    /// <summary>Searches clients by name, code, phone or mobile (case-insensitive, max 100 rows). No term lists the first 100 alphabetically.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ClientDto>>> Search([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await clients.SearchAsync(search, cancellationToken));

    /// <summary>Gets one client by id. 404 when missing.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await clients.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a client. 409 when the client code is already taken.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ClientDto>> Create(UpsertClientRequest request, CancellationToken cancellationToken)
    {
        var created = await clients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a client's registry data.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.SalesOrAdmin)]
    public async Task<ActionResult<ClientDto>> Update(int id, UpsertClientRequest request, CancellationToken cancellationToken) =>
        Ok(await clients.UpdateAsync(id, request, cancellationToken));

    /// <summary>Deletes a client with no quotes or repairs (400 otherwise). Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await clients.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
