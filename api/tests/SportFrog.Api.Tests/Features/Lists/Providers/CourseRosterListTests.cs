using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="CourseRosterList"/> actually reads a course's enrollments — a query
/// that compiles can still fail the moment EF Core tries to translate it, which only shows up
/// against a real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CourseRosterListTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private sealed record Fixture(Guid OrgId, Guid CourseId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// One course, one active student with a scholarship fee override, and
    /// one withdrawn student paying the course's own price — enough to pin
    /// both the "active first" ordering and which fee each row actually
    /// shows.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var activeAthleteId = Guid.NewGuid();
        var withdrawnAthleteId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Courses.Add(new Course
        {
            Id = courseId, OrgId = orgId, SportCode = Football, Name = "Fútbol infantil",
            MonthlyFeeBob = 150m, StartsOn = new DateOnly(2026, 1, 1),
        });

        setup.Athletes.Add(new Athlete
        {
            Id = activeAthleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{activeAthleteId:N}", BirthDate = new DateOnly(2015, 1, 1),
        });
        setup.Athletes.Add(new Athlete
        {
            Id = withdrawnAthleteId, OrgId = orgId, FirstName = "Ana", LastName = "Soto",
            DocumentId = $"doc-{withdrawnAthleteId:N}", BirthDate = new DateOnly(2014, 6, 1),
        });

        await setup.SaveChangesAsync();

        setup.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = Guid.NewGuid(), OrgId = orgId, CourseId = courseId, AthleteId = activeAthleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            FeeOverrideBob = 0m, FeeOverrideReason = "Beca deportiva",
        });
        setup.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = Guid.NewGuid(), OrgId = orgId, CourseId = courseId, AthleteId = withdrawnAthleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            WithdrawnAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, courseId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_UnknownCourse_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = Guid.NewGuid().ToString() }),
            session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NoCourseIdInScope_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(ListScope.Empty, session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownCourse_TitleNamesTheCourse()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Title.Should().Be("Plantel — Fútbol infantil");
    }

    [Fact]
    public async Task LoadAsync_ActiveStudentsComeBeforeWithdrawnOnes()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        var rows = table!.Sections.Single().Rows;
        rows.Should().HaveCount(2);

        var active = rows[0];
        active[1].Should().Be("Diaz, Juan");
        active[4].Should().Be("Activo");

        var withdrawn = rows[1];
        withdrawn[1].Should().Be("Soto, Ana");
        withdrawn[4].Should().Be("Retirado");
    }

    [Fact]
    public async Task LoadAsync_FeeOverride_ShowsTheOverrideNotTheCoursePrice()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Single(row => row[1]!.Equals("Diaz, Juan"))[3].Should().Be(0m);
    }

    [Fact]
    public async Task LoadAsync_NoFeeOverride_ShowsTheCoursesOwnFee()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new CourseRosterList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["courseId"] = fx.CourseId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Sections.Single().Rows.Single(row => row[1]!.Equals("Soto, Ana"))[3].Should().Be(150m);
    }
}
