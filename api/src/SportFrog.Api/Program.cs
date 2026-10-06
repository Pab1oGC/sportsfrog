using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Athletes.Photos;
using SportFrog.Api.Features.Auth;
using SportFrog.Api.Features.Draw;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Features.Competitions.Bulletin;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Features.Categories;
using SportFrog.Api.Features.Clubs;
using SportFrog.Api.Features.Organizations;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Features.Rosters;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Features.MatchEvents;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Features.Reports;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Features.Teams;
using SportFrog.Api.Features.Venues;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Email;
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

// A safety net for whatever an endpoint's own Results.Problem calls do not
// anticipate. See GlobalExceptionHandler for what this does and, as
// important, does not change: every deliberate 4xx an endpoint already
// returns never reaches this at all.
builder.Services.AddExceptionHandler<SportFrog.Api.Infrastructure.Observability.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
        //
        // X-Total-Count is the same story for a paged listing (see
        // PagedListing): the header carries the real row count across the
        // network correctly either way, but a script reading it needs to be
        // told it may, or it reads nothing back and cannot tell "5 rows,
        // page 1 of 1" from "5 rows, page 1 of many" — in production this
        // never mattered (the panel is same-origin behind nginx, where CORS
        // does not apply at all), only in local development against this
        // policy.
        .WithExposedHeaders("Content-Disposition", "X-Total-Count"));
});

builder.Services.AddScoped<OrganizationContext>();

// Files live in object storage rather than in a column: a photograph is a
// megabyte no query ever filters on, and a column carries it into every
// backup, every replica and every listing that reads the row.
builder.Services.AddSportFrogStorage(builder.Configuration);

// Outgoing mail — a club told its fixture moved, today the only thing that
// sends any. Optional on purpose: see SmtpOptions's remarks.
builder.Services.AddSportFrogEmail(builder.Configuration);

// Work that outlives a request: a batch of photographs is minutes of decoding
// and uploading, and no browser waits for that. The queue lives in the same
// database, so a job and the row it is about are written together or not at
// all.
builder.Services.AddSportFrogJobs(connectionString);
builder.Services.AddScoped<AttachAthletePhotosJob>();

// Short-lived, in-process cache for the handful of public-portal reads that
// are expensive to build and cheap to serve slightly stale — see
// IPublicQueryCache for the freshness contract.
builder.Services.AddSportFrogPublicCaching();

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

builder.Services.AddSingleton<SportFrog.Api.Features.Rulebook.IRulesetShapeRules, SportFrog.Api.Features.Rulebook.CumulativeRulesetShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Rulebook.IRulesetShapeRules, SportFrog.Api.Features.Rulebook.SetsRulesetShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Rulebook.IRulesetShapeRules, SportFrog.Api.Features.Rulebook.JudgedRulesetShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Rulebook.IRulesetShapeRulesRegistry, SportFrog.Api.Features.Rulebook.RulesetShapeRulesRegistry>();
builder.Services.AddSingleton<SportFrog.Api.Features.Draw.ICalendarDraw, SportFrog.Api.Features.Draw.LeagueCalendarDraw>();
builder.Services.AddSingleton<SportFrog.Api.Features.Draw.ICalendarDraw, SportFrog.Api.Features.Draw.GroupsCalendarDraw>();
builder.Services.AddSingleton<SportFrog.Api.Features.Draw.ICalendarDraw, SportFrog.Api.Features.Draw.KnockoutCalendarDraw>();
builder.Services.AddSingleton<SportFrog.Api.Features.Draw.ICalendarDrawRegistry, SportFrog.Api.Features.Draw.CalendarDrawRegistry>();

// Singleton like the draws above: today's four providers hold no state and
// read the database only through the instance LoadAsync is handed, never
// through their own constructor. A future provider that needs a scoped
// service (ObjectStore, AthletePhoto) cannot be registered this way — the
// container refuses a scoped dependency inside a singleton at startup, which
// is the point at which that provider's registration becomes AddScoped
// instead, not something to guess at before any provider needs it.
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.StandingsList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.LeadersList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.CardsList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.RosterList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.RosterByPositionList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.AthletesList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.MatchesList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListProvider, SportFrog.Api.Features.Lists.Providers.ClassificationList>();
builder.Services.AddSingleton<SportFrog.Api.Features.Lists.IListRegistry, SportFrog.Api.Features.Lists.ListRegistry>();
builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Rulebook.RulesetUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Categories.CategoryUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Competitions.CompetitionActivity>();
builder.Services.AddScoped<SportFrog.Api.Features.Teams.TeamUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Clubs.UnaffiliatedClub>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.RosterPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.RosterUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Venues.VenueUsage>();
builder.Services.AddScoped<SportFrog.Api.Features.Accreditation.AccreditationResolver>();

