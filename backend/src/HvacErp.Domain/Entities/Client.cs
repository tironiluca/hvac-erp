namespace HvacErp.Domain.Entities;

/// <summary>A customer (legacy <c>Anagrafica</c>), optionally referred by another client.</summary>
public class Client
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? TaxCode { get; set; }
    public string? ShippingAddress { get; set; }
    public string? ShippingCity { get; set; }

    public int? SourceId { get; set; }
    public ClientSource? Source { get; set; }

    /// <summary>"Cliente amico" — the client who referred this one, if any.</summary>
    public int? ReferredByClientId { get; set; }
    public Client? ReferredByClient { get; set; }

    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Repair> Repairs { get; set; } = new List<Repair>();
}
