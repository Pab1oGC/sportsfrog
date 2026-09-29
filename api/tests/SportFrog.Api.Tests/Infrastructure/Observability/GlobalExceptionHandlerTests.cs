using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SportFrog.Api.Infrastructure.Observability;

namespace SportFrog.Api.Tests.Infrastructure.Observability;

/// <summary>
/// The one property that actually matters here: whatever the real exception
/// says, none of it reaches the response. Everything else — the status
/// code, the shape — is secondary to that.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context, MemoryStream Body) Build()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        var handler = ActivatorUtilities.CreateInstance<GlobalExceptionHandler>(services);

        var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body },
        };

        return (handler, context, body);
    }

    [Fact]
    public async Task TryHandleAsync_GivenAnExceptionWithASensitiveMessage_NeverWritesItToTheResponse()
    {
        var (handler, context, body) = Build();

        // A password, a connection string, an internal path — the kind of
        // thing a real unhandled exception's own Message can end up
        // carrying, and exactly what must never leave this server.
        var exception = new InvalidOperationException(
            "Connection to Host=postgres;Password=letmein failed for user sportfrog_app");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        body.Position = 0;
        var written = await new StreamReader(body).ReadToEndAsync();

        written.Should().NotContain("letmein");
        written.Should().NotContain("Connection to Host=postgres");
        written.Should().NotContain(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task TryHandleAsync_WritesAProblemDetailsBodyShapedLikeEveryOtherFailure()
    {
        var (handler, context, body) = Build();

        await handler.TryHandleAsync(context, new Exception("anything"), CancellationToken.None);

        body.Position = 0;
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        // The same fields Results.Problem(...) already puts on every
        // deliberate 4xx elsewhere in this API — an unhandled failure should
        // not be the one response shape the frontend's error handling has
        // never seen.
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        root.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
