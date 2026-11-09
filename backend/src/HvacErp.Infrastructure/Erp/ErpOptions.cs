namespace HvacErp.Infrastructure.Erp;

/// <summary>
/// The main-ERP connection (<c>Erp</c> section; <c>Erp__…</c> environment variables in deployment).
/// </summary>
public sealed class ErpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Erp";

    /// <summary>Provider name of the connector to use: <c>None</c> (off) or <c>Http</c>.</summary>
    public string Provider { get; set; } = "None";

    /// <summary><c>Http</c>: base URL of the ERP's order API (orders are POSTed to <c>{BaseUrl}/orders</c>).</summary>
    public string? BaseUrl { get; set; }

    /// <summary><c>Http</c>: API key sent in <see cref="ApiKeyHeader"/>. A secret: set it as an environment variable.</summary>
    public string? ApiKey { get; set; }

    /// <summary><c>Http</c>: header carrying <see cref="ApiKey"/>.</summary>
    public string ApiKeyHeader { get; set; } = "X-Api-Key";

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>How often the background worker looks for due exports, in seconds.</summary>
    public int PollSeconds { get; set; } = 30;
}

/// <summary>The mock ERP hosted by the API for development and demos (<c>MockErp</c> section).</summary>
public sealed class MockErpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "MockErp";

    /// <summary>Serves the <c>/mock-erp</c> endpoints; off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>API key callers must send (header <c>X-Api-Key</c>, or <c>?key=</c> on the page). Required when enabled.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Share of order pushes answered with 503, to exercise the retry path (0–1).</summary>
    public double FailureRate { get; set; }
}
