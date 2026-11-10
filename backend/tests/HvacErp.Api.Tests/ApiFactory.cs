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
using HvacErp.Application.Common;
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
    public const string UserHeader = "X-Test-User";
    public const string TenantHeader = "X-Test-Tenant";
    public const string StoresHeader = "X-Test-Stores";
    public const string PlatformAdminHeader = "X-Test-Platform-Admin";

    /// <summary>
    /// Signs the caller in as this user id with nothing but the claims Supabase issues, so the real
    /// sign-in resolution (database lookups, <c>X-Tenant-Id</c> / <c>X-Store-Id</c>) decides the rest.
    /// </summary>
    public const string SupabaseIdentityHeader = "X-Test-Supabase-User";

    /// <summary>Role value that signs a caller in without a membership in any tenant.</summary>
    public const string NoMembership = "None";
    public const string DefaultUserId = "00000000-0000-0000-0000-000000000001";
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

    /// <summary>A client signed in with <paramref name="role"/> in <paramref name="tenantId"/>, reading <paramref name="stores"/> (comma-separated ids).</summary>
    public HttpClient ClientAs(string? role, int tenantId = 1, string stores = "1", bool platformAdmin = false)
    {
        var client = CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(RoleHeader, role);
            client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());
            client.DefaultRequestHeaders.Add(StoresHeader, stores);
            if (platformAdmin)
            {
                client.DefaultRequestHeaders.Add(PlatformAdminHeader, "true");
            }
        }
        return client;
    }
}

/// <summary>
/// Authenticates any request carrying <see cref="ApiFactory.RoleHeader"/> as a user with that role in
/// tenant 1 / store 1 — or in the tenant and stores named by <see cref="ApiFactory.TenantHeader"/> /
/// <see cref="ApiFactory.StoresHeader"/>. The role <see cref="ApiFactory.NoMembership"/> signs the
/// caller in without any tenant. The claims are the ones <c>SupabaseUserClaimsTransformation</c>
/// would add, so the transformation leaves the principal alone.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(ApiFactory.SupabaseIdentityHeader, out var supabaseUser))
        {
            var raw = new ClaimsIdentity(
                [new Claim("sub", supabaseUser.ToString()), new Claim("email", $"{supabaseUser}@example.com")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(raw), SchemeName)));
        }

        if (!Request.Headers.TryGetValue(ApiFactory.RoleHeader, out var role) || string.IsNullOrEmpty(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue(ApiFactory.UserHeader, out var user) ? user.ToString() : ApiFactory.DefaultUserId;
        List<Claim> claims =
        [
            new("sub", userId),
            new(AppClaimTypes.UserId, userId),
            new("email", $"{role.ToString().ToLowerInvariant()}@example.com"),
        ];

        if (role != ApiFactory.NoMembership)
        {
            var tenant = Request.Headers.TryGetValue(ApiFactory.TenantHeader, out var t) ? t.ToString() : "1";
            var stores = (Request.Headers.TryGetValue(ApiFactory.StoresHeader, out var s) ? s.ToString() : "1")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            claims.Add(new Claim(AppClaimTypes.TenantId, tenant));
            claims.AddRange(stores.Select(id => new Claim(AppClaimTypes.StoreId, id)));
            if (stores.Length == 1)
            {
                claims.Add(new Claim(AppClaimTypes.ActiveStoreId, stores[0]));
            }
        }

        if (Request.Headers.ContainsKey(ApiFactory.PlatformAdminHeader))
        {
            claims.Add(new Claim(AppClaimTypes.PlatformAdmin, "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Answers every request with a fixed status code.</summary>
public sealed class StubHandler(Func<HttpStatusCode> status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status()) { RequestMessage = request });
}
