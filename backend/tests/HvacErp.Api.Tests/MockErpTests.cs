using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using HvacErp.Application.Erp;
using HvacErp.Infrastructure.Erp;

namespace HvacErp.Api.Tests;

/// <summary>An API host with the mock ERP switched on.</summary>
public sealed class MockErpApiFactory : ApiFactory
{
    public const string Key = "test-mock-erp-key";

    protected override IEnumerable<(string Key, string Value)> ExtraSettings =>
        [("MockErp:Enabled", "true"), ("MockErp:ApiKey", Key)];
}

public class MockErpTests(MockErpApiFactory factory) : IClassFixture<MockErpApiFactory>
{
    private static ErpOrder Order(string reference) => new(
        ErpOrder.CurrentSchemaVersion, reference, "12/1", 12, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), null,
        "Sam", "Store", "EUR", new ErpVat("Standard22", "VAT 22%"),
        new ErpCustomer("C1", "Alex Morgan", null, null, null, null, null), null,
        [new ErpOrderLine(1, ErpLineKinds.Product, null, "Outdoor unit", 1, 1000, 900, 900)],
        new ErpTotals(900, 0, 0, 0, 1098, null, null), null);

    private HttpRequestMessage Post(ErpOrder order, string? key = MockErpApiFactory.Key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mock-erp/api/orders")
        {
            Content = JsonContent.Create(order, options: ErpOrder.JsonOptions),
        };
        if (key is not null)
        {
            request.Headers.Add("X-Api-Key", key);
        }
        return request;
    }

    [Fact]
    public async Task Orders_need_the_api_key_and_are_numbered_once_per_reference()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Post(Order("quote-1"), key: null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Post(Order("quote-1"), key: "wrong"))).StatusCode);

        var first = await client.SendAsync(Post(Order("quote-1")));
        var again = await client.SendAsync(Post(Order("quote-1")));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var firstNumber = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString();
        var againNumber = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderNumber").GetString();
        Assert.Matches(@"^ERP-\d{4}-\d{6}$", firstNumber!);
        Assert.Equal(firstNumber, againNumber);
    }

    [Fact]
    public async Task The_page_lists_received_orders_for_the_key_holder_only()
    {
        var client = factory.CreateClient();
        await client.SendAsync(Post(Order("quote-page")));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/mock-erp")).StatusCode);
        var page = await client.GetStringAsync($"/mock-erp?key={MockErpApiFactory.Key}");

        Assert.Contains("quote-page", page);
        Assert.Contains("Alex Morgan", page);
    }

    [Fact]
    public async Task The_http_connector_pushes_to_the_mock_end_to_end()
    {
        var options = new StaticOptions(new ErpOptions { Provider = "Http", BaseUrl = "/mock-erp/api", ApiKey = MockErpApiFactory.Key });
        var http = factory.CreateClient();
        var connector = new HttpJsonErpConnector(http, options);

        var accepted = await connector.PushOrderAsync(Order("quote-http"));
        var repeated = await connector.PushOrderAsync(Order("quote-http"));
        var rejected = await new HttpJsonErpConnector(factory.CreateClient(),
            new StaticOptions(new ErpOptions { BaseUrl = "/mock-erp/api", ApiKey = "wrong" })).PushOrderAsync(Order("quote-x"));

        Assert.True(accepted.Success);
        Assert.Equal(accepted.ErpOrderNumber, repeated.ErpOrderNumber);
        Assert.False(rejected.Success);
        Assert.False(rejected.Retryable); // 401: retrying will not help
    }

    private sealed class StaticOptions(ErpOptions value) : IOptionsMonitor<ErpOptions>
    {
        public ErpOptions CurrentValue => value;
        public ErpOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ErpOptions, string?> listener) => null;
    }
}

public class MockErpDisabledTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_mock_does_not_exist_unless_enabled()
    {
        var response = await factory.CreateClient().GetAsync("/mock-erp?key=anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
