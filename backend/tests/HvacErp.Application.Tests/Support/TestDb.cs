using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Pricing;
using HvacErp.Application.Services;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Support;

/// <summary>
/// Spins up the real <see cref="AppDbContext"/> (same entity configurations as production) on the
/// EF Core in-memory provider, one isolated database per test. Relational-only behaviour (CHECK
/// constraints, FK RESTRICT, jsonb) is not exercised here — these tests target Application logic.
/// </summary>
public static class TestDb
{
    /// <summary>The tenant and store every test works in unless it asks for another.</summary>
    public static readonly ITenantContext DefaultTenant = FixedTenantContext.For(Tenant.DefaultId, Store.DefaultId);

    /// <summary>
    /// A context on a fresh database working for <paramref name="tenant"/> (default: tenant 1,
    /// store 1). Pass the same <paramref name="databaseName"/> to several calls to look at one
    /// database as different tenants.
    /// </summary>
    public static AppDbContext Create(ITenantContext? tenant = null, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"hvac-erp-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, tenant ?? DefaultTenant);
    }

    /// <summary>The three VAT rows exactly as seeded in production (legacy literals).</summary>
    public static void SeedVatOptions(AppDbContext db)
    {
        db.VatOptions.AddRange(
            new VatOption { Code = VatCodes.Standard22, Label = "IVA 22%", BlendShape = VatBlendShapes.Split, MachinesMultiplier = 1.22m, InstallationMultiplier = 2.2m },
            new VatOption { Code = VatCodes.Reduced10, Label = "IVA 10%", BlendShape = VatBlendShapes.Flat, MachinesMultiplier = 1.1m, InstallationMultiplier = 1m },
            new VatOption { Code = VatCodes.Exempt, Label = "Esente", BlendShape = VatBlendShapes.None, MachinesMultiplier = 1m, InstallationMultiplier = 1m });
    }

    /// <summary>Base installation prices keyed by internal-unit count (Mono…Penta).</summary>
    public static void SeedBaseInstallationItems(AppDbContext db)
    {
        db.ServiceItems.AddRange(
            new ServiceItem { Code = InstallationCostItems.BaseSystemPriceFor(1), Name = "Single split", Price = 400m },
            new ServiceItem { Code = InstallationCostItems.BaseSystemPriceFor(2), Name = "Dual split", Price = 600m },
            new ServiceItem { Code = InstallationCostItems.BaseSystemPriceFor(3), Name = "Trial split", Price = 800m },
            new ServiceItem { Code = InstallationCostItems.BaseSystemPriceFor(4), Name = "Quad split", Price = 1000m },
            new ServiceItem { Code = InstallationCostItems.BaseSystemPriceFor(5), Name = "Penta split", Price = 1200m });
    }

    public static ProductPriceList AddPriceList(AppDbContext db, params decimal[] tierPercentages)
    {
        var priceList = new ProductPriceList { Description = "Test list" };
        for (var i = 0; i < tierPercentages.Length; i++)
        {
            priceList.DiscountTiers.Add(new PriceListDiscountTier { TierOrder = i + 1, Percentage = tierPercentages[i] });
        }
        db.ProductPriceLists.Add(priceList);
        return priceList;
    }

    /// <summary>Adds the default incentive schemes (<see cref="IncentiveSchemeDefaults"/>) and returns them by code.</summary>
    public static Dictionary<string, IncentiveScheme> SeedIncentiveSchemes(AppDbContext db)
    {
        var schemes = IncentiveSchemeDefaults.All();
        db.IncentiveSchemes.AddRange(schemes);
        db.SaveChanges();
        return schemes.ToDictionary(s => s.Code);
    }

    public static Product AddProduct(
        AppDbContext db,
        ProductPriceList priceList,
        decimal price,
        string brand = "Daikin",
        string gas = "R32",
        decimal? maxBtu = null,
        decimal? power = null,
        decimal? pricePrepared = null,
        decimal? priceNotPrepared = null,
        decimal? hydronicPrice = null,
        bool isHydronic = false)
    {
        var product = new Product
        {
            PriceList = priceList,
            Brand = brand,
            Model = $"M-{Guid.NewGuid():N}"[..10],
            Price = price,
            PricePrepared = pricePrepared,
            PriceNotPrepared = priceNotPrepared,
            HydronicPrice = hydronicPrice,
            IsHydronic = isHydronic,
            RefrigerantGas = gas,
            MaxBtu = maxBtu,
            Power = power,
        };
        db.Products.Add(product);
        return product;
    }

    public static Client AddClient(AppDbContext db, string code = "CP0001", string name = "Mario Rossi")
    {
        var client = new Client { Code = code, FullName = name, City = "Milano" };
        db.Clients.Add(client);
        return client;
    }
}

/// <summary>A fixed caller identity for audit-stamping assertions.</summary>
public sealed class FakeCurrentUser(string? name = "tester@example.com", string? id = "user-1", UserRole? role = UserRole.Sales) : ICurrentUser
{
    public string? Id { get; } = id;
    public string? Name { get; } = name;
    public UserRole? Role { get; } = role;
}

/// <summary>Records every calendar fan-out call so tests can assert on them.</summary>
public sealed class RecordingCalendarNotifier : IInstallationCalendarNotifier
{
    public List<ScheduledInstallationDto> Changed { get; } = [];
    public List<int> Deleted { get; } = [];

    public Task InstallationChangedAsync(ScheduledInstallationDto installation, CancellationToken cancellationToken = default)
    {
        Changed.Add(installation);
        return Task.CompletedTask;
    }

    public Task InstallationDeletedAsync(int installationId, int storeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(installationId);
        return Task.CompletedTask;
    }
}

/// <summary>Storage that keeps everything in memory and records what was asked of it.</summary>
public sealed class FakeStorage : IFileStorageService
{
    /// <summary>Private documents by path.</summary>
    public Dictionary<string, StoredFile> Documents { get; } = [];

    /// <summary>Public assets by path.</summary>
    public Dictionary<string, StoredFile> PublicAssets { get; } = [];

    /// <summary>Paths <see cref="TryDeleteAsync"/> was called with, whether or not it succeeded.</summary>
    public List<string> DeleteRequests { get; } = [];

    /// <summary>Makes deletes fail, as when storage is unreachable.</summary>
    public bool DeleteSucceeds { get; set; } = true;

    public static string SignedUrl(string path, TimeSpan lifetime) =>
        $"https://storage.example/signed/{path}?expiresIn={(int)lifetime.TotalSeconds}";

    public async Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        Documents[path] = await ReadAsync(content, contentType, cancellationToken);
        return path;
    }

    public async Task<string> UploadPublicAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        PublicAssets[path] = await ReadAsync(content, contentType, cancellationToken);
        return $"https://storage.example/public/{path}";
    }

    public Task<string> CreateSignedUrlAsync(string path, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Documents.ContainsKey(path)
            ? Task.FromResult(SignedUrl(path, lifetime))
            : throw new NotFoundException("Stored file", path);

    public Task<bool> TryDeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        DeleteRequests.Add(path);
        return Task.FromResult(DeleteSucceeds && Documents.Remove(path));
    }

    public Task<StoredFile?> TryDownloadDocumentAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documents.GetValueOrDefault(path));

    public Task<byte[]?> TryDownloadAsync(string url, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(null);

    private static async Task<StoredFile> ReadAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        return new StoredFile(copy.ToArray(), contentType);
    }
}
