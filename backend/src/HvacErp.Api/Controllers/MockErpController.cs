using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HvacErp.Application.Erp;
using HvacErp.Infrastructure.Erp;

namespace HvacErp.Api.Controllers;

/// <summary>
/// A mock of the main ERP's order API (<c>/mock-erp</c>), for development and demos: the
/// <c>Http</c> ERP connector can point at it. Off unless <c>MockErp:Enabled</c>; machine-to-machine,
/// so it takes an API key instead of a user session.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("mock-erp")]
[ApiExplorerSettings(IgnoreApi = true)]
public class MockErpController(MockErpService mock) : ControllerBase
{
    private const string KeyHeader = "X-Api-Key";

    /// <summary>
    /// Receives an order: 201 with <c>{ orderNumber }</c>, 200 with the same number for an order
    /// already received (by external reference), 503 when failing on purpose.
    /// </summary>
    [HttpPost("api/orders")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveOrder([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (Denied(Request.Headers[KeyHeader]) is { } denied)
        {
            return denied;
        }
        if (mock.ShouldFail())
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Mock ERP: simulated outage" });
        }

        ErpOrder order;
        try
        {
            order = ErpOrder.FromJson(body.GetRawText());
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = $"Not an ERP order document: {ex.Message}" });
        }
        if (string.IsNullOrWhiteSpace(order.ExternalReference) || order.Customer is null || order.Lines is null)
        {
            return BadRequest(new { error = "externalReference, customer and lines are required." });
        }

        var receipt = await mock.ReceiveAsync(order, body.GetRawText(), cancellationToken);
        return receipt.Duplicate
            ? Ok(new { orderNumber = receipt.OrderNumber, duplicate = true })
            : StatusCode(StatusCodes.Status201Created, new { orderNumber = receipt.OrderNumber, duplicate = false });
    }

    /// <summary>The latest orders received, as JSON.</summary>
    [HttpGet("api/orders")]
    public async Task<IActionResult> ListOrders(CancellationToken cancellationToken)
    {
        if (Denied(Request.Headers[KeyHeader]) is { } denied)
        {
            return denied;
        }

        var orders = await mock.ListAsync(100, cancellationToken);
        return Ok(orders.Select(o => new { o.OrderNumber, o.ExternalReference, o.CustomerName, o.FinalPrice, o.LineCount, o.ReceivedAt }));
    }

    /// <summary>A page listing the received orders (<c>/mock-erp?key=…</c>).</summary>
    [HttpGet]
    public async Task<IActionResult> Page([FromQuery] string? key, CancellationToken cancellationToken)
    {
        if (Denied(key) is { } denied)
        {
            return denied;
        }

        var orders = await mock.ListAsync(100, cancellationToken);
        var rows = new StringBuilder();
        foreach (var o in orders)
        {
            rows.Append($"""
                <tr>
                  <td><b>{Html(o.OrderNumber)}</b></td>
                  <td>{o.ReceivedAt:yyyy-MM-dd HH:mm} UTC</td>
                  <td>{Html(o.ExternalReference)}</td>
                  <td>{Html(o.CustomerName)}</td>
                  <td class="num">{o.FinalPrice:N2} €</td>
                  <td class="num">{o.LineCount}</td>
                </tr>
                <tr><td colspan="6"><details><summary>Order document</summary><pre>{Html(MockErpService.Pretty(o.PayloadJson))}</pre></details></td></tr>
                """);
        }

        var html = $$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Mock ERP</title>
            <style>
              body { font: 14px system-ui, sans-serif; margin: 24px; color: #1d1d1f; background: #fafafa; }
              h1 { font-size: 20px; margin: 0 0 4px; } p { color: #555; margin: 0 0 16px; }
              table { border-collapse: collapse; width: 100%; background: #fff; }
              th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid #e5e5e5; vertical-align: top; }
              th { background: #f0f0f0; } .num { text-align: right; white-space: nowrap; }
              pre { margin: 8px 0 0; font-size: 12px; overflow: auto; max-height: 360px; background: #f6f8fa; padding: 8px; }
              @media (prefers-color-scheme: dark) { body { background: #111; color: #eee; } table { background: #1b1b1b; }
                th { background: #262626; } th, td { border-color: #333; } p { color: #aaa; } pre { background: #222; } }
            </style></head>
            <body>
              <h1>Mock ERP: received orders</h1>
              <p>{{orders.Count}} most recent orders pushed by HVAC ERP. Development mock of the main ERP's order API.</p>
              <table>
                <thead><tr><th>ERP order</th><th>Received</th><th>Reference</th><th>Customer</th><th class="num">Total</th><th class="num">Lines</th></tr></thead>
                <tbody>{{(rows.Length == 0 ? "<tr><td colspan=\"6\">No orders yet.</td></tr>" : rows.ToString())}}</tbody>
              </table>
            </body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>404 while the mock is off (as if it did not exist), 401 for a wrong or missing key.</summary>
    private IActionResult? Denied(string? key) =>
        !mock.Enabled ? NotFound()
        : !mock.IsAuthorized(key) ? Unauthorized()
        : null;

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
