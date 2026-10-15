using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportFrog.Api.Features.Courses;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Courses;

/// <summary>
/// End to end against a real migrated database — not because a course is
/// complicated, but because the two things actually at risk here only show
/// up against the real thing: whether <see cref="ReadCourses"/>' nested
/// projection of <see cref="WeeklySlot"/> out of a child collection
/// translates to SQL at all, and whether two organizations' courses stay
/// apart under row-level security the same way every other table's do.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CoursesTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static OrganizationContext OrganizationFor(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Admin, userId: Guid.NewGuid(), ipAddress: null);
        return organization;
    }

    private async Task<Guid> SeedOrganizationAsync()
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        return orgId;
    }

    private static CourseContract Contract(
        string name,
        IReadOnlyList<WeeklySlot>? schedules = null,
        Guid? venueSpaceId = null,
        Guid? coachMembershipId = null) =>
        new(
            SportCode: Football,
            Name: name,
            VenueSpaceId: venueSpaceId,
            CoachMembershipId: coachMembershipId,
            Capacity: 20,
            MonthlyFeeBob: 150m,
            StartsOn: new DateOnly(2026, 3, 1),
            EndsOn: null,
            IsActive: true,
            Schedules: schedules ?? [new WeeklySlot(DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(19, 0))]);

    private async Task<Guid> CreateAsync(Guid orgId, CourseContract contract)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await CreateCourse.HandleAsync(contract, context, OrganizationFor(orgId), CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.IsType<Created<CreateCourse.Response>>(result).Value!.Id;
    }

    private async Task<IResult> CreateRawAsync(Guid orgId, CourseContract contract)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await CreateCourse.HandleAsync(contract, context, OrganizationFor(orgId), CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<ReadCourses.Summary?> ReadAsync(Guid orgId, Guid id)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadCourses.ReadAsync(id, context, CancellationToken.None);

        return result switch
        {
            Ok<ReadCourses.Summary> ok => ok.Value,
            NotFound => null,
            _ => throw new InvalidOperationException($"Unexpected result: {result.GetType()}"),
        };
    }

    private async Task<List<ReadCourses.Summary>> ListAsync(Guid orgId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadCourses.ListAsync(context, CancellationToken.None);

        return Assert.IsType<Ok<List<ReadCourses.Summary>>>(result).Value!;
    }

    [Fact]
    public async Task CreateThenRead_RoundTripsTheWholeWeeklySchedule()
    {
        var orgId = await SeedOrganizationAsync();

        var schedules = new WeeklySlot[]
        {
            new(DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(19, 0)),
            new(DayOfWeek.Thursday, new TimeOnly(18, 0), new TimeOnly(19, 0)),
        };

        var id = await CreateAsync(orgId, Contract("Fútbol infantil", schedules));

        var course = await ReadAsync(orgId, id);

        course.Should().NotBeNull();
        course!.Name.Should().Be("Fútbol infantil");
        course.SportName.Should().Be("Fútbol");
        course.Schedules.Should().Equal(schedules);
    }

    [Fact]
    public async Task CreateThenRead_TwoSlotsOnTheSameDay_BothSurvive()
    {
        // A morning and an evening group — accepted on purpose, see
        // WeeklySlot's own remarks.
        var orgId = await SeedOrganizationAsync();

        var schedules = new WeeklySlot[]
        {
            new(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(9, 0)),
            new(DayOfWeek.Saturday, new TimeOnly(18, 0), new TimeOnly(19, 0)),
        };

        var id = await CreateAsync(orgId, Contract("Natación sábados", schedules));
        var course = await ReadAsync(orgId, id);

        course!.Schedules.Should().HaveCount(2);
        course.Schedules.Select(s => s.StartsAt).Should().Equal(new TimeOnly(8, 0), new TimeOnly(18, 0));
    }

    [Fact]
    public async Task Update_ReplacesTheWholeSchedule_OldSlotsAreGone()
    {
        var orgId = await SeedOrganizationAsync();
        var id = await CreateAsync(
            orgId,
            Contract("Básquet", [new WeeklySlot(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(18, 0))]));

        var newSchedule = new WeeklySlot[]
        {
            new(DayOfWeek.Wednesday, new TimeOnly(16, 0), new TimeOnly(17, 30)),
            new(DayOfWeek.Friday, new TimeOnly(16, 0), new TimeOnly(17, 30)),
        };

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var updateResult = await UpdateCourse.HandleAsync(
            id, Contract("Básquet", newSchedule), context, CancellationToken.None);
        await transaction.CommitAsync();

        Assert.IsType<NoContent>(updateResult);

        var course = await ReadAsync(orgId, id);
        course!.Schedules.Should().Equal(newSchedule);
    }

    [Fact]
    public async Task Create_UnknownSportCode_ReturnsBadRequestAndNothingIsPersisted()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await CreateRawAsync(orgId, Contract("Curso fantasma") with { SportCode = "no-existe" });

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await ListAsync(orgId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownVenueSpace_ReturnsBadRequest()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await CreateRawAsync(orgId, Contract("Curso sin cancha", venueSpaceId: Guid.NewGuid()));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateNameInTheSameOrganization_ReturnsConflict()
    {
        var orgId = await SeedOrganizationAsync();
        await CreateAsync(orgId, Contract("Vóley juvenil"));

        var result = await CreateRawAsync(orgId, Contract("Vóley juvenil"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Create_SameNameInDifferentOrganizations_BothSucceed()
    {
        // The uniqueness is per organization, not platform-wide — two
        // schools naming their course the same thing is not a collision.
        var orgA = await SeedOrganizationAsync();
        var orgB = await SeedOrganizationAsync();

        await CreateAsync(orgA, Contract("Escuelita de verano"));
        await CreateAsync(orgB, Contract("Escuelita de verano"));

        (await ListAsync(orgA)).Should().ContainSingle();
        (await ListAsync(orgB)).Should().ContainSingle();
    }

    [Fact]
    public async Task List_AnotherOrganizationsCourse_NeverAppears()
    {
        var orgA = await SeedOrganizationAsync();
        var orgB = await SeedOrganizationAsync();

        await CreateAsync(orgA, Contract("Curso de A"));
        await CreateAsync(orgB, Contract("Curso de B"));

        (await ListAsync(orgA)).Select(c => c.Name).Should().Equal("Curso de A");
        (await ListAsync(orgB)).Select(c => c.Name).Should().Equal("Curso de B");
    }

    [Fact]
    public async Task Delete_SoftDeletes_CourseStopsAppearingButRowSurvives()
    {
        var orgId = await SeedOrganizationAsync();
        var id = await CreateAsync(orgId, Contract("Curso a borrar"));

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var deleteResult = await DeleteCourse.HandleAsync(id, context, TimeProvider.System, CancellationToken.None);
        await transaction.CommitAsync();

        Assert.IsType<NoContent>(deleteResult);

        (await ReadAsync(orgId, id)).Should().BeNull();
        (await ListAsync(orgId)).Should().BeEmpty();
    }
}
