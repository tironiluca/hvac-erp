using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using HvacErp.Application.Erp;

namespace HvacErp.Infrastructure.Erp;

/// <summary>
/// Generic ERP connector: POSTs the <see cref="ErpOrder"/> document as JSON to
/// <c>{Erp:BaseUrl}/orders</c> with an API-key header and reads <c>{ "orderNumber": "…" }</c> back.
/// Fits ERPs (or an integration middleware) that accept a JSON order webhook, and the mock ERP.
/// 2xx (and 409 with an order number, an already-received order) is success; 408, 429, 5xx and
/// network errors are temporary; other 4xx are rejections.
/// </summary>
public sealed class HttpJsonErpConnector(HttpClient http, IOptionsMonitor<ErpOptions> options) : IErpConnector
{
    /// <summary>Provider name.</summary>
    public const string ProviderName = "Http";

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async Task<ErpPushResult> PushOrderAsync(ErpOrder order, CancellationToken cancellationToken = default)
    {
        var settings = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return ErpPushResult.Rejected("Erp:BaseUrl is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/orders")
        {
            Content = JsonContent.Create(order, options: ErpOrder.JsonOptions),
        };
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            request.Headers.Add(settings.ApiKeyHeader, settings.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            response = await http.SendAsync(request, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ErpPushResult.TryLater($"ERP not reachable: {ex.Message}");
        }

        using (response)
        {
            var orderNumber = await ReadOrderNumberAsync(response, cancellationToken);
            if (response.IsSuccessStatusCode || (response.StatusCode == HttpStatusCode.Conflict && orderNumber is not null))
            {
                return orderNumber is null
                    ? ErpPushResult.Rejected("The ERP accepted the order but returned no order number.")
                    : ErpPushResult.Accepted(orderNumber);
            }

            var error = $"ERP answered {(int)response.StatusCode} {response.ReasonPhrase}";
            return response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500
                ? ErpPushResult.TryLater(error)
                : ErpPushResult.Rejected(error);
        }
    }

    private static async Task<string?> ReadOrderNumberAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("orderNumber", out var number)
                   && number.ValueKind == JsonValueKind.String
                ? number.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
