using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Auth;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Auth;

/// <summary>
/// <see cref="PasswordResetTokens"/> against a real database, because the
/// properties worth asserting are the ones the database enforces: a token
/// that is good exactly once, even when two callers redeem it at the same
/// moment, and one that stops working the instant a newer one is asked for.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PasswordResetTokensTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsync_StoresOnlyTheDigest()
    {
        var userId = await SeedUserAsync();

        await using var context = fixture.CreateAppContext();
        var token = await Tokens(context).IssueAsync(userId, CancellationToken.None);

        var stored = await context.PasswordResetTokens
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == userId);

        stored.TokenHash.Should().Be(OpaqueSecretHash(token));
        stored.TokenHash.Should().NotBe(token);
        stored.ExpiresAt.Should().Be(Now.AddHours(1));
        stored.ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task RedeemAsync_ReturnsTheAccount_AndSpendsTheToken()
    {
        var userId = await SeedUserAsync();

        await using var issuing = fixture.CreateAppContext();
        var token = await Tokens(issuing).IssueAsync(userId, CancellationToken.None);

        await using var redeeming = fixture.CreateAppContext();
        var redeemed = await Tokens(redeeming).RedeemAsync(token, CancellationToken.None);

        redeemed.Should().Be(userId);

        // RedeemAsync deliberately leaves the save to its caller, which is
        // what lets the password change and the spent token commit together.
        await redeeming.SaveChangesAsync();

        await using var reading = fixture.CreateAppContext();
        var stored = await reading.PasswordResetTokens
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == userId);

        stored.ConsumedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RedeemAsync_RefusesATokenThatWasAlreadySpent()
    {
        var userId = await SeedUserAsync();

        await using var issuing = fixture.CreateAppContext();
        var token = await Tokens(issuing).IssueAsync(userId, CancellationToken.None);

        await using var first = fixture.CreateAppContext();
        await Tokens(first).RedeemAsync(token, CancellationToken.None);
        await first.SaveChangesAsync();

        await using var second = fixture.CreateAppContext();
        var again = await Tokens(second).RedeemAsync(token, CancellationToken.None);

        again.Should().BeNull();
    }

    [Fact]
    public async Task RedeemAsync_RefusesAnExpiredToken()
    {
        var userId = await SeedUserAsync();

        await using var issuing = fixture.CreateAppContext();
        var token = await Tokens(issuing).IssueAsync(userId, CancellationToken.None);

        await using var later = fixture.CreateAppContext();
        var redeemed = await Tokens(later, Now.AddHours(1).AddSeconds(1))
            .RedeemAsync(token, CancellationToken.None);

        redeemed.Should().BeNull();
    }

    [Fact]
    public async Task RedeemAsync_RefusesAnUnknownToken()
    {
        await using var context = fixture.CreateAppContext();

        var redeemed = await Tokens(context).RedeemAsync("not-a-token", CancellationToken.None);

        redeemed.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_SupersedesATokenTheAccountIsStillHolding()
    {
        var userId = await SeedUserAsync();

        await using var issuing = fixture.CreateAppContext();
        var service = Tokens(issuing);

        var first = await service.IssueAsync(userId, CancellationToken.None);
        var second = await service.IssueAsync(userId, CancellationToken.None);

        await using var redeeming = fixture.CreateAppContext();
        var tokens = Tokens(redeeming);

        (await tokens.RedeemAsync(first, CancellationToken.None)).Should().BeNull();
        (await tokens.RedeemAsync(second, CancellationToken.None)).Should().Be(userId);
    }

    /// <summary>
    /// Two callers redeeming the same token with neither having saved yet —
    /// the race the <c>consumed_at</c> concurrency token exists for. One save
    /// wins; the other matches no row and raises rather than changing a
    /// password against a token already spent.
    /// </summary>
    [Fact]
    public async Task RedeemAsync_LetsOnlyOneOfTwoConcurrentRedemptionsSave()
    {
        var userId = await SeedUserAsync();

        await using var issuing = fixture.CreateAppContext();
        var token = await Tokens(issuing).IssueAsync(userId, CancellationToken.None);

        await using var winner = fixture.CreateAppContext();
        await using var loser = fixture.CreateAppContext();

        (await Tokens(winner).RedeemAsync(token, CancellationToken.None)).Should().Be(userId);
        (await Tokens(loser).RedeemAsync(token, CancellationToken.None)).Should().Be(userId);

        await winner.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loser.SaveChangesAsync());
    }

    private static PasswordResetTokens Tokens(SportFrogDbContext context, DateTimeOffset? now = null) =>
        new(
            context,
            Options.Create(new PasswordResetOptions
            {
                ResetBaseUrl = "https://example.test/auth/jwt/reset-password",
            }),
            new FixedTimeProvider(now ?? Now));

    /// <summary>
    /// The digest the service is expected to have stored, derived the way
    /// <see cref="RefreshTokenFactory"/> derives its own — the same
    /// <c>OpaqueSecret</c> both are built on, reached here through the public
    /// surface of the two.
    /// </summary>
    private static string OpaqueSecretHash(string token) => RefreshTokenFactory.Hash(token);

    private async Task<Guid> SeedUserAsync()
    {
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Users.Add(new User
        {
            Id = userId,
            Email = $"reset-{userId:N}@example.test",
            PasswordHash = "not-a-real-hash",
            FullName = "Cuenta de prueba",
        });

        await setup.SaveChangesAsync();

        return userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
