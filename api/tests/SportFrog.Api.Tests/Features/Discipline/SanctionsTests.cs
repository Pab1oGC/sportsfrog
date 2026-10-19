using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Discipline;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Discipline;

/// <summary>
/// Imposing a sanction, lifting or voiding it, waiving its fine, and the
/// one thing that ties them all to <see cref="ReadSanctions"/>: a
/// sanction's own status is never written down, only resolved fresh from
/// its terms against whatever has actually been served — see
/// <see cref="SanctionLedger"/>'s own remarks, exercised here through a
/// real calendar instead of hand-fed numbers (that side already has
/// <c>SanctionLedgerTests</c> in the domain project).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class SanctionsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(
        Guid OrgId, Guid UserId, Guid CompetitionId, Guid TeamId, Guid OpponentTeamId, Guid AthleteId);

    private static OrganizationContext OrganizationFor(Guid orgId, Guid userId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, userId, ipAddress: null);
        return organization;
    }

    /// <summary>
    /// One organization, one competition, a sanctioned team and an
    /// opponent, and one person to sanction — enough for every test here,
    /// none of it specific to any one of them.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var opponentClubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var opponentTeamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        setup.Users.Add(new User
        {
            Id = userId,
            Email = $"user-{userId:N}@example.com",
            PasswordHash = "hash",
            FullName = "Operador de prueba",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club Sancionado" });
        setup.Clubs.Add(new Club { Id = opponentClubId, OrgId = orgId, Name = "Club Rival" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = Football,
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = Football,
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria",
        });

        setup.Teams.Add(new Team { Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo Sancionado" });
        setup.Teams.Add(new Team { Id = opponentTeamId, OrgId = orgId, ClubId = opponentClubId, CategoryId = categoryId, Name = "Equipo Rival" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Jugador",
            LastName = "Sancionado",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2000, 1, 1),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, userId, competitionId, teamId, opponentTeamId, athleteId);
    }

    private async Task SeedMatchAsync(
        Fixture fx, MatchState status, DateTimeOffset? scheduledAt)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var categoryId = await context.Teams
            .Where(team => team.Id == fx.TeamId)
            .Select(team => team.CategoryId)
            .SingleAsync();

        context.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            CategoryId = categoryId,
            HomeTeamId = fx.TeamId,
            AwayTeamId = fx.OpponentTeamId,
            Status = status,
            ScheduledAt = scheduledAt,
            HomeTotal = status == MatchState.Finished ? 1 : null,
            AwayTotal = status == MatchState.Finished ? 0 : null,
            RecordedBy = status == MatchState.Finished ? fx.UserId : null,
            WalkoverTeamId = status == MatchState.Walkover ? fx.TeamId : null,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<IResult> ImposeAsync(Fixture fx, ImposeSanction.Request request)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ImposeSanction.HandleAsync(
            fx.CompetitionId, request, context, OrganizationFor(fx.OrgId, fx.UserId), Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<ReadSanctions.Response> ReadAsync(Fixture fx, Guid sanctionId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadSanctions.ReadAsync(
            fx.CompetitionId, sanctionId, context, new SanctionProgress(context), CancellationToken.None);

        return Assert.IsType<Ok<ReadSanctions.Response>>(result).Value!;
    }

    private async Task<IResult> LiftAsync(Fixture fx, Guid sanctionId, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await LiftSanction.HandleAsync(
            fx.CompetitionId, sanctionId, new LiftSanction.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> VoidAsync(Fixture fx, Guid sanctionId, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await VoidSanction.HandleAsync(
            fx.CompetitionId, sanctionId, new VoidSanction.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> WaiveFineAsync(Fixture fx, Guid sanctionId, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await WaiveSanctionFine.HandleAsync(
            fx.CompetitionId, sanctionId, new WaiveSanctionFine.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> WithholdAsync(Fixture fx, Guid sanctionId, bool withheld)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await WithholdCredential.HandleAsync(
            fx.CompetitionId, sanctionId, new WithholdCredential.Request(withheld), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private static Guid IdOf(IResult result) =>
        Assert.IsType<Created<ImposeSanction.Response>>(result).Value!.Id;

    // ---- Imposing ----------------------------------------------------

    [Fact]
    public async Task Impose_MatchesOnly_IsCreated()
    {
        var fx = await SeedAsync();

        var result = await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, MatchId: null, "Agresión a un rival",
            SanctionCompletionRule.Matches, MatchesRequired: 3, FineAmount: null));

        Assert.IsType<Created<ImposeSanction.Response>>(result);
    }

    // Cross-field pairing (matches_required/fine_amount against
    // completion_rule) is Validator territory — enforced by the FluentValidation
    // filter before HandleAsync is ever reached, and backstopped at the
    // database by ck_sanctions_matches_required_paired /
    // ck_sanctions_fine_amount_paired. Calling HandleAsync directly bypasses
    // that filter, so there is nothing of this module's own to test by doing
    // so; the Validator class is exercised the same way every other
    // endpoint's is, by its own metadata rather than a direct call.

    [Fact]
    public async Task Impose_UnknownAthlete_IsRefused()
    {
        var fx = await SeedAsync();

        var result = await ImposeAsync(fx, new ImposeSanction.Request(
            Guid.NewGuid(), fx.TeamId, MatchId: null, "Agresión a un rival",
            SanctionCompletionRule.Matches, MatchesRequired: 1, FineAmount: null));

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Impose_TeamFromAnotherCompetition_IsRefused()
    {
        var fx = await SeedAsync();
        var elsewhere = await SeedAsync();

        var result = await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, elsewhere.TeamId, MatchId: null, "Agresión a un rival",
            SanctionCompletionRule.Matches, MatchesRequired: 1, FineAmount: null));

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    // ---- Matches served, read off a real calendar -------------------------

    [Fact]
    public async Task Read_NoMatchesPlayedYet_IsActiveWithEveryMatchRemaining()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 2, null)));

        var response = await ReadAsync(fx, id);

        response.Status.Should().Be(SanctionStatus.Active);
        response.MatchesServed.Should().Be(0);
        response.MatchesRemaining.Should().Be(2);
        response.BlocksEntry.Should().BeTrue();
    }

    [Fact]
    public async Task Read_AMatchScheduledBeforeTheSanction_DoesNotCount()
    {
        var fx = await SeedAsync();
        await SeedMatchAsync(fx, MatchState.Finished, Clock.GetUtcNow().AddDays(-5));

        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        var response = await ReadAsync(fx, id);

        response.MatchesServed.Should().Be(0);
        response.Status.Should().Be(SanctionStatus.Active);
    }

    [Fact]
    public async Task Read_APostponedMatchAfterTheSanction_DoesNotCount()
    {
        var fx = await SeedAsync();
        await SeedMatchAsync(fx, MatchState.Postponed, Clock.GetUtcNow().AddDays(5));

        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        var response = await ReadAsync(fx, id);

        response.MatchesServed.Should().Be(0);
    }

    [Fact]
    public async Task Read_AWalkoverAfterTheSanction_Counts()
    {
        var fx = await SeedAsync();
        await SeedMatchAsync(fx, MatchState.Walkover, Clock.GetUtcNow().AddDays(5));

        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        var response = await ReadAsync(fx, id);

        response.MatchesServed.Should().Be(1);
        response.Status.Should().Be(SanctionStatus.Served);
        response.BlocksEntry.Should().BeFalse();
    }

    [Fact]
    public async Task Read_EnoughFinishedMatchesAfterTheSanction_IsServed()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 2, null)));

        await SeedMatchAsync(fx, MatchState.Finished, Clock.GetUtcNow().AddDays(1));
        await SeedMatchAsync(fx, MatchState.Finished, Clock.GetUtcNow().AddDays(8));

        var response = await ReadAsync(fx, id);

        response.MatchesServed.Should().Be(2);
        response.MatchesRemaining.Should().Be(0);
        response.Status.Should().Be(SanctionStatus.Served);
    }

    // ---- Fine ----------------------------------------------------------

    [Fact]
    public async Task Read_FineRule_UnpaidByDefault_IsActiveWithTheFullAmountOwed()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Insultos al árbitro", SanctionCompletionRule.Fine, null, 500m)));

        var response = await ReadAsync(fx, id);

        response.Status.Should().Be(SanctionStatus.Active);
        response.FineBalance.Should().Be(500m);
        response.MatchesServed.Should().BeNull();
    }

    [Fact]
    public async Task WaiveFine_OnAFineBearingSanction_ClearsTheBalance()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Insultos al árbitro", SanctionCompletionRule.Fine, null, 500m)));

        Assert.IsType<NoContent>(await WaiveFineAsync(fx, id, "Primera falta, se condona"));

        var response = await ReadAsync(fx, id);
        response.Status.Should().Be(SanctionStatus.Served);
        response.FineBalance.Should().Be(0m);
    }

    [Fact]
    public async Task WaiveFine_ClearingAnAbsentWaiver_LeavesTheFineOwed()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Insultos al árbitro", SanctionCompletionRule.Fine, null, 500m)));

        Assert.IsType<NoContent>(await WaiveFineAsync(fx, id, reason: null));

        var response = await ReadAsync(fx, id);
        response.Status.Should().Be(SanctionStatus.Active);
    }

    [Fact]
    public async Task WaiveFine_OnAMatchesOnlySanction_IsRefused()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        var result = await WaiveFineAsync(fx, id, "No corresponde");

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    // ---- Lifting and voiding outrank the terms ----------------------------

    [Fact]
    public async Task Lift_OverturnsTheSanctionEvenWithNothingServed()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 3, null)));

        Assert.IsType<NoContent>(await LiftAsync(fx, id, "La apelación tuvo lugar"));

        var response = await ReadAsync(fx, id);
        response.Status.Should().Be(SanctionStatus.Lifted);
        response.BlocksEntry.Should().BeFalse();
    }

    [Fact]
    public async Task Void_OutranksALiftAlreadyOnIt()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        Assert.IsType<NoContent>(await LiftAsync(fx, id, "La apelación tuvo lugar"));
        Assert.IsType<NoContent>(await VoidAsync(fx, id, "Se cargó contra la persona equivocada"));

        var response = await ReadAsync(fx, id);
        response.Status.Should().Be(SanctionStatus.Voided);
    }

    [Fact]
    public async Task Void_ClearingAnAbsentVoid_LeavesTheSanctionAsItWas()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        Assert.IsType<NoContent>(await VoidAsync(fx, id, reason: null));

        var response = await ReadAsync(fx, id);
        response.Status.Should().Be(SanctionStatus.Active);
    }

    // ---- Withholding the credential ---------------------------------------

    [Fact]
    public async Task WithholdCredential_RecordsTheFact_IndependentOfWhetherTheSanctionIsServed()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        Assert.IsType<NoContent>(await WithholdAsync(fx, id, withheld: true));

        var response = await ReadAsync(fx, id);
        response.CredentialWithheldAt.Should().NotBeNull();
        response.Status.Should().Be(SanctionStatus.Active); // withholding is not what resolves this
    }

    [Fact]
    public async Task WithholdCredential_Returning_ClearsTheFact()
    {
        var fx = await SeedAsync();
        var id = IdOf(await ImposeAsync(fx, new ImposeSanction.Request(
            fx.AthleteId, fx.TeamId, null, "Expulsión", SanctionCompletionRule.Matches, 1, null)));

        await WithholdAsync(fx, id, withheld: true);
        Assert.IsType<NoContent>(await WithholdAsync(fx, id, withheld: false));

        var response = await ReadAsync(fx, id);
        response.CredentialWithheldAt.Should().BeNull();
    }
}
