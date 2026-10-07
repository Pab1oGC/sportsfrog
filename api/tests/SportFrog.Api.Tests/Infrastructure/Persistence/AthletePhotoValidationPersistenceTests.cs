using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// The photo-validation columns on <c>athletes</c>: that a verdict written
/// through EF comes back unchanged, including the enum and the two JSON lists,
/// and that an athlete without any verdict reads as not evaluated.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AthletePhotoValidationPersistenceTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateTimeOffset ValidatedAt = new(2026, 10, 7, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_rejected_verdict_round_trips_through_the_database_unchanged()
    {
        var organizationId = await SeedOrganizationAsync();
        var athleteId = Guid.NewGuid();

        await using (var write = fixture.CreateAppContext())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, organizationId);

            var athlete = NewAthlete(organizationId, athleteId);
            athlete.PhotoKey = $"orgs/{organizationId:N}/athletes/{athleteId:N}.jpg";
            athlete.PhotoValidationState = PhotoValidationState.Rejected;
            athlete.PhotoValidationReasons = ["Se ven lentes de sol. Subí una foto sin lentes de sol."];
            athlete.PhotoValidationWarnings = ["No verificado: boca cerrada."];
            athlete.PhotoRulesVersion = "0.1.0";
            athlete.PhotoValidatedAt = ValidatedAt;

            write.Athletes.Add(athlete);
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, organizationId);

        var stored = await read.Athletes.AsNoTracking().SingleAsync(a => a.Id == athleteId);

        stored.PhotoValidationState.Should().Be(PhotoValidationState.Rejected);
        stored.PhotoValidationReasons.Should().Equal("Se ven lentes de sol. Subí una foto sin lentes de sol.");
        stored.PhotoValidationWarnings.Should().Equal("No verificado: boca cerrada.");
        stored.PhotoRulesVersion.Should().Be("0.1.0");
        stored.PhotoValidatedAt.Should().Be(ValidatedAt);
    }

    [Fact]
    public async Task An_athlete_written_without_a_verdict_reads_back_as_not_evaluated()
    {
        // The database default is the truth for a row nobody has assessed:
        // not_evaluated, empty lists, no version, no date.
        var organizationId = await SeedOrganizationAsync();
        var athleteId = Guid.NewGuid();

        await using (var write = fixture.CreateAppContext())
        {
            await using var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, organizationId);

            write.Athletes.Add(NewAthlete(organizationId, athleteId));
            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var read = fixture.CreateAppContext();
        await using var readTransaction = await read.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(read, organizationId);

        var stored = await read.Athletes.AsNoTracking().SingleAsync(a => a.Id == athleteId);

        stored.PhotoValidationState.Should().Be(PhotoValidationState.NotEvaluated);
        stored.PhotoValidationReasons.Should().BeEmpty();
        stored.PhotoValidationWarnings.Should().BeEmpty();
        stored.PhotoRulesVersion.Should().BeNull();
        stored.PhotoValidatedAt.Should().BeNull();
    }

    private async Task<Guid> SeedOrganizationAsync()
    {
        var organizationId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = $"Org {organizationId:N}",
            Slug = $"org-{organizationId:N}",
        });
        await setup.SaveChangesAsync();

        return organizationId;
    }

    private static Athlete NewAthlete(Guid organizationId, Guid athleteId) => new()
    {
        Id = athleteId,
        OrgId = organizationId,
        FirstName = "Ana",
        LastName = "Pérez",
        DocumentId = $"doc-{athleteId:N}",
    };
}