// Only ever asked to follow a shortened Google Maps link so the location
// picker can read the coordinates it hides — never an arbitrary admin-typed
// URL, which ManageVenues.ResolveMapsLinkAsync enforces by host allowlist
// before this is ever reached. Redirects and the response body are capped
// tight: this exists to read where a redirect landed, not to fetch content.
// AllowAutoRedirect is off on purpose: SafeRedirectResolver follows the
// chain itself, one hop at a time, so every address it lands on — not only
// the first — is checked before this connects to it. See its own remarks
// for why the handler's own redirect-chasing was not enough by itself.
builder.Services.AddHttpClient("MapsLinkResolver", client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });
builder.Services.AddScoped<SportFrog.Api.Features.Matches.FixturePolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Matches.MatchRescheduleNotificationJob>();
builder.Services.AddScoped<SportFrog.Api.Features.Performances.PerformancePolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Performances.PerformanceRescheduleNotificationJob>();

// Mode-specific rules, resolved through their registries rather than a mode
// check — a new score mode is a new registration on these four lines, not a
// change to anything that already depends on them.
builder.Services.AddSingleton<SportFrog.Domain.Rules.IMatchOutcomeRules, SportFrog.Domain.Rules.CumulativeMatchOutcomeRules>();
builder.Services.AddSingleton<SportFrog.Domain.Rules.IMatchOutcomeRules, SportFrog.Domain.Rules.SetsMatchOutcomeRules>();
builder.Services.AddSingleton<SportFrog.Domain.Rules.IMatchOutcomeRules, SportFrog.Domain.Rules.JudgedMatchOutcomeRules>();
builder.Services.AddSingleton<SportFrog.Domain.Rules.IMatchOutcomeRulesRegistry, SportFrog.Domain.Rules.MatchOutcomeRulesRegistry>();
builder.Services.AddSingleton<SportFrog.Api.Features.Matches.IResultShapeRules, SportFrog.Api.Features.Matches.CumulativeResultShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Matches.IResultShapeRules, SportFrog.Api.Features.Matches.SetsResultShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Matches.IResultShapeRules, SportFrog.Api.Features.Matches.JudgedResultShape>();
builder.Services.AddSingleton<SportFrog.Api.Features.Matches.IResultShapeRulesRegistry, SportFrog.Api.Features.Matches.ResultShapeRulesRegistry>();

builder.Services.AddScoped<SportFrog.Api.Features.Matches.MatchRulesLookup>();
builder.Services.AddScoped<SportFrog.Api.Features.Matches.ResultPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.MatchEvents.EventPolicy>();
builder.Services.AddScoped<SportFrog.Api.Features.Athletes.AthletePhoto>();
builder.Services.AddScoped<SportFrog.Api.Features.Clubs.ClubPhoto>();
builder.Services.AddScoped<SportFrog.Api.Infrastructure.Storage.PortalPicture>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.Import.RosterImportReview>();
builder.Services.AddScoped<SportFrog.Api.Features.Rosters.Import.DelegationRosterImportReview>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.TemplateBackground>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.TemplateWriter>();
builder.Services.AddScoped<SportFrog.Api.Features.Documents.CredentialNumbering>();
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

// The very first thing in the pipeline, ahead of even the forwarded-headers
// handling below: it has to wrap everything downstream, including
// UseSerilogRequestLogging's own re-throw, to catch whatever none of those
// anticipated.
app.UseExceptionHandler();

// First in the pipeline, ahead of logging, rate limiting and everything else
// that reads Connection.RemoteIpAddress: behind any reverse proxy that
// address is the proxy's, the same one for every visitor, not each caller's.
// The rate limiter partitions by it (RateLimitPolicies.PartitionByCaller) and
// OrganizationContextMiddleware records it against every establish — both
// silently share one budget and one address across the whole audience
// without this.
//
// KnownNetworks and KnownProxies are cleared rather than left at their
// default of loopback-only. This application does not know, and cannot know
// from here, what address the reverse proxy in front of it will have — that
// is decided by whoever deploys it, on infrastructure this repository has no
// view of. Trusting the immediate hop unconditionally is the standard trade
// for that situation, and it is a safe one only because Kestrel is never
// meant to be reachable directly: whoever deploys this is responsible for
// making sure the only path in is through their reverse proxy.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};

