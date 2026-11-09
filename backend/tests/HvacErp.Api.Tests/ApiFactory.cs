using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HvacErp.Infrastructure.Health;
using HvacErp.Infrastructure.Persistence;
using System.Net;

namespace HvacErp.Api.Tests;

/// <summary>
/// Boots the real API pipeline (routing, policies, exception handler) with an in-memory database and
/// a header-driven test authentication scheme in place of Supabase JWT validation.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string RoleHeader = "X-Test-Role";
    private readonly string _databaseName = $"api-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Supabase:Url", "https://test.supabase.co");
        builder.UseSetting("Supabase:ServiceRoleKey", "test-key");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=unused");
        foreach (var (key, value) in ExtraSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Storage readiness probe answers from a stub instead of the network.
            services.AddHttpClient<SupabaseStorageHealthCheck>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(() => StorageStatus));
        });
    }

    /// <summary>Extra configuration for a derived factory (e.g. switching the mock ERP on).</summary>
    protected virtual IEnumerable<(string Key, string Value)> ExtraSettings => [];

    /// <summary>Status the stubbed Supabase Storage bucket lookup returns.</summary>
    public HttpStatusCode StorageStatus { get; set; } = HttpStatusCode.OK;

    public HttpClient ClientAs(string? role)
    {
        var client = CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(RoleHeader, role);
        }
        return client;
    }
}

/// <summary>Authenticates any request carrying <see cref="ApiFactory.RoleHeader"/> as a user with that role.</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiFactory.RoleHeader, out var role) || string.IsNullOrEmpty(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "00000000-0000-0000-0000-000000000001"),
            new Claim("email", $"{role.ToString().ToLowerInvariant()}@example.com"),
            new Claim(ClaimTypes.Role, role.ToString()),
        ], SchemeName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Answers every request with a fixed status code.</summary>
public sealed class StubHandler(Func<HttpStatusCode> status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status()) { RequestMessage = request });
}
