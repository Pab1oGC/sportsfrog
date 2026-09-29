using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Competitions;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// "One living competition per name, per organization, ignoring case":
/// <c>uq_competitions_name</c>, and the pre-check and race answer in
/// <see cref="CompetitionUniqueness"/> that agree with it. The index is what
/// holds under a race; the pre-check is the ordinary case, so both halves are
/// pinned to the same rule.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CompetitionNameUniquenessTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task SameName_SameOrganization_ViolatesTheUniqueIndex()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));
        await context.SaveChangesAsync();

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("uq_competitions_name", postgres.ConstraintName);
    }

    [Fact]
    public async Task SameName_DifferentCase_ViolatesTheUniqueIndex()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));
        await context.SaveChangesAsync();

        context.Competitions.Add(tenant.NewCompetition("LA LIGA 25/26"));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal("uq_competitions_name", postgres.ConstraintName);
    }

    [Fact]
    public async Task SameName_DifferentOrganization_IsAllowed()
    {
        var first = await CompetitionTenant.SeedAsync(fixture);
        var second = await CompetitionTenant.SeedAsync(fixture);

        await using (var firstScope = await first.OpenAsync(fixture))
        {
            var firstContext = firstScope.Context;
            firstContext.Competitions.Add(first.NewCompetition("La Liga 25/26"));
            await firstContext.SaveChangesAsync();
        }

        await using (var secondScope = await second.OpenAsync(fixture))
        {
            var secondContext = secondScope.Context;
            secondContext.Competitions.Add(second.NewCompetition("La Liga 25/26"));

            Assert.Equal(1, await secondContext.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task NameOfASoftDeletedCompetition_CanBeTakenAgain()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        var withdrawn = tenant.NewCompetition("La Liga 25/26");
        context.Competitions.Add(withdrawn);
        await context.SaveChangesAsync();

        withdrawn.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));

        Assert.Equal(1, await context.SaveChangesAsync());
    }

    [Fact]
    public async Task IsNameTaken_IgnoresCaseAndSurroundingSpaces()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));
        await context.SaveChangesAsync();

        Assert.True(await CompetitionUniqueness.IsNameTakenAsync(
            context, "  la liga 25/26 ", except: null, CancellationToken.None));
        Assert.False(await CompetitionUniqueness.IsNameTakenAsync(
            context, "La Liga 26/27", except: null, CancellationToken.None));
    }

    [Fact]
    public async Task IsNameTaken_DoesNotCountTheCompetitionBeingCorrected()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        var own = tenant.NewCompetition("La Liga 25/26");
        context.Competitions.Add(own);
        await context.SaveChangesAsync();

        Assert.False(await CompetitionUniqueness.IsNameTakenAsync(
            context, "La Liga 25/26", except: own.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ConflictFor_NameViolation_AnswersWithTheNameMessage()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));
        await context.SaveChangesAsync();
        context.Competitions.Add(tenant.NewCompetition("La Liga 25/26"));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertConflict(CompetitionUniqueness.ConflictFor(thrown), "con ese nombre");
    }

    [Fact]
    public async Task ConflictFor_SlugViolation_AnswersWithTheAddressMessage()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var context = scope.Context;

        context.Competitions.Add(tenant.NewCompetition("Una", slug: "misma-direccion"));
        await context.SaveChangesAsync();
        context.Competitions.Add(tenant.NewCompetition("Otra", slug: "misma-direccion"));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        AssertConflict(CompetitionUniqueness.ConflictFor(thrown), "esa dirección");
    }

    [Fact]
    public void ConflictFor_AFailureThatIsNotAUniqueViolation_IsNotItsToExplain()
    {
        var unrelated = new DbUpdateException("boom", new InvalidOperationException());

        Assert.Null(CompetitionUniqueness.ConflictFor(unrelated));
    }

    private static void AssertConflict(IResult? result, string expectedDetail)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Contains(expectedDetail, problem.ProblemDetails.Detail);
    }
}
