using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HvacErp.Application.Interfaces;
using HvacErp.Application.Services;

namespace HvacErp.Application;

/// <summary>Composition-root helpers for the Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers every Application service (pricing, numbering, workflow, PDF orchestration, lookups, settings).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IQuoteNumberingService, QuoteNumberingService>();
        services.AddScoped<IQuotePricingService, QuotePricingService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IQuotePdfService, QuotePdfService>();
        services.AddScoped<IQuoteAttachmentService, QuoteAttachmentService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IRepairPdfService, RepairPdfService>();
        services.AddScoped<IInstallationService, InstallationService>();
        services.AddScoped<IInstallationPdfService, InstallationPdfService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IFGasRecordService, FGasRecordService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<IPdfTemplateService, PdfTemplateService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
