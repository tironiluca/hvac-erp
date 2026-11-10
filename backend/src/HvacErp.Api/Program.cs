using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using HvacErp.Api;
using HvacErp.Api.Authorization;
using HvacErp.Api.Hubs;
using HvacErp.Api.Hubs.Presence;
using HvacErp.Api.Observability;
using HvacErp.Application;
using HvacErp.Application.Common;
using HvacErp.Infrastructure;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Structured logs: one JSON object per line outside Development (Render's log stream / any log
// drain can filter on fields), with scopes so every line carries the request's CorrelationId.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders().AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    });
}

// Error reporting: Sentry is wired only when a DSN is configured (Sentry__Dsn on Render), so local
// runs and tests stay offline. No PII is sent — client data never leaves via error reports.
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(options =>
    {
        options.Dsn = sentryDsn;
        options.Environment = builder.Environment.EnvironmentName;
        options.SendDefaultPii = false;
        options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        options.TracesSampleRate = 0;
    });
}

builder.Services.AddControllers()
    // Enums as strings ("Standard22", "ExternalUnit") over the wire instead of raw ints —
    // self-documenting in Swagger and avoids the frontend needing to hardcode magic numbers.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Surface the <summary> docs of every layer (controllers + DTOs + domain codes) in Swagger UI.
    foreach (var xml in Directory.EnumerateFiles(AppContext.BaseDirectory, "HvacErp.*.xml"))
    {
        options.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }
});

// SignalR powers the shared real-time installation calendar (server → client fan-out of
// ScheduledInstallation changes). Enum values go over the hub as strings too, to match the REST wire.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    // Redis backplane + shared presence roster when Redis:ConnectionString is set; in-process otherwise.
    .AddCalendarScaleOut(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Exposes the current request's authenticated principal to Application services (audit stamping).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
// Which tenant and stores the request works in; read by the database context on every query and save.
builder.Services.AddScoped<ITenantContext, RequestTenantContext>();

// SignalR-backed implementation of the Application-layer calendar broadcast port.
builder.Services.AddScoped<IInstallationCalendarNotifier, InstallationCalendarNotifier>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Lets the SPA read the id to quote in a support request.
            .WithExposedHeaders(RequestCorrelationMiddleware.HeaderName)
            // Required for the SignalR browser client (credentialed WebSocket / SSE handshake).
            .AllowCredentials());
});

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseAuthority = $"{supabaseUrl}/auth/v1";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Supabase signs tokens with an asymmetric key (ECC/RSA); keys are fetched and
        // cached from the project's OIDC discovery document rather than a shared secret,
        // which also lets Supabase rotate signing keys without an app redeploy.
        options.Authority = supabaseAuthority;

        // Keep JWT claim names as-issued (e.g. "sub", "role") instead of ASP.NET Core's
        // legacy remapping to long ClaimTypes.* URIs. Without this, Supabase's own "role"
        // claim (a Postgres role, always "authenticated") gets remapped to ClaimTypes.Role
        // and SupabaseUserClaimsTransformation's `HasClaim(ClaimTypes.Role)` guard would
        // see it as already present, so our AppUser.Role claim would never get added.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(supabaseUrl),
            ValidIssuer = supabaseAuthority,
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };

        // The browser WebSocket client can't send an Authorization header, so the SignalR JS
        // client passes the Supabase access token as `?access_token=` on the hub URL instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder().AddAppPolicies();

var app = builder.Build();

// Seed commands write through a context fixed on one tenant and its first store: the first tenant
// (the one the migration creates) unless `--tenant <id>` names another.
// `seed-demo` layers a large, interlinked demo/test dataset (clients, quotes, repairs,
// installations, F-Gas records) on top of the lookup seed. Idempotent — see DemoDataSeeder.
var seedDemo = args.Contains("seed-demo", StringComparer.OrdinalIgnoreCase);
if (seedDemo || args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    var tenantArg = Array.FindIndex(args, a => string.Equals(a, "--tenant", StringComparison.OrdinalIgnoreCase));
    int? seedTenantId = tenantArg >= 0 && tenantArg + 1 < args.Length && int.TryParse(args[tenantArg + 1], out var parsed) ? parsed : null;

    using var scope = app.Services.CreateScope();
    await using var context = await SeedContext.CreateAsync(scope.ServiceProvider, seedTenantId);
    if (seedDemo)
    {
        var storageUrl = builder.Configuration["Supabase:Url"];
        var bucket = builder.Configuration["Supabase:StorageBucket"];
        var demoAssets = string.IsNullOrWhiteSpace(storageUrl) || string.IsNullOrWhiteSpace(bucket)
            ? null
            : $"{storageUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/demo";
        await DemoDataSeeder.SeedAsync(context, demoAssets);
    }
    else
    {
        await DevDataSeeder.SeedAsync(context);
    }

    return;
}

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapAppHealthChecks();
app.MapHub<InstallationCalendarHub>("/hubs/installations");

app.Run();
