using System.Globalization;

namespace HvacErp.Application.Pdf;

/// <summary>
/// The fixed captions the PDF generators print around the template's own text: document
/// meta lines, table headers, totals, signature captions. Selected per template by
/// <see cref="PdfPageConfig.Language"/>. Section headings and body text are not here. They are
/// template data the admin edits.
/// </summary>
public sealed record PdfLabels
{
    /// <summary>English, the product default.</summary>
    public const string English = "en";

    /// <summary>Italian.</summary>
    public const string Italian = "it";

    /// <summary>Languages a template can be printed in.</summary>
    public static readonly IReadOnlyList<string> Supported = [English, Italian];

    /// <summary>Number / currency formatting for amounts (both cultures print euros).</summary>
    public required CultureInfo Culture { get; init; }

    // Header and footer
    public required string QuoteRecipient { get; init; }
    public required string ClientHeading { get; init; }
    public required string Phone { get; init; }
    public required string Mobile { get; init; }
    public required string Email { get; init; }
    public required string Page { get; init; }
    public required string PageOf { get; init; }
    public required string Sheet { get; init; }

    // Quote
    public required string QuoteNumber { get; init; }
    public required string DatedOn { get; init; }
    public required string Agent { get; init; }
    public required string Model { get; init; }
    public required string Quantity { get; init; }
    public required string EnergyClass { get; init; }
    public required string Gas { get; init; }
    public required string Price { get; init; }
    public required string DuctType { get; init; }
    public required string VentCount { get; init; }
    public required string Plenum { get; init; }
    public required string Remote { get; init; }
    public required string PreparedSystem { get; init; }
    public required string SupplyAndInstallationTotal { get; init; }
    public required string EneaEligible { get; init; }
    public required string RaeeIncluded { get; init; }
    public required string InvoiceDiscount { get; init; }
    public required string InvoiceTotalWithVat { get; init; }
    public required string WarrantyIncluded { get; init; }
    public required string EneaPaperworkIncluded { get; init; }
    public required string TotalWithInvoiceDiscount { get; init; }
    public required string Description { get; init; }
    public required string UnitPrice { get; init; }
    public required string Total { get; init; }

    // Repair
    public required string RepairSheetNumber { get; init; }
    public required string Revision { get; init; }
    public required string Urgent { get; init; }
    public required string Repair { get; init; }
    public required string Brand { get; init; }
    public required string UnitCount { get; init; }
    public required string SplitCount { get; init; }
    public required string ScheduledDate { get; init; }
    public required string Prepared { get; init; }
    public required string FlatRate { get; init; }
    public required string Yes { get; init; }
    public required string No { get; init; }
    public required string Date { get; init; }
    public required string Installer { get; init; }
    public required string Notes { get; init; }
    public required string FlatRateTotal { get; init; }
    public required string RepairTotal { get; init; }

    // Installation
    public required string InstallationSheetNumber { get; init; }
    public required string QuoteReference { get; init; }
    public required string NoInstallerAssigned { get; init; }
    public required string ClientSignature { get; init; }
    public required string TechnicianSignature { get; init; }

    /// <summary>The label set for a template language; unknown or empty falls back to English.</summary>
    public static PdfLabels For(string? language) =>
        string.Equals(language, Italian, StringComparison.OrdinalIgnoreCase) ? ItalianLabels : EnglishLabels;

    /// <summary>Formats an amount in this language's culture (always euros).</summary>
    public string Money(decimal value) => value.ToString("C2", Culture);

