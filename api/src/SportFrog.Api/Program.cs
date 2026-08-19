using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Organizations;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'.");

// The application connects as sportfrog_app: it can read and write data but
// cannot modify the schema or bypass the isolation policies. Migrations are
// applied out of band, by the schema owner.
builder.Services.AddSingleton(SportFrogDataSource.Create(connectionString));
builder.Services.AddDbContext<SportFrogDbContext>((services, options) =>
    options.UseNpgsql(
        services.GetRequiredService<Npgsql.NpgsqlDataSource>(),
        SportFrogDataSource.MapEnums));

// Validated on start rather than on first use: a missing or too-short signing
// key must stop the process, not surface as a failed login much later.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(services =>
{
    var options = services.GetRequiredService<IOptions<JwtOptions>>().Value;

    return new JwtAccessTokenIssuer(
        options.SigningKey, options.Issuer, options.Audience, options.AccessTokenLifetime);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtAccessTokenIssuer>((options, issuer) =>
    {
        // The same rules the issuer applies, taken from the issuer itself:
        // two definitions of "valid" would drift, and the looser one would
        // become the real gate.
        options.TokenValidationParameters = issuer.CreateValidationParameters();

        // Claims keep the names they were written with instead of being
        // renamed to their WS-* URIs, so "org:{id}" survives the round trip.
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<OrganizationContext>();

// The public view reads with its own database user, which holds no write
// permission: its read-only condition is enforced by the engine and not by
// the absence of a write in the code (DD-08).
var publicConnectionString = builder.Configuration.GetConnectionString("Public")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Public'.");

builder.Services.AddKeyedSingleton(
    PublicCompetitionReader.PublicDataSourceKey,
    (_, _) => SportFrogDataSource.Create(publicConnectionString));

builder.Services.AddScoped<PublicCompetitionReader>();

// Cost factor left at the default; it travels inside each hash, so raising
// it later does not invalidate what is already stored.
builder.Services.AddSingleton<BCryptPasswordHasher>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// After authentication, so the organization can be resolved from a validated
// token, and before any endpoint, so nothing reaches the database outside the
// isolation context.
app.UseMiddleware<OrganizationContextMiddleware>();

app.MapRegisterOrganization();

app.MapGet("/health", () => Results.Ok())
    .AllowAnonymous()
    .WithoutOrganizationContext();

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
