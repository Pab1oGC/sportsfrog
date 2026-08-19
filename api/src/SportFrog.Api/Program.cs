using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
