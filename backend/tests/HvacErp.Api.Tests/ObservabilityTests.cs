using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HvacErp.Api.Tests;

public class ObservabilityTests
{
    [Fact]
    public async Task Liveness_is_anonymous_and_runs_no_dependency_checks()
    {
        await using var factory = new ApiFactory { StorageStatus = HttpStatusCode.InternalServerError };

        var response = await factory.ClientAs(null).GetAsync("/health/live");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("checks").EnumerateObject());
    }

    [Fact]
    public async Task Readiness_reports_database_and_storage()
    {
        await using var factory = new ApiFactory();

        var response = await factory.ClientAs(null).GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("storage").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_is_503_when_storage_is_down_without_leaking_details()
    {
        await using var factory = new ApiFactory { StorageStatus = HttpStatusCode.Unauthorized };

        var response = await factory.ClientAs(null).GetAsync("/health");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"storage\":{\"status\":\"Unhealthy\"", raw);
        Assert.DoesNotContain("401", raw);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_when_well_formed_and_minted_otherwise()
    {
        await using var factory = new ApiFactory();
        var client = factory.ClientAs("Sales");

        var echoed = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        echoed.Headers.Add("X-Correlation-Id", "spa-1234abcd");
        var forged = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        forged.Headers.Add("X-Correlation-Id", "<script>alert(1)</script>");

        var a = await client.SendAsync(echoed);
        var b = await client.SendAsync(forged);

        Assert.Equal("spa-1234abcd", a.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Matches("^[0-9a-f]{32}$", b.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Problem_responses_carry_the_correlation_id()
    {
        await using var factory = new ApiFactory();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/quotes/424242");
        request.Headers.Add("X-Correlation-Id", "trace-abcdef12");

        var response = await factory.ClientAs("Sales").SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("trace-abcdef12", problem.GetProperty("correlationId").GetString());
    }
}
