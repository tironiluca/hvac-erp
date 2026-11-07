using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
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
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hvac-erp-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
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

    public Task InstallationDeletedAsync(int installationId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(installationId);
        return Task.CompletedTask;
    }
}
