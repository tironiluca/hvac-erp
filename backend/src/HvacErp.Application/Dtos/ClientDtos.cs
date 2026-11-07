namespace HvacErp.Application.Dtos;

/// <summary>A client as returned by the API.</summary>
public record ClientDto(
    int Id,
    string Code,
    string FullName,
    string? Address,
    string? City,
    string? Phone,
    string? Mobile,
    string? Email,
    string? TaxCode,
    string? ShippingAddress,
    string? ShippingCity,
    int? SourceId,
    string? SourceDescription,
    int? ReferredByClientId);

/// <summary>Create / update payload for a client.</summary>
public record UpsertClientRequest(
    string Code,
    string FullName,
    string? Address,
    string? City,
    string? Phone,
    string? Mobile,
    string? Email,
    string? TaxCode,
    string? ShippingAddress,
    string? ShippingCity,
    int? SourceId,
    int? ReferredByClientId);
