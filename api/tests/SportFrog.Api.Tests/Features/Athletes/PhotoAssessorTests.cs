using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Tests.Features.Athletes;

public sealed class PhotoAssessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Photo = [0xFF, 0xD8, 0xFF, 0xE0];

    [Fact]
    public async Task An_approved_verdict_is_recorded_as_approved_with_its_rules_version_and_time()
    {
        var assessor = Build(new FakeValidator(new PhotoVerdict(
            PhotoVerdictState.Approved, [], ["No verificado: boca cerrada."], [], "0.1.0", false)));

        var assessment = await assessor.AssessAsync(Photo, CancellationToken.None);

        assessment.State.Should().Be(PhotoValidationState.Approved);
        assessment.Reasons.Should().BeEmpty();
        assessment.Warnings.Should().Equal("No verificado: boca cerrada.");
        assessment.RulesVersion.Should().Be("0.1.0");
        assessment.ValidatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task A_rejected_verdict_is_recorded_as_rejected_with_its_reasons()
    {
        var assessor = Build(new FakeValidator(new PhotoVerdict(
            PhotoVerdictState.Rejected, ["Se ven lentes de sol."], [], [], "0.1.0", false)));

        var assessment = await assessor.AssessAsync(Photo, CancellationToken.None);

        assessment.State.Should().Be(PhotoValidationState.Rejected);
        assessment.Reasons.Should().Equal("Se ven lentes de sol.");
    }

    [Fact]
    public async Task A_validator_that_does_not_answer_is_recorded_as_not_evaluated_not_rejected()
    {
        // The photograph is not at fault, so it must not be marked as failed.
        var assessor = Build(new FakeValidator(new PhotoValidatorUnavailableException("down")));

        var assessment = await assessor.AssessAsync(Photo, CancellationToken.None);

        assessment.State.Should().Be(PhotoValidationState.NotEvaluated);
        assessment.Reasons.Should().BeEmpty();
        assessment.RulesVersion.Should().BeNull();
        assessment.ValidatedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_cancelled_request_is_not_swallowed_into_not_evaluated()
    {
        var assessor = Build(new FakeValidator(new OperationCanceledException()));

        Func<Task> act = () => assessor.AssessAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ApplyTo_writes_every_field_onto_the_athlete()
    {
        var athlete = NewAthlete();
        var assessment = new PhotoAssessment(
            PhotoValidationState.Rejected,
            ["Se ven lentes de sol."],
            ["No verificado: boca cerrada."],
            "0.1.0",
            Now);

        assessment.ApplyTo(athlete);

        athlete.PhotoValidationState.Should().Be(PhotoValidationState.Rejected);
        athlete.PhotoValidationReasons.Should().Equal("Se ven lentes de sol.");
        athlete.PhotoValidationWarnings.Should().Equal("No verificado: boca cerrada.");
        athlete.PhotoRulesVersion.Should().Be("0.1.0");
        athlete.PhotoValidatedAt.Should().Be(Now);
    }

    [Fact]
    public void Applying_the_unevaluated_state_clears_an_earlier_verdict()
    {
        // A photograph that is removed, or replaced by one nobody checked, must
        // not leave the previous photograph's reasons behind.
        var athlete = NewAthlete();
        new PhotoAssessment(PhotoValidationState.Rejected, ["Se ven lentes de sol."], ["advertencia"], "0.1.0", Now)
            .ApplyTo(athlete);

        PhotoAssessment.Unevaluated.ApplyTo(athlete);

        athlete.PhotoValidationState.Should().Be(PhotoValidationState.NotEvaluated);
        athlete.PhotoValidationReasons.Should().BeEmpty();
        athlete.PhotoValidationWarnings.Should().BeEmpty();
        athlete.PhotoRulesVersion.Should().BeNull();
        athlete.PhotoValidatedAt.Should().BeNull();
    }

    [Fact]
    public void Applying_copies_the_lists_so_the_athlete_does_not_share_them_with_the_assessment()
    {
        var reasons = new List<string> { "Se ven lentes de sol." };
        var athlete = NewAthlete();

        new PhotoAssessment(PhotoValidationState.Rejected, reasons, [], "0.1.0", Now).ApplyTo(athlete);
        reasons.Add("cambio posterior");

        athlete.PhotoValidationReasons.Should().Equal("Se ven lentes de sol.");
    }

    private static PhotoAssessor Build(IPhotoValidator validator) =>
        new(validator, new FixedClock(Now), NullLogger<PhotoAssessor>.Instance);

    private static Athlete NewAthlete() => new()
    {
        FirstName = "Ana",
        LastName = "Pérez",
        DocumentId = "1234567",
    };

    private sealed class FakeValidator : IPhotoValidator
    {
        private readonly PhotoVerdict? _verdict;
        private readonly Exception? _failure;

        public FakeValidator(PhotoVerdict verdict) => _verdict = verdict;

        public FakeValidator(Exception failure) => _failure = failure;

        public Task<PhotoVerdict> ValidateAsync(byte[] photo, CancellationToken cancellationToken) =>
            _failure is not null
                ? throw _failure
                : Task.FromResult(_verdict!);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
