using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Athletes.Photos;
using SportFrog.Api.Features.Auth;
using SportFrog.Api.Features.Draw;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Features.Clubs;
using SportFrog.Api.Features.Organizations;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Features.MatchEvents;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Features.Teams;
using SportFrog.Api.Features.Venues;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Observability;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.RateLimiting;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Named once so the registration and the use cannot drift apart.
const string FrontendCors = "FrontendDevelopment";

// QuestPDF asks which licence this runs under and refuses to generate
// anything until it is told. Community is the one that applies: this is not a
// product sold on, and the threshold it sets is revenue nobody here is near.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
        .AddInterceptors(services.GetRequiredService<AuditInterceptor>())

        // Several entities are the required end of a relationship whose
        // principal is soft-deleted, and EF warns that filtering the principal
        // away may give unexpected results. Here it gives exactly the intended
        // one, in every case, which is why the warning is silenced rather than
        // answered by adding filters:
        //
        //   · An issued document and the batch that printed it outlive the
        //     competition being retired. A credential somebody is carrying does
        //     not stop existing because a league was tidied up, and it still
        //     has to verify (RF-45).
        //   · A stored template version outlives its template for the same
        //     reason: retiring a design must not make everything printed from
        //     it unreprintable.
        //   · A membership of a deleted organization, and a refresh token of a
        //     deleted user, should indeed disappear — which is what the filter
        //     already does.
        //
        // Left on, it is five lines of noise at every start-up, and noise at
        // start-up is how a real warning goes unread.
        .ConfigureWarnings(warnings => warnings.Ignore(
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

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

// The browser refuses a cross-origin call unless the API says the origin is
// welcome, and the development front-end runs on its own port. Origins are
// listed rather than reflected: echoing back whatever origin asked would let
// any page on the internet call this API with the visitor's cookies.
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()

        // AllowAnyHeader speaks about the request. A response header the
        // browser is not told to expose is hidden from the script that made
        // the call, and Content-Disposition is where every download carries
        // its filename — a spreadsheet named after its team, a credential
        // named after its batch. Without this the front-end reads nothing
        // there and falls back to one generic name for all of them, which
        // only shows up when somebody stops using the development proxy.
        .WithExposedHeaders("Content-Disposition"));
});

builder.Services.AddScoped<OrganizationContext>();

// Files live in object storage rather than in a column: a photograph is a
// megabyte no query ever filters on, and a column carries it into every
// backup, every replica and every listing that reads the row.
builder.Services.AddSportFrogStorage(builder.Configuration);

// Work that outlives a request: a batch of photographs is minutes of decoding
// and uploading, and no browser waits for that. The queue lives in the same
// database, so a job and the row it is about are written together or not at
// all.
builder.Services.AddSportFrogJobs(connectionString);
builder.Services.AddScoped<AttachAthletePhotosJob>();

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
builder.Services.AddScoped<PublicDocumentReader>();

// Cost factor left at the default; it travels inside each hash, so raising
// it later does not invalidate what is already stored.
builder.Services.AddSingleton<BCryptPasswordHasher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionIssuer>();

builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Competitions.CompetitionActivity>();
builder.Services.AddScoped<SportFrog.Api.Features.Teams.TeamUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.RosterPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.RosterUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Venues.VenueUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Matches.FixturePolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Matches.ResultPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.MatchEvents.EventPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Athletes.AthletePhoto>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.Import.RosterImportReview>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.TemplateBackground>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.TemplateWriter>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.IssueDocumentsJob>();

// Printed on physical cards, so a wrong address is unrecoverable. Validated
// on start rather than discovered on a laminated credential.
builder.Services
    .AddOptions<SportFrog.Api.Features.Documents.DocumentOptions>()
    .Bind(builder.Configuration.GetSection(SportFrog.Api.Features.Documents.DocumentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// A validator exists, so its contract is validated. Nothing is wired per
// endpoint (DD-07).
// includeInternalTypes: the validators are internal on purpose — they are an
// implementation detail of their slice, not part of anyone's API — and the
// scanner skips those unless told otherwise. Without this the filter finds no
// validator and every contract passes unchecked, silently.
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

app.UseSportFrogRequestLogging();

// Before everything that can refuse a request. A preflight is an OPTIONS with
// no credentials on it, so the entry channel would answer it with a 401 long
// before any header was added — and the browser would report the refusal as a
// CORS failure, which is the least helpful way to be told about it. Ahead of
// the rate limiter too, so preflights do not spend a caller's budget.
app.UseCors(FrontendCors);

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
api.MapChangeCompetitionStatus();
api.MapPublishCompetition();

api.MapCreateCategory();
api.MapReadCategories();
api.MapUpdateCategory();
api.MapDeleteCategory();

api.MapCreateTeam();
api.MapReadTeams();
api.MapUpdateTeam();
api.MapDeleteTeam();

api.MapRegisterPlayer();
api.MapReadRoster();
api.MapCorrectRegistration();
api.MapWithdrawPlayer();
api.MapStrikeRegistration();
api.MapBuildRosterTemplate();
api.MapPreviewRosterImport();
api.MapApplyRosterImport();

api.MapImportAthletePhotos();
api.MapReadPhotoImports();

api.MapReadTemplateDesign();
api.MapUploadTemplateBackground();
api.MapCreateTemplate();
api.MapReadTemplates();
api.MapUpdateTemplate();
api.MapDeleteTemplate();
api.MapRequestDocumentBatch();
api.MapReadDocuments();
api.MapRevokeDocument();

api.MapVenues();
api.MapVenueSpaces();

api.MapScheduleMatch();
api.MapReadMatches();
api.MapRescheduleMatch();
api.MapDeleteMatch();
api.MapRecordResult();
api.MapChangeMatchStatus();
api.MapAwardWalkover();

api.MapRecordEvent();
api.MapReadEvents();
api.MapCorrectEvent();
api.MapDeleteEvent();

api.MapReadStandings();
api.MapReadLeaders();

api.MapDrawCalendar();
api.MapScheduleCalendar();
api.MapAdvanceBracket();

api.MapReadPublicCompetitions();
api.MapReadPublicCompetition();
api.MapReadPublicTables();
api.MapReadPublicCalendar();
api.MapReadPublicRoster();
api.MapVerifyDocument();

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
