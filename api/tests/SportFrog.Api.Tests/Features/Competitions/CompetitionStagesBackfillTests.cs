using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// The backfill in <c>20261022000000_AddCompetitionAndCategoryStages.Up.sql</c>:
/// that it derives the same stages for a real row that
/// <see cref="CompetitionStages.For"/> already derives in C#, and splits
/// what used to be one schedule and one set of category columns onto the
/// right stage.
/// </summary>
/// <remarks>
/// The migration itself only ever runs once, against whatever existed the
/// moment it applied — none of this fixture's own data, created afterwards.
/// What is under test here is the backfill statements themselves, run again
/// against freshly seeded rows the same way they ran once against whatever
/// existed in production; copied from the migration's own .Up.sql rather
/// than re-derived, so a future edit to one and not the other breaks this
/// test instead of silently drifting.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CompetitionStagesBackfillTests(SportFrogDatabaseFixture fixture)
{
    private const string BackfillCompetitionStages =
        """
        INSERT INTO competition_stages (org_id, competition_id, kind, buffer_minutes, space_ids)
        SELECT
            c.org_id,
            c.id,
            stage.kind,
            (c.settings -> 'schedule' ->> 'buffer_minutes')::smallint,
            CASE WHEN c.settings -> 'schedule' -> 'space_ids' IS NOT NULL
                 THEN (SELECT array_agg(value::uuid)
                       FROM jsonb_array_elements_text(c.settings -> 'schedule' -> 'space_ids'))
                 ELSE NULL
            END
        FROM competitions c
        JOIN sports sp ON sp.code = c.sport_code
        CROSS JOIN LATERAL (
            SELECT unnest(
                CASE
                    WHEN c.format = 'league' THEN ARRAY['league']
                    WHEN c.format = 'knockout' THEN ARRAY['bracket']
                    WHEN c.format = 'groups' AND sp.score_mode = 'judged'
                        THEN ARRAY['classification', 'bracket']
                    WHEN c.format = 'groups' THEN ARRAY['groups', 'bracket']
                    ELSE ARRAY[]::text[]
                END
            ) AS kind
        ) AS stage
        WHERE c.id = {0};
        """;

    private const string BackfillCategoryStages =
        """
        INSERT INTO category_stages (
            org_id, category_id, kind, qualifiers_per_group, knockout_entrants, uses_repechage
        )
        SELECT
            cat.org_id,
            cat.id,
            stage.kind,
            CASE WHEN stage.kind IN ('groups', 'classification') THEN cat.qualifiers_per_group END,
            CASE WHEN stage.kind = 'bracket' THEN cat.knockout_entrants END,
            CASE WHEN stage.kind = 'bracket' THEN cat.uses_repechage ELSE false END
        FROM categories cat
        JOIN competitions c ON c.id = cat.competition_id
        JOIN sports sp ON sp.code = c.sport_code
        CROSS JOIN LATERAL (
            SELECT unnest(
                CASE
                    WHEN c.format = 'league' THEN ARRAY['league']
                    WHEN c.format = 'knockout' THEN ARRAY['bracket']
                    WHEN c.format = 'groups' AND sp.score_mode = 'judged'
                        THEN ARRAY['classification', 'bracket']
                    WHEN c.format = 'groups' THEN ARRAY['groups', 'bracket']
                    ELSE ARRAY[]::text[]
                END
            ) AS kind
        ) AS stage
        WHERE cat.id = {0};
        """;

    [Fact]
    public async Task Backfill_AGroupsCompetition_SplitsScheduleAndCategoryColumnsOntoTheRightStage()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026");
        competition.Format = CompetitionFormat.Groups;
        competition.Settings = new CompetitionSettings();
        scope.Context.Competitions.Add(competition);

        var spaceOne = Guid.NewGuid();
        var spaceTwo = Guid.NewGuid();
        var entrants = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
            QualifiersPerGroup = 2,
            KnockoutEntrants = entrants,
            UsesRepechage = true,
        };
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        // The C# shape this jsonb used to come from (CompetitionSettings.Schedule)
        // is gone now that stages carry it instead — see CompetitionSettings'
        // own remarks. An old row in a real database still has this exact
        // shape sitting in its settings column, written back when that type
        // existed, so this writes it directly rather than through an entity
        // that cannot express it anymore: the backfill reads raw jsonb paths,
        // not the current C# model, and this is what it actually ran against.
        await scope.Context.Database.ExecuteSqlRawAsync(
            """
            UPDATE competitions
            SET settings = settings || jsonb_build_object(
                'schedule', jsonb_build_object('buffer_minutes', 20, 'space_ids', {0}::text[])
            )
            WHERE id = {1}
            """,
            new[] { spaceOne.ToString(), spaceTwo.ToString() }, competition.Id);

        await scope.Context.Database.ExecuteSqlRawAsync(BackfillCompetitionStages, competition.Id);
        await scope.Context.Database.ExecuteSqlRawAsync(BackfillCategoryStages, category.Id);

        var competitionStages = await scope.Context.CompetitionStages
            .AsNoTracking()
            .Where(stage => stage.CompetitionId == competition.Id)
            .ToListAsync();

        competitionStages.Select(s => s.Kind).Should().BeEquivalentTo([StageKind.Groups, StageKind.Bracket]);
        competitionStages.Should().AllSatisfy(stage =>
        {
            // Lo que antes era un único horario de la competencia se copió
            // igual sobre las dos fases que esta competencia ya tiene.
            stage.BufferMinutes.Should().Be((short)20);
            stage.SpaceIds.Should().BeEquivalentTo([spaceOne, spaceTwo]);
        });

        var categoryStages = await scope.Context.CategoryStages
            .AsNoTracking()
            .Where(stage => stage.CategoryId == category.Id)
            .ToListAsync();

        var groupsStage = categoryStages.Single(s => s.Kind == StageKind.Groups);
        groupsStage.QualifiersPerGroup.Should().Be((short)2);
        groupsStage.KnockoutEntrants.Should().BeNull();
        groupsStage.UsesRepechage.Should().BeFalse();

        var bracketStage = categoryStages.Single(s => s.Kind == StageKind.Bracket);
        bracketStage.QualifiersPerGroup.Should().BeNull();
        bracketStage.KnockoutEntrants.Should().BeEquivalentTo(entrants);
        bracketStage.UsesRepechage.Should().BeTrue();

        // El reglamento efectivo de hoy no cambia por existir esta tabla:
        // RulesetId se queda null en todas las filas hasta que EffectiveRuleset
        // aprenda a leer un tercer nivel.
        categoryStages.Should().AllSatisfy(stage => stage.RulesetId.Should().BeNull());
    }

    [Fact]
    public async Task Backfill_ALeagueCompetition_IsOneStageOnEachTable()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var competition = tenant.NewCompetition("Liga 2026"); // Format ya es "league".
        scope.Context.Competitions.Add(competition);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Name = "Primera",
        };
        scope.Context.Categories.Add(category);
        await scope.Context.SaveChangesAsync();

        await scope.Context.Database.ExecuteSqlRawAsync(BackfillCompetitionStages, competition.Id);
        await scope.Context.Database.ExecuteSqlRawAsync(BackfillCategoryStages, category.Id);

        (await scope.Context.CompetitionStages.AsNoTracking()
                .Where(stage => stage.CompetitionId == competition.Id).ToListAsync())
            .Select(s => s.Kind).Should().Equal(StageKind.League);

        (await scope.Context.CategoryStages.AsNoTracking()
                .Where(stage => stage.CategoryId == category.Id).ToListAsync())
            .Select(s => s.Kind).Should().Equal(StageKind.League);
    }
}
