using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Tests.Infrastructure.Validation;

public sealed class PhotoValidatorClientTests
{
    private const string BaseUrl = "http://validador:8000";

    private static readonly byte[] Photo = [0xFF, 0xD8, 0xFF, 0xE0];

    private const string ApprovedWithWarning =
        """{"estado":"aprobada","motivos":[],"advertencias":["No verificado: boca cerrada."],"controles":[],"version_reglas":"0.1.0","calibrado":false}""";

    private const string Rejected =
        """{"estado":"rechazada","motivos":["Se ven lentes de sol. Subí una foto sin lentes de sol."],"advertencias":[],"controles":[],"version_reglas":"0.1.0","calibrado":false}""";

    [Fact]
    public async Task An_approved_answer_becomes_an_approved_verdict_with_its_warnings()
    {
        var client = Build(_ => Respond(ApprovedWithWarning));

        var verdict = await client.ValidateAsync(Photo, CancellationToken.None);

        verdict.State.Should().Be(PhotoVerdictState.Approved);
        verdict.Reasons.Should().BeEmpty();
        verdict.Warnings.Should().Equal("No verificado: boca cerrada.");
        verdict.RulesVersion.Should().Be("0.1.0");
        verdict.Calibrated.Should().BeFalse();
    }

    [Fact]
    public async Task Warnings_and_unverified_rules_are_read_as_two_different_things()
    {
        // Un defecto que no bloquea no es lo mismo que una regla que no se
        // revisó, y el cliente no puede mezclarlos.
        var client = Build(_ => Respond(
            """
            {"estado":"aprobada","motivos":[],"advertencias":["La foto está borrosa."],
             "no_verificado":["No verificado: boca cerrada."],"controles":[],
             "version_reglas":"0.2.0","calibrado":false}
            """));

        var verdict = await client.ValidateAsync(Photo, CancellationToken.None);

        verdict.State.Should().Be(PhotoVerdictState.Approved);
        verdict.Warnings.Should().Equal("La foto está borrosa.");
        verdict.Unverified.Should().Equal("No verificado: boca cerrada.");
    }

    [Fact]
    public async Task A_rejected_answer_becomes_a_rejected_verdict_with_its_reasons()
    {
        var client = Build(_ => Respond(Rejected));

        var verdict = await client.ValidateAsync(Photo, CancellationToken.None);

        verdict.State.Should().Be(PhotoVerdictState.Rejected);
        verdict.Reasons.Should().Equal("Se ven lentes de sol. Subí una foto sin lentes de sol.");
    }

    [Fact]
    public async Task The_photo_is_sent_as_the_archivo_field_of_a_multipart_post_to_validar()
    {
        var handler = new FakeHandler(_ => Respond(ApprovedWithWarning));
        var client = Build(handler);

        await client.ValidateAsync(Photo, CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri.Should().Be(new Uri("http://validador:8000/validar"));

        handler.LastBody.Should().Contain("name=archivo");
        handler.LastBody.Should().Contain("Content-Type: image/jpeg");
    }

    [Fact]
    public async Task A_base_url_with_a_path_still_posts_to_the_validar_endpoint()
    {
        var handler = new FakeHandler(_ => Respond(ApprovedWithWarning));
        var client = Build(handler, url: "http://validador:8000/");

        await client.ValidateAsync(Photo, CancellationToken.None);

        handler.LastRequest!.RequestUri.Should().Be(new Uri("http://validador:8000/validar"));
    }

    [Fact]
    public async Task Without_a_configured_url_it_is_unavailable_and_no_request_is_sent()
    {
        var handler = new FakeHandler(_ => Respond(ApprovedWithWarning));
        var client = Build(handler, url: null);

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithMessage("*Validador:Url*");
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task An_error_status_is_unavailable()
    {
        var client = Build(_ => Respond("{}", HttpStatusCode.InternalServerError));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithMessage("*500*");
    }

    [Fact]
    public async Task A_connection_failure_is_unavailable()
    {
        var client = Build(_ => throw new HttpRequestException("connection refused"));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithInnerException(typeof(HttpRequestException));
    }

    [Fact]
    public async Task A_timeout_is_unavailable()
    {
        // HttpClient reports its own timeout as a TaskCanceledException whose
        // inner exception is a TimeoutException, while the caller's token is intact.
        var client = Build(_ => throw new TaskCanceledException("timed out", new TimeoutException()));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithMessage("*in time*");
    }

    [Fact]
    public async Task A_cancelled_caller_is_not_reported_as_the_validator_being_unavailable()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var client = Build(_ => throw new TaskCanceledException("cancelled"));

        Func<Task> act = () => client.ValidateAsync(Photo, cancelled.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_unavailable()
    {
        var client = Build(_ => Respond("<html>bad gateway</html>"));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>();
    }

    [Fact]
    public async Task An_unknown_state_is_unavailable_rather_than_guessed()
    {
        var client = Build(_ => Respond(
            """{"estado":"revisar","motivos":[],"advertencias":[],"controles":[],"version_reglas":"0.1.0","calibrado":false}"""));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithMessage("*'revisar'*");
    }

    [Fact]
    public async Task An_answer_without_a_rules_version_is_unavailable()
    {
        var client = Build(_ => Respond(
            """{"estado":"aprobada","motivos":[],"advertencias":[],"controles":[],"calibrado":false}"""));

        Func<Task> act = () => client.ValidateAsync(Photo, CancellationToken.None);

        await act.Should().ThrowAsync<PhotoValidatorUnavailableException>()
            .WithMessage("*rules version*");
    }

    private static PhotoValidatorClient Build(
        FakeHandler handler, string? url = BaseUrl)
    {
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var options = Options.Create(new PhotoValidationOptions { Url = url });

        return new PhotoValidatorClient(http, options, NullLogger<PhotoValidatorClient>.Instance);
    }

    private static PhotoValidatorClient Build(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, string? url = BaseUrl) =>
        Build(new FakeHandler(respond), url);

    private static Task<HttpResponseMessage> Respond(
        string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    /// <summary>Stands in for the validator and remembers the last request.</summary>
    private sealed class FakeHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        /// <summary>
        /// The body as sent. Read here because the client disposes its content
        /// as soon as the send completes, so it can't be read afterwards.
        /// </summary>
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await respond(request);
        }
    }
}
