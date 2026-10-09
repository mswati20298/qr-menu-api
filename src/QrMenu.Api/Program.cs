using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QrMenu.Api;
using QrMenu.Api.Filters;
using QrMenu.Api.Middleware;
using QrMenu.Application.Auth;
using QrMenu.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Platform;
using QrMenu.Application.PublicMenu;
using QrMenu.Infrastructure;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

const string AngularCorsPolicy = "AngularDevClient";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddInfrastructure(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// Anyone who knows the signing secret can forge a login token (even a super admin one),
// so never run outside Development with a missing, short or placeholder secret.
var jwtSecretIsUnsafe = string.IsNullOrWhiteSpace(jwtSettings.Secret)
    || jwtSettings.Secret.Length < 32
    || jwtSettings.Secret.StartsWith("REPLACE_THIS", StringComparison.OrdinalIgnoreCase);
if (jwtSecretIsUnsafe && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Jwt:Secret is missing or still the placeholder. Set a long random secret (32+ characters) via environment variable or user-secrets before running outside Development.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

builder.Services.AddScoped<ActiveRestaurantFilter>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? ["http://localhost:4200"])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
    // Read-only data any site may fetch (the landing page testimonials). GET only, no cookies or tokens.
    options.AddPolicy(QrMenu.Api.Controllers.PublicReadCors.Policy, policy => policy.AllowAnyOrigin().WithMethods("GET"));
});

// Behind Cloudflare + Caddy the real client address arrives in X-Forwarded-For (Caddy fills it from
// CF-Connecting-IP). Needed for per-client rate limits and correct https links.
var forwardedHeadersEnabled = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Only the reverse proxy on the private Docker network can reach the API, so trust it.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.ForwardLimit = 1;
    });
}

// "auth": sign-in and sign-up (on top of the per-email lockout). "public": customer actions without a login.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many requests. Please wait a minute and try again.\"}", ct);
    };

    static string ClientKey(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy(RateLimits.Auth, http => RateLimitPartition.GetFixedWindowLimiter(ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy(RateLimits.Public, http => RateLimitPartition.GetFixedWindowLimiter(ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "QrMenu API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Sample restaurant only where asked for (local development and the Demo deployment), never in Prod.
    await DbSeeder.SeedAsync(
        db,
        builder.Configuration.GetValue("Seed:DemoData", false),
        scope.ServiceProvider.GetRequiredService<IFileStorageService>());
    await SuperAdminSeeder.SeedAsync(
        db,
        builder.Configuration,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SuperAdminSeeder"));
    // Payment and AI keys saved in the super admin panel.
    await scope.ServiceProvider.GetRequiredService<IPlatformKeysService>().LoadAsync();
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

// API explorer only where it is switched on (on by default in Development, off in Demo and Prod).
if (builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(AngularCorsPolicy);

// Uploaded photos have random, never reused names, so browsers and Cloudflare may keep them for 30 days.
// Set here (only on real files), not in Caddy: a header there also went on 404s, and Cloudflare kept those.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// Warm-up: the first menu request after a start compiles the code paths and EF queries, which can take
// several seconds. Do that once in the background now, so the first guest after a deploy does not wait.
app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = await db.Restaurants.AsNoTracking().Where(r => r.IsActive).Select(r => r.Slug).FirstOrDefaultAsync();
        if (slug is not null)
        {
            await scope.ServiceProvider.GetRequiredService<IPublicMenuService>().GetMenuAsync(slug);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Warm-up request failed");
    }
}));

app.Run();
