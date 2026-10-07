using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SkiaSharp;
using SportFrog.Api.Features.Athletes.Photos;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Tests.Features.Athletes;

/// <summary>
/// The preview a dropzone calls before a photograph is saved: what it answers,
/// what it refuses, and the fact that it never stores anything.
/// </summary>
public sealed class PreviewAthletePhotoTests
{
    [Fact]
    public async Task An_approved_verdict_is_answered_as_approved_with_its_warnings()
    {
        var validator = new RecordingValidator(new PhotoVerdict(
            PhotoVerdictState.Approved, [], ["No verificado: boca cerrada."], [], "0.1.0", false));

        var result = await PreviewAthletePhoto.HandleAsync(Upload(Jpeg()), validator, CancellationToken.None);

        var answer = result.Should().BeOfType<Ok<PreviewAthletePhoto.Response>>().Subject.Value!;
        answer.State.Should().Be("approved");
        answer.Reasons.Should().BeEmpty();
        answer.Warnings.Should().Equal("No verificado: boca cerrada.");
        answer.RulesVersion.Should().Be("0.1.0");
        answer.Calibrated.Should().BeFalse();
    }

    [Fact]
    public async Task A_rejected_verdict_is_answered_as_rejected_with_its_reasons()
    {
        var validator = new RecordingValidator(new PhotoVerdict(
            PhotoVerdictState.Rejected, ["Se ven lentes de sol. Subí una foto sin lentes de sol."], [], [], "0.1.0", false));

        var result = await PreviewAthletePhoto.HandleAsync(Upload(Jpeg()), validator, CancellationToken.None);

        var answer = result.Should().BeOfType<Ok<PreviewAthletePhoto.Response>>().Subject.Value!;
        answer.State.Should().Be("rejected");
        answer.Reasons.Should().Equal("Se ven lentes de sol. Subí una foto sin lentes de sol.");
    }

    [Fact]
    public async Task When_the_validator_does_not_answer_the_caller_is_told_to_retry_not_given_a_verdict()
    {
        // Unlike a stored photograph, the person is waiting: an unanswered
        // validator must not look like a verdict.
        var validator = new RecordingValidator(failure: new PhotoValidatorUnavailableException("down"));

        var result = await PreviewAthletePhoto.HandleAsync(Upload(Jpeg()), validator, CancellationToken.None);

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task A_png_is_judged_as_the_jpeg_that_would_be_printed()
    {
        var validator = new RecordingValidator(Approved);

        await PreviewAthletePhoto.HandleAsync(Upload(Png()), validator, CancellationToken.None);

        validator.Received.Should().NotBeNull();
        validator.Received![0].Should().Be(0xFF);
        validator.Received[1].Should().Be(0xD8);
    }

    [Fact]
    public async Task Something_that_is_not_an_image_is_refused_before_the_validator_is_called()
    {
        var validator = new RecordingValidator(Approved);

        var result = await PreviewAthletePhoto.HandleAsync(
            Upload("esto no es una foto"u8.ToArray()), validator, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>();
        validator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task An_empty_upload_is_refused_before_the_validator_is_called()
    {
        var validator = new RecordingValidator(Approved);

        var result = await PreviewAthletePhoto.HandleAsync(Upload([]), validator, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>();
        validator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task An_upload_over_the_limit_is_refused_without_being_read()
    {
        var validator = new RecordingValidator(Approved);
        var oversized = new byte[PreviewAthletePhoto.MaximumUpload + 1];

        var result = await PreviewAthletePhoto.HandleAsync(Upload(oversized), validator, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>();
        validator.Calls.Should().Be(0);
    }

    private static readonly PhotoVerdict Approved = new(PhotoVerdictState.Approved, [], [], [], "0.1.0", false);

    private static IFormFile Upload(byte[] contents) =>
        new FormFile(new MemoryStream(contents), 0, contents.Length, PreviewAthletePhoto.FormField, "foto.jpg");

    private static byte[] Jpeg() => Encode(SKEncodedImageFormat.Jpeg);

    private static byte[] Png() => Encode(SKEncodedImageFormat.Png);

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(400, 500, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);

        return encoded.ToArray();
    }

    /// <summary>Answers with a fixed verdict or failure, and remembers what it was asked to judge.</summary>
    private sealed class RecordingValidator : IPhotoValidator
    {
        private readonly PhotoVerdict? _verdict;
        private readonly Exception? _failure;

        public RecordingValidator(PhotoVerdict verdict) => _verdict = verdict;

        public RecordingValidator(Exception failure) => _failure = failure;

        public int Calls { get; private set; }

        public byte[]? Received { get; private set; }

        public Task<PhotoVerdict> ValidateAsync(byte[] photo, CancellationToken cancellationToken)
        {
            Calls++;
            Received = photo;

            return _failure is not null
                ? throw _failure
                : Task.FromResult(_verdict!);
        }
    }
}