// KnownIPNetworks and KnownProxies come pre-populated with loopback, and
// assigning "= { }" to either would only mean "add nothing to what is
// already there" — the properties have no setter, so that line would compile
// and do nothing. Clearing them is a statement, not an initializer.
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeaders);

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
api.MapReadOrganization();
api.MapUpdateOrganization();
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
api.MapReadCompetitionBulletin();

api.MapCreateCategory();
api.MapReadCategories();
api.MapUpdateCategory();
api.MapDeleteCategory();

api.MapCreateTeam();
api.MapEnrollIndividual();
api.MapReadTeams();
api.MapUpdateTeam();
api.MapDeleteTeam();

api.MapRegisterPlayer();
api.MapRegisterPlayersBulk();
api.MapReadRoster();
api.MapCorrectRegistration();
api.MapWithdrawPlayer();
api.MapStrikeRegistration();
api.MapBuildRosterTemplate();
api.MapPreviewRosterImport();
api.MapApplyRosterImport();

api.MapBuildDelegationRosterTemplate();
api.MapPreviewDelegationRosterImport();
api.MapApplyDelegationRosterImport();

api.MapOpenClassificationStage();
api.MapRecordPerformance();
api.MapReadPerformances();
api.MapSchedulePerformance();
api.MapRescheduleClassificationOrder();

api.MapImportAthletePhotos();
api.MapReadPhotoImports();

api.MapAccreditationItems();
api.MapAccreditationCategories();
api.MapAthleteAccreditations();
api.MapAthleteAccreditationOverrides();

api.MapReadTemplateDesign();
api.MapUploadTemplateBackground();
api.MapCreateTemplate();
api.MapReadTemplates();
api.MapUpdateTemplate();
api.MapDeleteTemplate();
api.MapCreateCredentialDesign();
api.MapReadCredentialDesigns();
api.MapUpdateCredentialDesign();
api.MapDeleteCredentialDesign();
api.MapRequestDocumentBatch();
api.MapReadDocuments();
api.MapRevokeDocument();
api.MapPreviewCredential();

api.MapVenues();
api.MapVenueSpaces();

api.MapScheduleMatch();
api.MapReadMatches();
api.MapRescheduleMatch();
api.MapRescheduleMatchesBulk();
api.MapDeleteMatch();
api.MapRecordResult();
api.MapChangeMatchStatus();
api.MapAwardWalkover();
api.MapRecordPenalties();

api.MapRecordEvent();
api.MapReadEvents();
api.MapCorrectEvent();
api.MapDeleteEvent();

api.MapReadStandings();
api.MapReadLeaders();
api.MapReadTeamReport();
api.MapReadAthleteReport();
api.MapReadLists();

api.MapDrawGroups();
api.MapDrawCalendar();
api.MapScheduleCalendar();
api.MapAdvanceBracket();
api.MapDrawRepechage();
api.MapPromoteGroupStage();
api.MapPromoteClassification();

api.MapReadPublicCompetitions();
api.MapReadPublicRecentResults();
api.MapReadPublicCompetition();
api.MapReadPublicCompetitionPreview();
api.MapReadPublicTables();
api.MapReadPublicClassification();
api.MapReadPublicCalendar();
api.MapReadPublicRoster();
api.MapReadPublicMatchEvents();

// Desconectado a propósito (2026-09-27): la verificación pública de
// credenciales no hace falta todavía — la emisión y el diseño siguen activos,
// solo esta ruta queda sin mapear. VerifyDocument.cs no se tocó: para volver
// a activarla alcanza con descomentar la línea de abajo.
// api.MapVerifyDocument();

api.MapCreateAthlete();
api.MapReadAthletes();
api.MapUpdateAthlete();
api.MapDeleteAthlete();
api.MapSignIn();
api.MapRenewSession();
api.MapSignOut();
api.MapChangePassword();

app.MapGet("/health", () => Results.Ok())
    .AllowAnonymous()
    .WithoutOrganizationContext();

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
