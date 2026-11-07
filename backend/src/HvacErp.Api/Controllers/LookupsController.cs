using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Api.Controllers;

/// <summary>Read-only reference data for the SPA's dropdowns and price preview (<c>/api/lookups/*</c>).</summary>
[ApiController]
[Authorize]
[Route("api/lookups")]
public class LookupsController(ILookupService lookups) : ControllerBase
{
    /// <summary>Installers and their crews.</summary>
    [HttpGet("installers")]
    public async Task<ActionResult<List<InstallerDto>>> GetInstallers(CancellationToken cancellationToken) =>
        Ok(await lookups.GetInstallersAsync(cancellationToken));

    /// <summary>Sales operators / agents.</summary>
    [HttpGet("operators")]
    public async Task<ActionResult<List<OperatorDto>>> GetOperators(CancellationToken cancellationToken) =>
        Ok(await lookups.GetOperatorsAsync(cancellationToken));

    /// <summary>How a client found the business (marketing source).</summary>
    [HttpGet("client-sources")]
    public async Task<ActionResult<List<ClientSourceDto>>> GetClientSources(CancellationToken cancellationToken) =>
        Ok(await lookups.GetClientSourcesAsync(cancellationToken));

    /// <summary>Product type classification.</summary>
    [HttpGet("product-types")]
    public async Task<ActionResult<List<ProductTypeDto>>> GetProductTypes(CancellationToken cancellationToken) =>
        Ok(await lookups.GetProductTypesAsync(cancellationToken));

    /// <summary>Labour / consumables price list.</summary>
    [HttpGet("service-items")]
    public async Task<ActionResult<List<ServiceItemDto>>> GetServiceItems(CancellationToken cancellationToken) =>
        Ok(await lookups.GetServiceItemsAsync(cancellationToken));

    /// <summary>Extra-work line types a quote can include, with default prices.</summary>
    [HttpGet("extra-types")]
    public async Task<ActionResult<List<ExtraTypeDto>>> GetExtraTypes(CancellationToken cancellationToken) =>
        Ok(await lookups.GetExtraTypesAsync(cancellationToken));

    /// <summary>Product price lists (markup, VAT, fees).</summary>
    [HttpGet("product-price-lists")]
    public async Task<ActionResult<List<ProductPriceListDto>>> GetProductPriceLists(CancellationToken cancellationToken) =>
        Ok(await lookups.GetProductPriceListsAsync(cancellationToken));

    /// <summary>VAT schemes with the blend shape and multipliers the pricing engine applies.</summary>
    [HttpGet("vat-options")]
    public async Task<ActionResult<List<VatOptionDto>>> GetVatOptions(CancellationToken cancellationToken) =>
        Ok(await lookups.GetVatOptionsAsync(cancellationToken));

    /// <summary>The 10 repair states in legacy ordinal order, with workflow flags.</summary>
    [HttpGet("repair-statuses")]
    public async Task<ActionResult<List<RepairStatusDto>>> GetRepairStatuses(CancellationToken cancellationToken) =>
        Ok(await lookups.GetRepairStatusesAsync(cancellationToken));

    /// <summary>Quote lifecycle milestones.</summary>
    [HttpGet("quote-statuses")]
    public async Task<ActionResult<List<LookupItemDto>>> GetQuoteStatuses(CancellationToken cancellationToken) =>
        Ok(await lookups.GetQuoteStatusesAsync(cancellationToken));

    /// <summary>Ducted-system product lines (Standard / Airzone).</summary>
    [HttpGet("duct-system-types")]
    public async Task<ActionResult<List<LookupItemDto>>> GetDuctSystemTypes(CancellationToken cancellationToken) =>
        Ok(await lookups.GetDuctSystemTypesAsync(cancellationToken));

    /// <summary>Quote origin desks (Store / Field).</summary>
    [HttpGet("quote-departments")]
    public async Task<ActionResult<List<LookupItemDto>>> GetQuoteDepartments(CancellationToken cancellationToken) =>
        Ok(await lookups.GetQuoteDepartmentsAsync(cancellationToken));

    /// <summary>Installation time bands (Morning / Afternoon / FullDay).</summary>
    [HttpGet("day-slots")]
    public async Task<ActionResult<List<LookupItemDto>>> GetDaySlots(CancellationToken cancellationToken) =>
        Ok(await lookups.GetDaySlotsAsync(cancellationToken));

    /// <summary>Active HVAC units, optionally filtered by <paramref name="category"/> (e.g. <c>Esterna</c> / <c>Interna</c>), with their discount ladders.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<List<ProductDto>>> GetProducts([FromQuery] string? category, CancellationToken cancellationToken) =>
        Ok(await lookups.GetProductsAsync(category, cancellationToken));
}
