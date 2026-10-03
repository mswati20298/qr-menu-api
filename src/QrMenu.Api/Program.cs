using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QrMenu.Api.Filters;
using QrMenu.Api.Middleware;
using QrMenu.Application.Auth;
using QrMenu.Application.Common.Interfaces;
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
    await DbSeeder.SeedAsync(db);
    await SuperAdminSeeder.SeedAsync(
        db,
        builder.Configuration,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SuperAdminSeeder"));
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(AngularCorsPolicy);

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
