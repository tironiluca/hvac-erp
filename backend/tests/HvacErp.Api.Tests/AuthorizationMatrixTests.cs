using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using HvacErp.Api.Authorization;

namespace HvacErp.Api.Tests;

public class AuthorizationMatrixTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly HttpStatusCode[] Denied = [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden];

    public static TheoryData<string, string> Mutations => new()
    {
        { "POST", "/api/clients" },
        { "PUT", "/api/clients/1" },
        { "POST", "/api/quotes" },
        { "PUT", "/api/quotes/1" },
        { "POST", "/api/quotes/1/confirm" },
        { "POST", "/api/quotes/1/recall" },
        { "POST", "/api/quotes/1/survey" },
        { "POST", "/api/quotes/1/incentives/1/confirmation" },
        { "POST", "/api/quotes/1/erp-export" },
        { "DELETE", "/api/quotes/1" },
        { "POST", "/api/quotes/1/pdf" },
        { "POST", "/api/repairs" },
        { "PUT", "/api/repairs/1" },
        { "POST", "/api/repairs/1/status" },
        { "POST", "/api/repairs/1/schedule" },
        { "DELETE", "/api/repairs/1" },
        { "POST", "/api/installations" },
        { "PATCH", "/api/installations/1/schedule" },
        { "DELETE", "/api/installations/1" },
        { "POST", "/api/fgas-records" },
        { "DELETE", "/api/fgas-records/1" },
    };

    private static HttpRequestMessage Request(string method, string url) =>
        new(new HttpMethod(method), url) { Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { }) };

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Warehouse_users_cannot_mutate_business_data(string method, string url)
    {
        var response = await factory.ClientAs("Warehouse").SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Sales_users_pass_authorization_for_business_mutations(string method, string url)
    {
        // The (empty) payloads / missing rows make these 400/404 — the point is they are not 401/403.
        var response = await factory.ClientAs("Sales").SendAsync(Request(method, url));

        Assert.DoesNotContain(response.StatusCode, Denied);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Anonymous_callers_are_challenged(string method, string url)
    {
        var response = await factory.ClientAs(null).SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/clients")]
    [InlineData("/api/quotes")]
    [InlineData("/api/repairs")]
    [InlineData("/api/installations")]
    [InlineData("/api/lookups/vat-options")]
    [InlineData("/api/me")]
    public async Task Any_role_can_read(string url)
    {
        var response = await factory.ClientAs("Warehouse").GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Sales", HttpStatusCode.Forbidden)]
    [InlineData("Warehouse", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Pdf_template_settings_are_admin_only(string role, HttpStatusCode expected)
    {
        var response = await factory.ClientAs(role).GetAsync("/api/pdf-templates");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("Sales", HttpStatusCode.Forbidden)]
    [InlineData("Warehouse", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.NotFound)] // authorized; the client simply does not exist
    public async Task Deleting_a_client_is_admin_only(string role, HttpStatusCode expected)
    {
        var response = await factory.ClientAs(role).DeleteAsync("/api/clients/987654");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/stores")]
    [InlineData("POST", "/api/admin/stores")]
    [InlineData("PUT", "/api/admin/stores/1")]
    [InlineData("GET", "/api/admin/members")]
    [InlineData("POST", "/api/admin/members")]
    [InlineData("PUT", "/api/admin/members/1")]
    [InlineData("DELETE", "/api/admin/members/1")]
    public async Task Stores_and_members_are_administered_by_the_tenants_admin_only(string method, string url)
    {
        foreach (var role in new[] { "Sales", "Warehouse" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientAs(role).SendAsync(Request(method, url))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.ClientAs(null).SendAsync(Request(method, url))).StatusCode);
        Assert.DoesNotContain((await factory.ClientAs("Admin").SendAsync(Request(method, url))).StatusCode, Denied);
    }

    [Fact]
    public async Task Branding_is_readable_anonymously_for_the_login_page()
    {
        var response = await factory.ClientAs(null).GetAsync("/api/branding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Every_mutating_action_declares_a_role_policy_or_is_explicitly_anonymous()
    {
        var controllers = typeof(Policies).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var offenders = new List<string>();
        foreach (var controller in controllers)
        {
            var classPolicy = controller.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy is not null);
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var verbs = action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).ToList();
                if (verbs.Count == 0 || verbs.All(v => v == "GET"))
                {
                    continue;
                }

                var actionPolicy = action.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy is not null);
                var anonymous = action.GetCustomAttributes<AllowAnonymousAttribute>().Any();
                if (!classPolicy && !actionPolicy && !anonymous)
                {
                    offenders.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }
}
