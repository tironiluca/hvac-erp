using HvacErp.Application.Common;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Dtos;

/// <summary>
/// Maps loaded Quote aggregates to DTOs. Callers must have eager-loaded Client, Operator,
/// Products (with Product), DuctedUnits (with Outlets), ServiceItems (with ServiceItem),
/// Extras (with ExtraType), StatusEvents and Attachments.
/// </summary>
public static class QuoteMappingExtensions
{
    /// <summary>Human-facing quote number: <c>Number/Version</c>, plus <c>.Revision</c> when revised (e.g. <c>12/3.2</c>).</summary>
    public static string DisplayNumber(this Quote q) =>
        q.Revision > 0 ? $"{q.Number}/{q.Version}.{q.Revision}" : $"{q.Number}/{q.Version}";

    /// <summary>Maps a quote (with Client loaded) to its list-row DTO.</summary>
    public static QuoteListItemDto ToListItemDto(this Quote q) => new(
        q.Id, q.Number, q.Version, q.Revision,
        q.ClientId, q.Client.FullName, q.Client.Code,
        q.Department, q.CreatedAt, q.CurrentStatus, q.FinalPrice);

    /// <summary>Maps a fully loaded quote to its detail DTO, deriving the survey / recall / confirm flags from the status history.</summary>
    public static QuoteDetailDto ToDetailDto(this Quote q)
    {
        var survey = LatestEvent(q, QuoteStatusCodes.SurveyDone);
        var recall = LatestEvent(q, QuoteStatusCodes.Recalled);
        var confirm = LatestEvent(q, QuoteStatusCodes.Confirmed);

        return new QuoteDetailDto(
            q.Id, q.Number, q.Version, q.Revision, q.ParentQuoteId,
            q.ClientId, q.Client.FullName,
            q.OperatorId, q.Operator?.Name,
            q.Department, q.CreatedAt,
            q.CurrentStatus,
            survey is not null, survey?.OccurredAt,
            recall is not null, recall?.OccurredAt, recall?.Notes,
            confirm is not null, confirm?.OccurredAt,
            q.Predisposed, q.Vat, q.ManualDiscount,
            q.MachinesPrice, q.InstallationPrice, q.Rounding, q.FinalPrice,
            q.EneaEligible, q.EneaPrice,
            q.RaeeEligible, q.RaeePrice,
            q.SfRequested, q.SfConfirmed, q.SfPrice,
            q.InstallationAddress, q.InstallationCity,
            q.InstallationPositionNotes, q.FloorPlanImageUrl, q.PdfUrl,
            q.Products.Select(p => p.ToDto()).ToList(),
            q.DuctedUnits.Select(d => d.ToDto()).ToList(),
            q.ServiceItems.Select(s => s.ToDto()).ToList(),
            q.Extras.Select(e => e.ToDto()).ToList(),
            q.StatusEvents.OrderBy(e => e.OccurredAt)
                .Select(e => new QuoteStatusEventDto(e.Id, e.Status, e.OccurredAt, e.Notes, e.ChangedBy)).ToList(),
            q.Attachments.Select(a => new QuoteAttachmentDto(a.Id, a.FileName, a.FileUrl, a.UploadedAt)).ToList(),
            FloorPlanSketchJson.Parse(q.FloorPlanSketchJson));
    }

    private static QuoteStatusEvent? LatestEvent(Quote q, string status) =>
        q.StatusEvents.Where(e => e.Status == status).OrderByDescending(e => e.OccurredAt).FirstOrDefault();

    /// <summary>Maps a quote child row to its DTO.</summary>
    public static QuoteProductDto ToDto(this QuoteProduct p) => new(
        p.Id, p.ProductId, p.Product.Brand, p.Product.Model, p.Role, p.Quantity,
        p.Price, p.DiscountedPrice, p.Warranty, p.WarrantyPrice);

    /// <summary>Maps a quote child row to its DTO.</summary>
    public static QuoteServiceItemDto ToDto(this QuoteServiceItem s) => new(
        s.Id, s.ServiceItemId, s.ServiceItem.Name, s.Quantity, s.UnitPrice);

    /// <summary>Maps a quote child row to its DTO.</summary>
    public static QuoteExtraDto ToDto(this QuoteExtra e) => new(
        e.Id, e.ExtraTypeId, e.ExtraType.Code, e.ExtraType.Label, e.ExtraType.QuantityBased, e.Quantity, e.UnitPrice);

    /// <summary>Maps a quote child row to its DTO.</summary>
    public static DuctedUnitDto ToDto(this DuctedUnit d) => new(
        d.Id,
        d.CloudQuantity, d.PlenumQuantity, d.HasFlexibleHose, d.FlexibleHoseQuantity,
        d.HasRigidHose, d.RigidHoseQuantity, d.RemoteQuantity,
        d.Outlets.OrderBy(o => o.Position)
            .Select(o => new DuctedUnitOutletDto(o.Id, o.Position, o.SystemType, o.VentCount)).ToList());
}
