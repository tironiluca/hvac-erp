namespace HvacErp.Application.Erp;

/// <summary>
/// A connection to a main ERP. Implementations translate <see cref="ErpOrder"/> to their ERP's API;
/// which one is used is configuration (<c>Erp:Provider</c>), resolved by <see cref="IErpConnectorResolver"/>.
/// </summary>
public interface IErpConnector
{
    /// <summary>The provider name it is selected by (e.g. <c>Http</c>).</summary>
    string Name { get; }

    /// <summary>Sends one order. Must be safe to repeat for the same <see cref="ErpOrder.ExternalReference"/>.</summary>
    Task<ErpPushResult> PushOrderAsync(ErpOrder order, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a push. <see cref="Retryable"/> tells a temporary failure (timeout, 5xx, throttling)
/// from a rejection that retrying will not fix (validation, 4xx).
/// </summary>
public sealed record ErpPushResult(bool Success, string? ErpOrderNumber, string? Error, bool Retryable)
{
    /// <summary>Accepted, with the ERP's order number.</summary>
    public static ErpPushResult Accepted(string orderNumber) => new(true, orderNumber, null, false);

    /// <summary>A temporary failure; try again later.</summary>
    public static ErpPushResult TryLater(string error) => new(false, null, error, true);

    /// <summary>A permanent rejection.</summary>
    public static ErpPushResult Rejected(string error) => new(false, null, error, false);
}

/// <summary>Picks the configured connector at runtime.</summary>
public interface IErpConnectorResolver
{
    /// <summary>The configured connector, or null when ERP integration is off (<c>Erp:Provider=None</c>).</summary>
    IErpConnector? Current { get; }

    /// <summary>A connector by name (an export keeps the connector it was enqueued for), or null.</summary>
    IErpConnector? ByName(string name);
}
