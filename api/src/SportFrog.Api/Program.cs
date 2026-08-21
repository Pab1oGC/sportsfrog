using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Auth;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Features.Clubs;
using SportFrog.Api.Features.Organizations;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Observability;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSportFrogLogging();
builder.Services.AddSportFrogRateLimiting();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'.");

// The application connects as sportfrog_app: it can read and write data but
// cannot modify the schema or bypass the isolation policies. Migrations are
// applied out of band, by the schema owner.
builder.Services.AddSingleton(SportFrogDataSource.Create(connectionString));
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContext<SportFrogDbContext>((services, options) =>
    options
        .UseNpgsql(
            services.GetRequiredService<Npgsql.NpgsqlDataSource>(),
            SportFrogDataSource.MapEnums)
        // At the save point, so a change is recorded because it happened and
        // not because a feature remembered to say so (DD-07).
        .AddInterceptors(services.GetRequiredService<AuditInterceptor>()));

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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionIssuer>();

builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryUsage>();

// A validator exists, so its contract is validated. Nothing is wired per
// endpoint (DD-07).
// includeInternalTypes: the validators are internal on purpose — they are an
// implementation detail of their slice, not part of anyone's API — and the
// scanner skips those unless told otherwise. Without this the filter finds no
// validator and every contract passes unchecked, silently.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

app.UseSportFrogRequestLogging();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// After authentication, so the organization can be resolved from a validated
// token, and before any endpoint, so nothing reaches the database outside the
// isolation context.
app.UseMiddleware<OrganizationContextMiddleware>();

// Every endpoint hangs off this group, so validation covers an operation by
// the operation existing rather than by its author remembering.
var api = app.MapGroup("").ValidateContracts();

api.MapRegisterOrganization();
api.MapAddMember();

api.MapCreateClub();
api.MapReadClubs();
api.MapUpdateClub();
api.MapDeleteClub();

api.MapReadSports();
api.MapCreateRuleset();
api.MapReadRulesets();
api.MapUpdateRuleset();
api.MapDeleteRuleset();

api.MapCreateCompetition();
api.MapReadCompetitions();
api.MapUpdateCompetition();
api.MapDeleteCompetition();

api.MapCreateCategory();
api.MapReadCategories();
api.MapUpdateCategory();
api.MapDeleteCategory();

api.MapCreateAthlete();
api.MapReadAthletes();
api.MapUpdateAthlete();
api.MapDeleteAthlete();
api.MapSignIn();
api.MapRenewSession();
api.MapSignOut();

app.MapGet("/health", () => Results.Ok())
    .AllowAnonymous()
    .WithoutOrganizationContext();

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
