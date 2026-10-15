using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SportFrog.Api.Features.Tuition;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Tuition;

/// <summary>
/// The scheduled twin of <see cref="AccrueTuition"/>: reaching every
/// organization on its own, with no person behind it, through
/// <see cref="OrganizationJobScope"/> exactly the way
/// <c>CredentialNumberingJobScopeTests</c> already proves a background job
/// does.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TuitionAccrualJobTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";
    private static readonly DateOnly March = new(2026, 3, 1);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static DateTimeOffset AtUtc(DateOnly date) => new(date.ToDateTime(new TimeOnly(0, 0)), TimeSpan.Zero);

    /// <summary>One organization with one chargeable enrollment.</summary>
    private async Task<Guid> SeedOrganizationWithAnEnrollmentAsync(bool active = true)
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
            IsActive = active,
        });
        await setup.SaveChangesAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var courseId = Guid.NewGuid();
        context.Courses.Add(new Course
        {
            Id = courseId,
            OrgId = orgId,
            SportCode = Football,
            Name = $"Curso {Guid.NewGuid():N}",
            MonthlyFeeBob = 100m,
            StartsOn = new DateOnly(2026, 1, 1),
        });

        var athleteId = Guid.NewGuid();
        context.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Estudiante",
            LastName = "DePrueba",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2015, 1, 1),
        });

        context.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CourseId = courseId,
            AthleteId = athleteId,
            EnrolledAt = AtUtc(new DateOnly(2026, 1, 1)),
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return orgId;
    }

    private async Task<int> ChargeCountAsync(Guid orgId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await context.TuitionCharges.CountAsync(charge => charge.Period == March);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<string> Errors = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception) + (exception is null ? "" : " -- " + exception));
            }
        }
    }

    private async Task<List<string>> RunJobAsync()
    {
        // The data source is captured and disposed explicitly, the same way
        // CredentialNumberingJobScopeTests already does: UseNpgsql(string)
        // would have this DbContext build its own throwaway data source
        // and silently leak its connections otherwise, one more pool the
        // suite never closes for every call this test makes.
        await using var dataSource = SportFrogDataSource.Create(fixture.AppConnectionString);

        var services = new ServiceCollection();
        services.AddScoped<OrganizationContext>();
        services.AddScoped<TuitionAccrual>();
        services.AddDbContext<SportFrogDbContext>(options =>
            options.UseNpgsql(dataSource, SportFrogDataSource.MapEnums));

        await using var provider = services.BuildServiceProvider();
        var scopes = new OrganizationJobScope(provider.GetRequiredService<IServiceScopeFactory>());

        await using var database = fixture.CreateAppContext();
        var logger = new CapturingLogger<TuitionAccrualJob>();
        var job = new TuitionAccrualJob(database, scopes, Clock, logger);

        await job.RunAsync(CancellationToken.None);

        return logger.Errors;
    }

    [Fact]
    public async Task RunAsync_ChargesEveryActiveOrganization()
    {
        var first = await SeedOrganizationWithAnEnrollmentAsync();
        var second = await SeedOrganizationWithAnEnrollmentAsync();

        var errors = await RunJobAsync();

        errors.Should().BeEmpty();
        (await ChargeCountAsync(first)).Should().Be(1);
        (await ChargeCountAsync(second)).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_SkipsADeactivatedOrganization()
    {
        var inactive = await SeedOrganizationWithAnEnrollmentAsync(active: false);

        await RunJobAsync();

        (await ChargeCountAsync(inactive)).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_RunTwice_IsIdempotentAcrossOrganizations()
    {
        var orgId = await SeedOrganizationWithAnEnrollmentAsync();

        var errors = (await RunJobAsync()).Concat(await RunJobAsync()).ToList();

        errors.Should().BeEmpty();
        (await ChargeCountAsync(orgId)).Should().Be(1);
    }
}
