namespace HvacErp.Domain.Enums;

// Status/type sets that carry real business behaviour — RepairStatus, VatOption, DuctSystemType,
// QuoteDepartment, DaySlot, QuoteStatus — are DB-backed lookup tables (see Domain/Entities/LookupTables.cs
// and Domain/Enums/LookupCodes.cs), not C# enums, so their labels/rates/flags are data, not a deploy.
// The three below stay real enums: they gate authorization policies / unit-compatibility branches and
// are structurally unlikely to ever gain a value.

/// <summary>Application role of a signed-in user; drives the API authorization policies and Postgres RLS.</summary>
public enum UserRole
{
    Admin,
    Sales,
    Warehouse
}

/// <summary>Whether a quote line is the external (condenser) or an internal (split) unit.</summary>
public enum ProductRole
{
    ExternalUnit,
    InternalUnit
}

/// <summary>A corrective repair or a periodic revision.</summary>
public enum RepairType
{
    Repair,
    Revision
}

/// <summary>
/// The three PDFs the in-app template editor can lay out. Stays a real enum (like
/// <see cref="RepairType"/>): it is a fixed, small set that gates which
/// <c>PdfTemplateCatalog</c> section list applies, not business metadata that would ever
/// need to be data-driven.
/// </summary>
public enum PdfDocumentType
{
    Quote,
    Repair,
    Installation
}
