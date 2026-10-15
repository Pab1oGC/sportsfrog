using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SportFrog.Api.Features.Courses;
using SportFrog.Api.Features.Courses.Enrollments;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Courses.Enrollments;

/// <summary>
/// Enrolling a student (one at a time or several at once), withdrawing and
/// reinstating them, and the one rule that ties enrolling and reinstating
/// together: a withdrawn enrollment still blocks a fresh one for the same
/// course and athlete — see <see cref="EnrollmentPolicy"/>'s own remarks
/// for why, and <see cref="StrikeEnrollment"/> for the one way out of it.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CourseEnrollmentsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static OrganizationContext OrganizationFor(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, userId: Guid.NewGuid(), ipAddress: null);
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

    private async Task<Guid> SeedAthleteAsync(Guid orgId, string firstName, bool isActive = true)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            FirstName = firstName,
            LastName = "Apellido",
            DocumentId = Guid.NewGuid().ToString("N")[..10],
            BirthDate = new DateOnly(2015, 1, 1),
            IsActive = isActive,
        };
        context.Athletes.Add(athlete);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return athlete.Id;
    }

    private async Task<Guid> SeedCourseAsync(Guid orgId, string name, short? capacity = null, bool isActive = true)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var contract = new CourseContract(
            SportCode: Football,
            Name: name,
            VenueSpaceId: null,
            CoachMembershipId: null,
            Capacity: capacity,
            MonthlyFeeBob: 100m,
            StartsOn: new DateOnly(2026, 3, 1),
            EndsOn: null,
            IsActive: isActive,
            Schedules: [new WeeklySlot(DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(19, 0))]);

        var result = await CreateCourse.HandleAsync(contract, context, OrganizationFor(orgId), CancellationToken.None);
        await transaction.CommitAsync();

        return Assert.IsType<Created<CreateCourse.Response>>(result).Value!.Id;
    }

    private async Task<IResult> EnrollAsync(Guid orgId, Guid courseId, Guid athleteId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await EnrollStudent.HandleAsync(
            courseId,
            new EnrollStudent.Request(athleteId),
            context,
            new EnrollmentPolicy(context),
            OrganizationFor(orgId),
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<Guid> EnrollSuccessfullyAsync(Guid orgId, Guid courseId, Guid athleteId) =>
        Assert.IsType<Created<EnrollStudent.Response>>(
            await EnrollAsync(orgId, courseId, athleteId)).Value!.Id;

    private async Task<IResult> EnrollBulkAsync(Guid orgId, Guid courseId, IReadOnlyList<Guid> athleteIds)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await EnrollStudentsBulk.HandleAsync(
            courseId,
            new EnrollStudentsBulk.Request(athleteIds),
            context,
            new EnrollmentPolicy(context),
            OrganizationFor(orgId),
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> WithdrawAsync(Guid orgId, Guid enrollmentId, bool withdrawn)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await WithdrawEnrollment.HandleAsync(
            enrollmentId,
            new WithdrawEnrollment.Request(withdrawn),
            context,
            new EnrollmentPolicy(context),
            TimeProvider.System,
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> StrikeAsync(Guid orgId, Guid enrollmentId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await StrikeEnrollment.HandleAsync(enrollmentId, context, TimeProvider.System, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<List<ReadEnrollments.Summary>> ListAsync(Guid orgId, Guid courseId, bool active = false)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadEnrollments.ListAsync(courseId, context, CancellationToken.None, active);

        return Assert.IsType<Ok<List<ReadEnrollments.Summary>>>(result).Value!;
    }

    [Fact]
    public async Task Enroll_NewStudent_Succeeds()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Fútbol infantil");
        var athleteId = await SeedAthleteAsync(orgId, "Ana");

        await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        var listing = await ListAsync(orgId, courseId);
        listing.Should().ContainSingle(entry => entry.AthleteId == athleteId && entry.WithdrawnAt == null);
    }

    [Fact]
    public async Task Enroll_SameStudentTwice_IsRefusedWithAFriendlyMessageNotARawConflict()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Natación");
        var athleteId = await SeedAthleteAsync(orgId, "Beto");

        await EnrollSuccessfullyAsync(orgId, courseId, athleteId);
        var second = await EnrollAsync(orgId, courseId, athleteId);

        var problem = Assert.IsType<ProblemHttpResult>(second);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails).Errors.Should().ContainKey("AthleteId");
    }

    [Fact]
    public async Task Enroll_InactiveAthlete_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Básquet");
        var athleteId = await SeedAthleteAsync(orgId, "Caro", isActive: false);

        var result = await EnrollAsync(orgId, courseId, athleteId);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Enroll_UnknownAthlete_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Vóley");

        var result = await EnrollAsync(orgId, courseId, Guid.NewGuid());

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Enroll_DeactivatedCourse_ReturnsConflict()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Curso pausado", isActive: false);
        var athleteId = await SeedAthleteAsync(orgId, "Dana");

        var result = await EnrollAsync(orgId, courseId, athleteId);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Enroll_CourseAtCapacity_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Curso con cupo uno", capacity: 1);
        var first = await SeedAthleteAsync(orgId, "Eva");
        var second = await SeedAthleteAsync(orgId, "Fer");

        await EnrollSuccessfullyAsync(orgId, courseId, first);
        var result = await EnrollAsync(orgId, courseId, second);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EnrollBulk_SeveralNewStudents_EnrollsAllOfThem()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Natación grupal");
        var first = await SeedAthleteAsync(orgId, "Nico");
        var second = await SeedAthleteAsync(orgId, "Olga");
        var third = await SeedAthleteAsync(orgId, "Pablo");

        var result = await EnrollBulkAsync(orgId, courseId, [first, second, third]);

        var response = Assert.IsType<Created<EnrollStudentsBulk.Response>>(result).Value!;
        response.EnrollmentIds.Should().HaveCount(3);

        var listing = await ListAsync(orgId, courseId);
        listing.Select(entry => entry.AthleteId).Should().BeEquivalentTo([first, second, third]);
    }

    [Fact]
    public async Task EnrollBulk_OneAthleteAlreadyEnrolled_RefusesTheWholeBatch()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Básquet grupal");
        var already = await SeedAthleteAsync(orgId, "Quique");
        var fresh = await SeedAthleteAsync(orgId, "Rosa");
        await EnrollSuccessfullyAsync(orgId, courseId, already);

        var result = await EnrollBulkAsync(orgId, courseId, [fresh, already]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails).Errors.Should().ContainKey("AthleteIds[1]");

        // All or nothing: the fresh athlete named first in the batch is not
        // left enrolled just because only the second one had a problem.
        (await ListAsync(orgId, courseId)).Should().ContainSingle(entry => entry.AthleteId == already);
    }

    [Fact]
    public async Task EnrollBulk_UnknownAthlete_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Vóley grupal");
        var known = await SeedAthleteAsync(orgId, "Sole");

        var result = await EnrollBulkAsync(orgId, courseId, [known, Guid.NewGuid()]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await ListAsync(orgId, courseId)).Should().BeEmpty();
    }

    [Fact]
    public async Task EnrollBulk_ExceedsCapacityWithinTheSameBatch_RefusesTheOnesPastTheLimit()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Curso con cupo dos", capacity: 2);
        var first = await SeedAthleteAsync(orgId, "Tito");
        var second = await SeedAthleteAsync(orgId, "Uma");
        var third = await SeedAthleteAsync(orgId, "Vera");

        // Nobody is enrolled yet, so the cap is only reached by counting
        // this same batch's own earlier names (EnrollmentPolicy's `pending`)
        // -- not by anything already in the database.
        var result = await EnrollBulkAsync(orgId, courseId, [first, second, third]);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails).Errors.Should().ContainKey("AthleteIds[2]");
        (await ListAsync(orgId, courseId)).Should().BeEmpty();
    }

    [Fact]
    public void BulkValidator_EmptyList_IsRejected()
    {
        var result = new EnrollStudentsBulk.Validator().Validate(new EnrollStudentsBulk.Request([]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BulkValidator_RepeatedAthlete_IsRejected()
    {
        var athleteId = Guid.NewGuid();
        var result = new EnrollStudentsBulk.Validator().Validate(
            new EnrollStudentsBulk.Request([athleteId, athleteId]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task WithdrawThenReinstate_RoundTrips()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Atletismo");
        var athleteId = await SeedAthleteAsync(orgId, "Gus");
        var enrollmentId = await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        Assert.IsType<NoContent>(await WithdrawAsync(orgId, enrollmentId, withdrawn: true));
        (await ListAsync(orgId, courseId, active: true)).Should().BeEmpty();

        Assert.IsType<NoContent>(await WithdrawAsync(orgId, enrollmentId, withdrawn: false));
        (await ListAsync(orgId, courseId, active: true)).Should().ContainSingle(entry => entry.Id == enrollmentId);
    }

    [Fact]
    public async Task WithdrawTwiceInARow_SecondCallIsANoOpSuccess()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Ajedrez");
        var athleteId = await SeedAthleteAsync(orgId, "Hugo");
        var enrollmentId = await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        await WithdrawAsync(orgId, enrollmentId, withdrawn: true);
        var second = await WithdrawAsync(orgId, enrollmentId, withdrawn: true);

        Assert.IsType<NoContent>(second);
    }

    [Fact]
    public async Task Reinstate_WhenCourseFilledWhileAway_IsRefused()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Curso con cupo uno otra vez", capacity: 1);
        var first = await SeedAthleteAsync(orgId, "Ines");
        var second = await SeedAthleteAsync(orgId, "Joel");

        var firstEnrollment = await EnrollSuccessfullyAsync(orgId, courseId, first);
        await WithdrawAsync(orgId, firstEnrollment, withdrawn: true);

        // The one seat frees up and somebody else takes it while the first
        // student is away.
        await EnrollSuccessfullyAsync(orgId, courseId, second);

        var result = await WithdrawAsync(orgId, firstEnrollment, withdrawn: false);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EnrollAgainAfterWithdrawal_IsRefused_MustReinstateInsteadOfCreatingANewRow()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Taekwondo");
        var athleteId = await SeedAthleteAsync(orgId, "Kira");
        var enrollmentId = await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        await WithdrawAsync(orgId, enrollmentId, withdrawn: true);

        var result = await EnrollAsync(orgId, courseId, athleteId);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Strike_FreesTheEnrollmentForAGenuinelyNewOne()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Gimnasia");
        var athleteId = await SeedAthleteAsync(orgId, "Luz");
        var enrollmentId = await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        Assert.IsType<NoContent>(await StrikeAsync(orgId, enrollmentId));

        // Struck, not withdrawn: the slot is genuinely free again, unlike
        // EnrollAgainAfterWithdrawal_IsRefused above.
        await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        (await ListAsync(orgId, courseId)).Should().ContainSingle();
    }

    [Fact]
    public async Task SetFee_WithAmountAndReason_Persists()
    {
        var orgId = await SeedOrganizationAsync();
        var courseId = await SeedCourseAsync(orgId, "Natación becada");
        var athleteId = await SeedAthleteAsync(orgId, "Mara");
        var enrollmentId = await EnrollSuccessfullyAsync(orgId, courseId, athleteId);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await SetEnrollmentFee.HandleAsync(
            enrollmentId, new SetEnrollmentFee.Request(0m, "Beca deportiva completa"), context, CancellationToken.None);
        await transaction.CommitAsync();

        Assert.IsType<NoContent>(result);

        var listing = await ListAsync(orgId, courseId);
        listing.Should().ContainSingle(entry =>
            entry.Id == enrollmentId
            && entry.FeeOverrideBob == 0m
            && entry.FeeOverrideReason == "Beca deportiva completa");
    }

    [Fact]
    public void FeeValidator_AmountWithoutReason_IsRejected()
    {
        var result = new SetEnrollmentFee.Validator().Validate(new SetEnrollmentFee.Request(50m, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FeeValidator_ReasonWithoutAmount_IsRejected()
    {
        var result = new SetEnrollmentFee.Validator().Validate(new SetEnrollmentFee.Request(null, "Motivo sin monto"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FeeValidator_BothAbsent_IsAcceptedAsClearingTheOverride()
    {
        var result = new SetEnrollmentFee.Validator().Validate(new SetEnrollmentFee.Request(null, null));

        result.IsValid.Should().BeTrue();
    }
}
