namespace HvacErp.Domain.Entities;

/// <summary>
/// Outbox row for sending a confirmed quote to the main ERP as an order. Written in the same
/// transaction as the confirmation, so nothing is lost while the ERP is unreachable; a background
/// worker sends due rows and records the outcome. One row per quote.
/// </summary>
public class ErpOrderExport
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    /// <summary>The connector the export goes through (the <c>Erp:Provider</c> at enqueue time).</summary>
    public string Connector { get; set; } = string.Empty;

    /// <summary>One of <see cref="ErpExportStatuses"/>.</summary>
    public string Status { get; set; } = ErpExportStatuses.Pending;

    public int Attempts { get; set; }

    /// <summary>When the worker may try again; null once sent or given up.</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>The order number the ERP assigned, once accepted.</summary>
    public string? ErpOrderNumber { get; set; }

    public string? LastError { get; set; }

    /// <summary>The order document (JSON) as built at confirmation: what is sent, unchanged by retries.</summary>
    public string PayloadJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

/// <summary>States of an <see cref="ErpOrderExport"/>.</summary>
public static class ErpExportStatuses
{
    /// <summary>Waiting to be sent (first try or a retry after a temporary failure).</summary>
    public const string Pending = "Pending";

    /// <summary>Accepted by the ERP; <see cref="ErpOrderExport.ErpOrderNumber"/> is set.</summary>
    public const string Sent = "Sent";

    /// <summary>Rejected, or retries exhausted; needs attention (a manual send tries again).</summary>
    public const string Failed = "Failed";
}
