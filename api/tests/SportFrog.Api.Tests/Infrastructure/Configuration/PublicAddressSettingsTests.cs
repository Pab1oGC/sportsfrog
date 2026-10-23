using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using SportFrog.Api.Features.Auth;

namespace SportFrog.Api.Tests.Infrastructure.Configuration;

/// <summary>
/// The address a password reset link carries, asserted against the
/// configuration files as committed.
///
/// It lives in appsettings.json rather than travelling as an environment
/// variable from the deployment's own files, and that is deliberate: it is
/// not a secret, and the one thing it must never be is absent. A blank
/// <see cref="PasswordResetOptions.ResetBaseUrl"/> is a supported state —
/// <see cref="RequestPasswordReset"/> still answers the caller the same — but
/// what it means in practice is that nobody ever receives a reset mail, and
/// it means it silently.
///
/// <see cref="Features.Documents.DocumentOptions"/> is deliberately not
/// covered here. Its address is injected per deployment, by files this
/// repository is not the owner of.
/// </summary>
public sealed class PublicAddressSettingsTests
{
    private static readonly string ApiProjectDirectory = FindApiProjectDirectory();

    [Fact]
    public void TheCommittedResetAddressIsAnAbsolutePublicAddress()
    {
        var value = Read("appsettings.json")["PasswordReset:ResetBaseUrl"];

        value.Should().NotBeNullOrWhiteSpace(
            "a blank reset address is not an error anywhere — it is simply no mail, ever");

        Uri.TryCreate(value, UriKind.Absolute, out var address).Should().BeTrue();
        address!.Scheme.Should().Be(Uri.UriSchemeHttps);
        address.IsLoopback.Should().BeFalse();
    }

    [Fact]
    public void DevelopmentOverridesItWithTheDevServer()
    {
        // Without the override, a link mailed from a developer's machine would
        // carry the production address — and with it a token that installation
        // cannot redeem.
        var value = Read("appsettings.Development.json")["PasswordReset:ResetBaseUrl"];

        value.Should().NotBeNullOrWhiteSpace();
        Uri.TryCreate(value, UriKind.Absolute, out var address).Should().BeTrue();
        address!.IsLoopback.Should().BeTrue();
    }

    /// <summary>
    /// The page the link opens reads the token off the query string, so the
    /// address has to be the page itself and carry nothing of its own —
    /// <see cref="PasswordResetMail.Link"/> appends <c>?token=</c> to it.
    /// </summary>
    [Fact]
    public void TheCommittedResetAddressCarriesNoQueryOfItsOwn()
    {
        var value = Read("appsettings.json")["PasswordReset:ResetBaseUrl"];

        new Uri(value!).Query.Should().BeEmpty();
        PasswordResetMail.Link(value!, "un-token").Should().EndWith("?token=un-token");
    }

    [Fact]
    public void TheCommittedTokenLifetimeIsShortEnoughToSitInAnInbox()
    {
        var options = new PasswordResetOptions();
        Read("appsettings.json").GetSection(PasswordResetOptions.SectionName).Bind(options);

        options.TokenLifetime.Should().BeGreaterThan(TimeSpan.Zero);
        options.TokenLifetime.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(24));
    }

    private static IConfigurationRoot Read(string fileName) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ApiProjectDirectory, fileName), optional: false)
            .Build();

    /// <summary>
    /// Walks up from the test assembly to the API project, so this reads the
    /// files as committed rather than whatever the build copied out.
    /// </summary>
    private static string FindApiProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "api", "src", "SportFrog.Api");

            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find api/src/SportFrog.Api above the test assembly.");
    }
}
