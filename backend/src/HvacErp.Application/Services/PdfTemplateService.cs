using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IPdfTemplateService"/>: storage, validation, activation and previews.</summary>
public class PdfTemplateService(
    IAppDbContext context,
    ICurrentUser currentUser,
    IQuoteDocumentGenerator quoteGenerator,
    IRepairDocumentGenerator repairGenerator,
    IInstallationDocumentGenerator installationGenerator) : IPdfTemplateService
{
    private const int MaxTemplatesPerType = 25;

    public PdfTemplateCatalogDto GetCatalog(PdfDocumentType documentType) => new(
        documentType,
        PdfTemplateCatalog.For(documentType)
            .Select(s => new PdfSectionCatalogDto(
                s.Type, s.LabelKey, s.Repeatable, s.SupportsBody,
                s.Columns.Select(c => new PdfColumnOptionDto(c.Key, c.LabelKey, c.DefaultOn)).ToList(),
                s.Options.Select(o => new PdfOptionToggleDto(o.Key, o.LabelKey, o.DefaultOn)).ToList()))
            .ToList(),
        PdfTemplateCatalog.ClientFields.Select(f => new PdfFieldOptionDto(f.Key, f.LabelKey)).ToList(),
        PdfTemplateCatalog.FontFamilies.ToList());

    public async Task<List<PdfTemplateListItemDto>> ListAsync(
        PdfDocumentType? documentType, CancellationToken cancellationToken = default)
    {
        var query = context.PdfTemplates.AsNoTracking();
        if (documentType is { } type)
        {
            query = query.Where(t => t.DocumentType == type);
        }

        return await query
            .OrderBy(t => t.DocumentType).ThenByDescending(t => t.IsActive).ThenBy(t => t.Name)
            .Select(t => new PdfTemplateListItemDto(
                t.Id, t.DocumentType, t.Name, t.IsActive, t.IsSystemDefault, t.UpdatedAt, t.UpdatedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<PdfTemplateDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await context.PdfTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PdfTemplate), id);
        return ToDto(row);
    }

    public async Task<PdfTemplateDefinition> GetActiveDefinitionAsync(
        PdfDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var json = await context.PdfTemplates.AsNoTracking()
            .Where(t => t.DocumentType == documentType && t.IsActive)
            .Select(t => t.DefinitionJson)
            .FirstOrDefaultAsync(cancellationToken);

        return json is null ? PdfTemplateDefaults.Default(documentType) : PdfJson.Deserialize(json);
    }

    public async Task<PdfTemplateDto> CreateAsync(
        UpsertPdfTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(request.Name);
        PdfTemplateValidator.Validate(request.DocumentType, request.Definition);
        await EnsureNameFreeAsync(request.DocumentType, name, excludeId: null, cancellationToken);

        var count = await context.PdfTemplates.CountAsync(t => t.DocumentType == request.DocumentType, cancellationToken);
        if (count >= MaxTemplatesPerType)
        {
            throw new BusinessRuleException($"At most {MaxTemplatesPerType} templates per document type.");
        }

        var row = new PdfTemplate
        {
            DocumentType = request.DocumentType,
            Name = name,
            IsActive = false,
            IsSystemDefault = false,
            DefinitionJson = PdfJson.Serialize(request.Definition),
        };
        Stamp(row);
        context.PdfTemplates.Add(row);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<PdfTemplateDto> UpdateAsync(
        int id, UpsertPdfTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var row = await GetTrackedAsync(id, cancellationToken);
        var name = NormalizeName(request.Name);
        PdfTemplateValidator.Validate(row.DocumentType, request.Definition);
        await EnsureNameFreeAsync(row.DocumentType, name, excludeId: id, cancellationToken);

        row.Name = name;
        row.DefinitionJson = PdfJson.Serialize(request.Definition);
        Stamp(row);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<PdfTemplateDto> DuplicateAsync(
        int id, DuplicatePdfTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var source = await GetTrackedAsync(id, cancellationToken);
        var name = NormalizeName(request.Name);
        await EnsureNameFreeAsync(source.DocumentType, name, excludeId: null, cancellationToken);

        var copy = new PdfTemplate
        {
            DocumentType = source.DocumentType,
            Name = name,
            IsActive = false,
            IsSystemDefault = false,
            DefinitionJson = source.DefinitionJson,
        };
        Stamp(copy);
        context.PdfTemplates.Add(copy);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(copy);
    }

    public async Task<PdfTemplateDto> SetActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await GetTrackedAsync(id, cancellationToken);
        if (row.IsActive)
        {
            return ToDto(row);
        }

        var current = await context.PdfTemplates
            .Where(t => t.DocumentType == row.DocumentType && t.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var other in current)
        {
            other.IsActive = false;
        }

        row.IsActive = true;
        Stamp(row);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<PdfTemplateDto> RestoreDefaultsAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await GetTrackedAsync(id, cancellationToken);
        // Keep the template's language: restoring an Italian template gives the Italian defaults.
        var language = PdfJson.Deserialize(row.DefinitionJson).Page.Language;
        row.DefinitionJson = PdfJson.Serialize(PdfTemplateDefaults.Default(row.DocumentType, language));
        Stamp(row);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await GetTrackedAsync(id, cancellationToken);
        if (row.IsSystemDefault)
        {
            throw new BusinessRuleException("The built-in template cannot be deleted. Use \"Restore defaults\" instead.");
        }
        if (row.IsActive)
        {
            throw new BusinessRuleException("Activate another template before deleting this one.");
        }

        context.PdfTemplates.Remove(row);
        await context.SaveChangesAsync(cancellationToken);
    }

    public byte[] RenderPreview(PdfTemplatePreviewRequest request)
    {
        PdfTemplateValidator.Validate(request.DocumentType, request.Definition);
        var ctx = new PdfRenderContext(request.Definition, PortalBranding.DefaultPortalTitle, LogoImage: null);

        return request.DocumentType switch
        {
            PdfDocumentType.Quote => quoteGenerator.Generate(PdfSampleData.Quote(), PdfSampleData.VatLabel, ctx),
            PdfDocumentType.Repair => repairGenerator.Generate(PdfSampleData.Repair(), ctx),
            PdfDocumentType.Installation => installationGenerator.Generate(PdfSampleData.Installation(), PdfSampleData.SlotLabel, ctx),
            _ => throw new BusinessRuleException($"Unknown document type '{request.DocumentType}'."),
        };
    }

    private async Task<PdfTemplate> GetTrackedAsync(int id, CancellationToken cancellationToken) =>
        await context.PdfTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(PdfTemplate), id);

    private async Task EnsureNameFreeAsync(
        PdfDocumentType documentType, string name, int? excludeId, CancellationToken cancellationToken)
    {
        var taken = await context.PdfTemplates.AnyAsync(
            t => t.DocumentType == documentType && t.Name == name && (excludeId == null || t.Id != excludeId),
            cancellationToken);
        if (taken)
        {
            throw new ConflictException($"A {documentType} template named \"{name}\" already exists.");
        }
    }

    private void Stamp(PdfTemplate row)
    {
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = currentUser.Name;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BusinessRuleException("Template name must not be empty.");
        }
        if (trimmed.Length > 120)
        {
            throw new BusinessRuleException("Template name must be at most 120 characters.");
        }
        return trimmed;
    }

    private static PdfTemplateDto ToDto(PdfTemplate row) => new(
        row.Id, row.DocumentType, row.Name, row.IsActive, row.IsSystemDefault, row.UpdatedAt, row.UpdatedBy,
        PdfJson.Deserialize(row.DefinitionJson));
}
