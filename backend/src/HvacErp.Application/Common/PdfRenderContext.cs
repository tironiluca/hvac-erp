using HvacErp.Application.Pdf;

namespace HvacErp.Application.Common;

/// <summary>
/// Everything a QuestPDF generator needs beyond the entity itself: the admin-authored layout, plus
/// the branding bits (portal title, logo image bytes) that Phase 9 parked for the PDF headers. Built
/// by the <c>*PdfService</c> classes from the active <c>PdfTemplate</c> and the <c>PortalBranding</c>
/// row.
/// </summary>
/// <param name="Definition">The layout to render against (never null — falls back to <see cref="PdfTemplateDefaults"/>).</param>
/// <param name="PortalTitle">Brand title text for the header when the template shows it.</param>
/// <param name="LogoImage">PNG/JPEG bytes of the brand logo, or null when none is set / the download failed.</param>
public sealed record PdfRenderContext(PdfTemplateDefinition Definition, string PortalTitle, byte[]? LogoImage);
