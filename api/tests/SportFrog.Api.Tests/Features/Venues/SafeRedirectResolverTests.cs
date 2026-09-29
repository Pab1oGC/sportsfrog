using System.Net;
using AwesomeAssertions;
using SportFrog.Api.Features.Venues;

namespace SportFrog.Api.Tests.Features.Venues;

/// <summary>
/// No network reached anywhere here: every target is either a literal IP
/// address (which <c>Dns.GetHostAddressesAsync</c> parses without a real
/// lookup) or a stub handler that never lets a request past what the test
/// itself expects — the one Venues module this codebase never got around to
/// testing, closing the specific gap that let the SSRF through unnoticed.
/// </summary>
public sealed class SafeRedirectResolverTests
{
    private sealed class StubHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request.RequestUri!));
    }

    private static HttpClient ClientRespondingWith(Func<Uri, HttpResponseMessage> respond) =>
        new(new StubHandler(respond));

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    [Fact]
    public async Task FollowAsync_EndingAtAPublicAddress_ReturnsTheFinalUri()
    {
        using var client = ClientRespondingWith(uri => uri.Host switch
        {
            "maps.app.goo.gl" => Redirect("http://8.8.8.8/maps"),
            "8.8.8.8" => Ok(),
            _ => throw new InvalidOperationException($"Unexpected request to {uri}."),
        });

        var result = await SafeRedirectResolver.FollowAsync(
            client, new Uri("http://maps.app.goo.gl/x"), CancellationToken.None);

        result.Problem.Should().BeNull();
        result.FinalUri.Should().Be(new Uri("http://8.8.8.8/maps"));
    }

    /// <summary>
    /// The exact chain this defends against: a short link (the only thing the
    /// host allowlist in <c>ManageVenues</c> ever sees) whose own redirect
    /// ends somewhere only this server's network can reach.
    /// </summary>
    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.1.2.3/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://192.168.1.1/")]
    public async Task FollowAsync_RedirectedToAPrivateAddress_RefusesToFollowIt(string target)
    {
        using var client = ClientRespondingWith(uri => uri.Host == "maps.app.goo.gl"
            ? Redirect(target)
            : throw new InvalidOperationException($"Should never have connected to {uri}."));

        var result = await SafeRedirectResolver.FollowAsync(
            client, new Uri("http://maps.app.goo.gl/x"), CancellationToken.None);

        result.FinalUri.Should().BeNull();
        result.Problem.Should().NotBeNull();
    }

    /// <summary>
    /// 172.15.x and 172.32.x sit just outside 172.16.0.0/12 on either side —
    /// worth pinning so the range check is never quietly loosened to the
    /// whole /8 by mistake.
    /// </summary>
    [Theory]
    [InlineData("http://172.15.255.255/")]
    [InlineData("http://172.32.0.0/")]
    public async Task FollowAsync_RedirectedJustOutsideThePrivateRange_StillFollowsIt(string target)
    {
        using var client = ClientRespondingWith(uri => uri.Host switch
        {
            "maps.app.goo.gl" => Redirect(target),
            _ => Ok(),
        });

        var result = await SafeRedirectResolver.FollowAsync(
            client, new Uri("http://maps.app.goo.gl/x"), CancellationToken.None);

        result.Problem.Should().BeNull();
        result.FinalUri.Should().Be(new Uri(target));
    }

    [Fact]
    public async Task FollowAsync_ARelativeLocation_ResolvesItAgainstTheRequestItCameFrom()
    {
        using var client = ClientRespondingWith(uri => uri.PathAndQuery switch
        {
            "/x" => Redirect("/y"),
            "/y" => Ok(),
            _ => throw new InvalidOperationException($"Unexpected request to {uri}."),
        });

        var result = await SafeRedirectResolver.FollowAsync(
            client, new Uri("http://maps.app.goo.gl/x"), CancellationToken.None);

        result.Problem.Should().BeNull();
        result.FinalUri.Should().Be(new Uri("http://maps.app.goo.gl/y"));
    }

    [Fact]
    public async Task FollowAsync_AChainThatNeverEnds_RefusesAfterTheHopLimit()
    {
        var hop = 0;
        using var client = ClientRespondingWith(_ => Redirect($"http://8.8.8.8/{hop++}"));

        var result = await SafeRedirectResolver.FollowAsync(
            client, new Uri("http://8.8.8.8/start"), CancellationToken.None);

        result.FinalUri.Should().BeNull();
        result.Problem.Should().NotBeNull();
    }
}
