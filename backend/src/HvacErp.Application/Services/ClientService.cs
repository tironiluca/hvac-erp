using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IClientService"/> over EF Core.</summary>
public class ClientService(IAppDbContext context) : IClientService
{
    public async Task<List<ClientDto>> SearchAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var query = context.Clients.Include(c => c.Source).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.Code.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.ToLower().Contains(term)) ||
                (c.Mobile != null && c.Mobile.ToLower().Contains(term)));
        }

        var clients = await query.OrderBy(c => c.FullName).Take(100).ToListAsync(cancellationToken);
        return clients.Select(ToDto).ToList();
    }

    public async Task<ClientDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await context.Clients.Include(c => c.Source)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);
        return ToDto(client);
    }

    public async Task<ClientDto> CreateAsync(UpsertClientRequest request, CancellationToken cancellationToken = default)
    {
        var client = new Client();
        Apply(client, request);
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(client.Id, cancellationToken);
    }

    public async Task<ClientDto> UpdateAsync(int id, UpsertClientRequest request, CancellationToken cancellationToken = default)
    {
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);
        Apply(client, request);
        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Client), id);

        var quotes = await context.Quotes.CountAsync(q => q.ClientId == id, cancellationToken);
        var repairs = await context.Repairs.CountAsync(r => r.ClientId == id, cancellationToken);
        if (quotes > 0 || repairs > 0)
        {
            throw new BusinessRuleException(
                $"Client '{client.Code}' still has {quotes} quote(s) and {repairs} repair(s) — delete those first.");
        }

        var referred = await context.Clients.Where(c => c.ReferredByClientId == id).ToListAsync(cancellationToken);
        foreach (var other in referred)
        {
            other.ReferredByClientId = null;
        }

        context.Clients.Remove(client);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(Client client, UpsertClientRequest request)
    {
        client.Code = request.Code;
        client.FullName = request.FullName;
        client.Address = request.Address;
        client.City = request.City;
        client.Phone = request.Phone;
        client.Mobile = request.Mobile;
        client.Email = request.Email;
        client.TaxCode = request.TaxCode;
        client.ShippingAddress = request.ShippingAddress;
        client.ShippingCity = request.ShippingCity;
        client.SourceId = request.SourceId;
        client.ReferredByClientId = request.ReferredByClientId;
    }

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Code, c.FullName, c.Address, c.City, c.Phone, c.Mobile, c.Email, c.TaxCode,
        c.ShippingAddress, c.ShippingCity, c.SourceId, c.Source?.Description, c.ReferredByClientId);
}