    private static readonly PdfLabels EnglishLabels = new()
    {
        // en-IE: English formatting with the euro symbol.
        Culture = CultureInfo.GetCultureInfo("en-IE"),
        QuoteRecipient = "To",
        ClientHeading = "Client",
        Phone = "Tel",
        Mobile = "Mobile",
        Email = "Email",
        Page = "Page",
        PageOf = "of",
        Sheet = "Sheet",
        QuoteNumber = "QUOTE NO.",
        DatedOn = "dated",
        Agent = "Agent",
        Model = "Model",
        Quantity = "Qty",
        EnergyClass = "Energy class (cool/heat)",
        Gas = "Gas",
        Price = "Price",
        DuctType = "Type",
        VentCount = "Vents",
        Plenum = "Plenum",
        Remote = "Remote",
        PreparedSystem = "Pre-installed piping",
        SupplyAndInstallationTotal = "Supply and installation total",
        EneaEligible = "Eligible for the ENEA tax deduction",
        RaeeIncluded = "WEEE collection and disposal included",
        InvoiceDiscount = "INVOICE DISCOUNT",
        InvoiceTotalWithVat = "Invoice total, VAT and installation included",
        WarrantyIncluded = "All-inclusive warranty included",
        EneaPaperworkIncluded = "ENEA paperwork included",
        TotalWithInvoiceDiscount = "Total payable with the 50% invoice discount",
        Description = "Description",
        UnitPrice = "Unit price",
        Total = "Total",
        RepairSheetNumber = "SERVICE SHEET NO.",
        Revision = "SERVICE",
        Urgent = "URGENT",
        Repair = "REPAIR",
        Brand = "Brand",
        UnitCount = "Units",
        SplitCount = "Splits",
        ScheduledDate = "Scheduled date",
        Prepared = "Pre-installed",
        FlatRate = "Flat rate",
        Yes = "Yes",
        No = "No",
        Date = "Date",
        Installer = "Installer",
        Notes = "Notes",
        FlatRateTotal = "Flat-rate total",
        RepairTotal = "Service total",
        InstallationSheetNumber = "INSTALLATION SHEET NO.",
        QuoteReference = "Quote no.",
        NoInstallerAssigned = "No installer assigned",
        ClientSignature = "Client signature",
        TechnicianSignature = "Technician signature",
    };

    private static readonly PdfLabels ItalianLabels = new()
    {
        Culture = CultureInfo.GetCultureInfo("it-IT"),
        QuoteRecipient = "Spett.le",
        ClientHeading = "Cliente",
        Phone = "Tel",
        Mobile = "Cell",
        Email = "Email",
        Page = "Pagina",
        PageOf = "di",
        Sheet = "Foglio",
        QuoteNumber = "OFFERTA N.",
        DatedOn = "del",
        Agent = "Agente",
        Model = "Modello",
        Quantity = "Qt.",
        EnergyClass = "Classe En. (raff/risc)",
        Gas = "Gas",
        Price = "Prezzo",
        DuctType = "Tipo",
        VentCount = "N. Bocchette",
        Plenum = "Plenum",
        Remote = "Comando",
        PreparedSystem = "Impianto predisposto",
        SupplyAndInstallationTotal = "Totale fornitura e installazione",
        EneaEligible = "Idoneo a detrazione fiscale ENEA",
        RaeeIncluded = "Ritiro e smaltimento RAEE incluso",
        InvoiceDiscount = "SCONTO IN FATTURA",
        InvoiceTotalWithVat = "Totale fattura IVA ed installazione comprese",
        WarrantyIncluded = "Garanzia All Inclusive compresa",
        EneaPaperworkIncluded = "Pratica ENEA compresa",
        TotalWithInvoiceDiscount = "Totale a pagare con sconto in fattura 50%",
        Description = "Descrizione",
        UnitPrice = "Prezzo Un.",
        Total = "Totale",
        RepairSheetNumber = "SCHEDA INTERVENTO N.",
        Revision = "REVISIONE",
        Urgent = "URGENTE",
        Repair = "RIPARAZIONE",
        Brand = "Marca",
        UnitCount = "N. unità",
        SplitCount = "N. split",
        ScheduledDate = "Data prevista",
        Prepared = "Predisposto",
        FlatRate = "Forfettario",
        Yes = "Sì",
        No = "No",
        Date = "Data",
        Installer = "Installatore",
        Notes = "Note",
        FlatRateTotal = "Totale forfettario",
        RepairTotal = "Totale intervento",
        InstallationSheetNumber = "SCHEDA DI INSTALLAZIONE N.",
        QuoteReference = "Preventivo N.",
        NoInstallerAssigned = "Nessun installatore assegnato",
        ClientSignature = "Firma Cliente",
        TechnicianSignature = "Firma Tecnico",
    };
}
