using AwesomeAssertions;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Public;

/// <summary>
/// <see cref="ReadPublicCalendar.AthletePhotoKeysAsync"/> is the one place a
/// competitor's photo — private everywhere else in this system (RNF-16) —
/// is allowed to leave the organization at all, and only under three
/// conditions at once: the organization opted in, the sport is individual,
/// and the team fielding that photo is exactly one athlete. Getting any one
/// of those wrong publishes something this codebase has otherwise gone out
/// of its way to keep private, so each condition gets its own guard here
/// rather than trusting the combination to work by construction.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ReadPublicCalendarAthletePhotoKeysTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Seeded(Guid OrgId, Guid CompetitionId, List<Guid> TeamIds);

    /// <param name="sportCode">"taekwondo_kyorugi" for individual, "football" for a team sport.</param>
    /// <param name="showAthletePhotos">The organization's own switch.</param>
    /// <param name="entriesPerTeam">
    /// One team is created per entry in this list, with that many active
    /// roster entries (athletes) each — a pair or trio all sharing one team,
    /// same shape <c>EnrollIndividual</c> already produces for poomsae.
    /// </param>
    private async Task<Seeded> SeedAsync(string sportCode, bool showAthletePhotos, params int[] entriesPerTeam)
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club de Prueba" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = sportCode,
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = sportCode,
            RulesetId = rulesetId,
            Name = "Copa de Prueba",
            Slug = $"copa-{competitionId:N}",
            Season = "2026",
            Format = CompetitionFormat.Knockout,
            Settings = new CompetitionSettings { Public = new PublicSettings { ShowAthletePhotos = showAthletePhotos } },
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Absoluto" });

        var teamIds = new List<Guid>();

        foreach (var athleteCount in entriesPerTeam)
        {
            var teamId = Guid.NewGuid();
            teamIds.Add(teamId);

            setup.Teams.Add(new Team
            {
                Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId,
                Name = $"Equipo {teamId:N}", IsIndividual = sportCode.StartsWith("taekwondo"),
            });

            for (var i = 0; i < athleteCount; i++)
            {
                var athleteId = Guid.NewGuid();

                setup.Athletes.Add(new Athlete
                {
                    Id = athleteId, OrgId = orgId,
                    FirstName = "Atleta", LastName = $"{i}", DocumentId = $"doc-{athleteId:N}",
                    PhotoKey = $"orgs/{orgId:N}/athletes/{athleteId:N}.jpg",
                });

                setup.RosterEntries.Add(new RosterEntry
                {
                    Id = Guid.NewGuid(), OrgId = orgId, TeamId = teamId, AthleteId = athleteId,
                    RegisteredAt = DateTimeOffset.UtcNow,
                });
            }
        }

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Seeded(orgId, competitionId, teamIds);
    }

    [Fact]
    public async Task ReturnsNothing_WhenTheSwitchIsOff()
    {
        var fx = await SeedAsync("taekwondo_kyorugi", showAthletePhotos: false, 1, 1);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPublicCalendar.AthletePhotoKeysAsync(context, fx.CompetitionId, fx.TeamIds, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsThePhoto_ForAnIndividualSportTeamOfExactlyOneAthlete()
    {
        var fx = await SeedAsync("taekwondo_kyorugi", showAthletePhotos: true, 1);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPublicCalendar.AthletePhotoKeysAsync(context, fx.CompetitionId, fx.TeamIds, CancellationToken.None);

        result.Should().ContainKey(fx.TeamIds[0]);
        result[fx.TeamIds[0]].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsNothing_ForAPoomsaePairSharingOneTeam()
    {
        // Dos atletas, un solo equipo -- IndividualTeamName ya arma "Fulano /
        // Mengano" para este mismo caso. Ninguna de las dos fotos representa
        // al par, así que no se ofrece ninguna en vez de elegir una al azar.
        var fx = await SeedAsync("taekwondo_poomsae", showAthletePhotos: true, 2);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPublicCalendar.AthletePhotoKeysAsync(context, fx.CompetitionId, fx.TeamIds, CancellationToken.None);

        result.Should().NotContainKey(fx.TeamIds[0]);
    }

    [Fact]
    public async Task ReturnsNothing_ForATeamSport_EvenWithTheSwitchOn()
    {
        var fx = await SeedAsync("football", showAthletePhotos: true, 1);
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPublicCalendar.AthletePhotoKeysAsync(context, fx.CompetitionId, fx.TeamIds, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
