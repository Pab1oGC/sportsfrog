using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// RF-02's second axis: a membership scoped to one competition or one club,
/// rather than reaching its role across the whole organization. Two things
/// have to hold regardless of what application code does — a scope round
/// trips through the <c>membership_scope_type</c> enum unchanged, and the
/// database itself, not just EF's mapping, refuses a row that names both a
/// competition and a club or neither (<c>ck_membership_scope_target</c>) and
/// a membership scoped twice to the same target
/// (<c>uq_membership_scope_competition</c> / <c>uq_membership_scope_club</c>).
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class MembershipScopeTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(
        Guid OrgId, Guid MembershipId, Guid CompetitionId, Guid OtherCompetitionId, Guid ClubId, Guid CourseId);

    [Fact]
    public async Task A_competition_scope_round_trips_through_the_database_unchanged()
    {
        var fx = await SeedAsync();
        var scopeId = Guid.NewGuid();

        await using (var write = fixture.CreateAppContext())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, fx.OrgId);

            write.MembershipScopes.Add(new MembershipScope
            {
                Id = scopeId,
                OrgId = fx.OrgId,
                MembershipId = fx.MembershipId,
                ScopeType = ScopeType.Competition,
                CompetitionId = fx.CompetitionId,
            });
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // A fresh context, same reasoning as EnumRoundTripTests: proves
        // Postgres actually stored and re-parsed the enum, not EF's
        // first-level cache handing back the in-memory instance.
        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, fx.OrgId);

        var stored = await read.MembershipScopes.AsNoTracking().SingleAsync(s => s.Id == scopeId);

        stored.ScopeType.Should().Be(ScopeType.Competition);
        stored.CompetitionId.Should().Be(fx.CompetitionId);
        stored.ClubId.Should().BeNull();
    }

    [Fact]
    public async Task A_club_scope_round_trips_through_the_database_unchanged()
    {
        var fx = await SeedAsync();
        var scopeId = Guid.NewGuid();

        await using (var write = fixture.CreateAppContext())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, fx.OrgId);

            write.MembershipScopes.Add(new MembershipScope
            {
                Id = scopeId,
                OrgId = fx.OrgId,
                MembershipId = fx.MembershipId,
                ScopeType = ScopeType.Club,
                ClubId = fx.ClubId,
            });
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, fx.OrgId);

        var stored = await read.MembershipScopes.AsNoTracking().SingleAsync(s => s.Id == scopeId);

        stored.ScopeType.Should().Be(ScopeType.Club);
        stored.ClubId.Should().Be(fx.ClubId);
        stored.CompetitionId.Should().BeNull();
    }

    [Fact]
    public async Task A_course_scope_round_trips_through_the_database_unchanged()
    {
        var fx = await SeedAsync();
        var scopeId = Guid.NewGuid();

        await using (var write = fixture.CreateAppContext())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, fx.OrgId);

            write.MembershipScopes.Add(new MembershipScope
            {
                Id = scopeId,
                OrgId = fx.OrgId,
                MembershipId = fx.MembershipId,
                ScopeType = ScopeType.Course,
                CourseId = fx.CourseId,
            });
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, fx.OrgId);

        var stored = await read.MembershipScopes.AsNoTracking().SingleAsync(s => s.Id == scopeId);

        stored.ScopeType.Should().Be(ScopeType.Course);
        stored.CourseId.Should().Be(fx.CourseId);
        stored.CompetitionId.Should().BeNull();
        stored.ClubId.Should().BeNull();
    }

    [Fact]
    public async Task A_row_naming_both_a_course_and_a_competition_violates_the_target_check()
    {
        // The three-branch check added by AddCourseScopeColumn, exercised
        // the same way the pre-existing competition/club pair already is
        // below: scope_type names exactly one target, never two.
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Course,
            CourseId = fx.CourseId,
            CompetitionId = fx.CompetitionId,
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Contains("ck_membership_scope_target", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task Scoping_the_same_membership_twice_to_the_same_course_violates_the_unique_index()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Course,
            CourseId = fx.CourseId,
        });
        await context.SaveChangesAsync();

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Course,
            CourseId = fx.CourseId,
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("uq_membership_scope_course", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task A_row_naming_both_a_competition_and_a_club_violates_the_target_check()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = fx.CompetitionId,
            ClubId = fx.ClubId,
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Contains("ck_membership_scope_target", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task A_competition_scope_with_no_competition_id_violates_the_target_check()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = null,
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Contains("ck_membership_scope_target", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task Scoping_the_same_membership_twice_to_the_same_competition_violates_the_unique_index()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = fx.CompetitionId,
        });
        await context.SaveChangesAsync();

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = fx.CompetitionId,
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("uq_membership_scope_competition", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task Scoping_the_same_membership_to_two_different_competitions_is_allowed()
    {
        // A membership that must reach several competitions gets several
        // rows, not one row widened to cover them (see the migration's own
        // remarks) — proving that is allowed is as important as proving a
        // duplicate is not.
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = fx.CompetitionId,
        });
        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            MembershipId = fx.MembershipId,
            ScopeType = ScopeType.Competition,
            CompetitionId = fx.OtherCompetitionId,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, fx.OrgId);

        var count = await read.MembershipScopes.CountAsync(s => s.MembershipId == fx.MembershipId);
        count.Should().Be(2);
    }

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var otherCompetitionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        // organizations and users carry no isolation policy, same note as
        // EnumRoundTripTests.
        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        setup.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@test.sportfrog.local",
            PasswordHash = "irrelevant-for-this-test",
            FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Memberships.Add(new OrganizationMembership
        {
            Id = membershipId,
            OrgId = orgId,
            UserId = userId,
            Role = MembershipRole.Operator,
        });

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club de Prueba" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "football",
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
            SportCode = "football",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });
        setup.Competitions.Add(new Competition
        {
            Id = otherCompetitionId,
            OrgId = orgId,
            SportCode = "football",
            RulesetId = rulesetId,
            Name = "Otra competencia",
            Slug = $"comp-{otherCompetitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Courses.Add(new Course
        {
            Id = courseId,
            OrgId = orgId,
            SportCode = "football",
            Name = "Curso de Prueba",
            MonthlyFeeBob = 100m,
            StartsOn = new DateOnly(2026, 3, 1),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, membershipId, competitionId, otherCompetitionId, clubId, courseId);
    }
}
