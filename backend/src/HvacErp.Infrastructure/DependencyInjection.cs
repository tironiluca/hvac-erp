using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HvacErp.Application.Common;
using HvacErp.Application.Erp;
using HvacErp.Application.Interfaces;
using HvacErp.Infrastructure.Auth;
using HvacErp.Infrastructure.Erp;
using HvacErp.Infrastructure.Health;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Services.Pdf;
using HvacErp.Infrastructure.Services.Storage;
using HvacErp.Infrastructure.Tenancy;

namespace HvacErp.Infrastructure;

/// <summary>Composition-root helpers for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers EF Core (Npgsql, snake_case), Supabase Storage, the QuestPDF generators and the Supabase claims transformation.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Postgres' configuration value.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<TenantAccessResolver>();
        services.AddScoped<IPlatformTenantService, PlatformTenantService>();
        services.AddTransient<IClaimsTransformation, SupabaseUserClaimsTransformation>();

        services.AddHttpClient<IFileStorageService, SupabaseStorageService>(ConfigureSupabaseClient);
        services.AddScoped<BrandingAssetMover>();
        services.AddHttpClient<IIdentityInviter, SupabaseIdentityInviter>(ConfigureSupabaseClient);
        services.AddHttpClient<SupabaseStorageHealthCheck>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigureHttpClient(ConfigureSupabaseClient);

        // Readiness: the database and Storage are both required to serve the app. Exposed at /health.
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: [ReadinessTag])
            .AddCheck<SupabaseStorageHealthCheck>("storage", tags: [ReadinessTag]);

        services.AddSingleton<IQuoteDocumentGenerator, QuestPdfQuoteDocumentGenerator>();
        services.AddSingleton<IRepairDocumentGenerator, QuestPdfRepairDocumentGenerator>();
        services.AddSingleton<IInstallationDocumentGenerator, QuestPdfInstallationDocumentGenerator>();

        // Main-ERP integration: connectors are picked by Erp:Provider; the worker drains the outbox.
        services.Configure<ErpOptions>(configuration.GetSection(ErpOptions.Section));
        services.Configure<MockErpOptions>(configuration.GetSection(MockErpOptions.Section));
        services.AddHttpClient<HttpJsonErpConnector>();
        services.AddTransient<IErpConnector>(sp => sp.GetRequiredService<HttpJsonErpConnector>());
        services.AddScoped<IErpConnectorResolver, ErpConnectorResolver>();
        services.AddHostedService<ErpExportWorker>();
        services.AddScoped<MockErpService>();

        return services;
    }

    /// <summary>Health-check tag for dependencies that must be up before the API can serve requests.</summary>
    public const string ReadinessTag = "ready";

    private static void ConfigureSupabaseClient(IServiceProvider sp, HttpClient client)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var supabaseUrl = config["Supabase:Url"]
            ?? throw new InvalidOperationException("Missing 'Supabase:Url' configuration value.");
        var serviceRoleKey = config["Supabase:ServiceRoleKey"]
            ?? throw new InvalidOperationException("Missing 'Supabase:ServiceRoleKey' configuration value.");

        client.BaseAddress = new Uri(supabaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        client.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
    }
}
