using System.IdentityModel.Tokens.Jwt;
using AwesomeAssertions;
using Microsoft.IdentityModel.Tokens;
using SportFrog.Api.Infrastructure.Auth;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// RF-02 — after login, the access token must carry the user's identity and
/// roles: everything downstream authorization decides on comes from here.
/// </summary>
public sealed class JwtAccessTokenIssuerTests
{
    // 32 ASCII bytes: the minimum HS256 accepts without weakening the MAC.
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";
    private const string OtherSigningKey = "a-completely-different-key-32by";
    private const string Issuer = "sportfrog-api";
    private const string Audience = "sportfrog-clients";
    private const string RoleClaimType = "role";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly JwtAccessTokenIssuer _issuer = new(SigningKey, Issuer, Audience, Lifetime);

    private static JwtSecurityToken Read(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Issue_ProducesAWellFormedJwt()
    {
        var token = _issuer.Issue(Guid.NewGuid(), ["admin"]);

        token.Split('.').Should().HaveCount(3);
        new JwtSecurityTokenHandler().CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void Issue_ProducesATokenThatValidatesWithTheConfiguredKey()
    {
        var token = _issuer.Issue(Guid.NewGuid(), ["admin"]);

        var validating = () => new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(SigningKey)),
            },
            out _);

        validating.Should().NotThrow();
    }

    [Fact]
    public void Issue_ProducesATokenThatFailsValidation_WithADifferentSigningKey()
    {
        // Proves the signature is real protection and not a decorative
        // string: forging or altering the payload without the real key must
        // be detectable.
        var token = _issuer.Issue(Guid.NewGuid(), ["admin"]);

        var validating = () => new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(OtherSigningKey)),
            },
            out _);

        validating.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void Issue_EmbedsTheGivenUserIdAsTheSubjectClaim()
    {
        var userId = Guid.NewGuid();

        var token = _issuer.Issue(userId, ["admin"]);

        Read(token).Subject.Should().Be(userId.ToString());
    }

    [Fact]
    public void Issue_EmbedsExactlyTheGivenRoles()
    {
        string[] roles = ["admin", "recorder"];

        var token = _issuer.Issue(Guid.NewGuid(), roles);

        var embeddedRoles = Read(token).Claims
            .Where(c => c.Type == RoleClaimType)
            .Select(c => c.Value);

        // Set comparison: no missing role, no extra role, order irrelevant.
        embeddedRoles.Should().BeEquivalentTo(roles);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("recorder")]
    [InlineData("viewer")]
    public void Issue_EmbedsEachRoleDefinedByRF02(string role)
    {
        var token = _issuer.Issue(Guid.NewGuid(), [role]);

        Read(token).Claims.Should().Contain(c => c.Type == RoleClaimType && c.Value == role);
    }

    [Fact]
    public void Issue_SetsTheConfiguredIssuerAndAudience()
    {
        var token = _issuer.Issue(Guid.NewGuid(), ["admin"]);
        var jwt = Read(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void Issue_SetsAnExpirationConsistentWithTheConfiguredLifetime()
    {
        var before = DateTime.UtcNow;

        var token = _issuer.Issue(Guid.NewGuid(), ["admin"]);

        var expiresAt = Read(token).ValidTo;

        // A short-lived token, as the design calls for: long enough to be
        // usable, short enough to bound the damage of a leaked one.
        expiresAt.Should().BeCloseTo(before + Lifetime, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Issue_GivesEachTokenAUniqueIdentifier()
    {
        var userId = Guid.NewGuid();

        var first = Read(_issuer.Issue(userId, ["admin"]));
        var second = Read(_issuer.Issue(userId, ["admin"]));

        // Same user, same roles, same instant is still possible: without a
        // jti, two tokens for one login could become indistinguishable in
        // logs and unrevokable individually.
        var firstJti = first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var secondJti = second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        firstJti.Should().NotBe(secondJti);
    }

    [Fact]
    public void Issue_RejectsAnEmptyUserId()
    {
        var issuing = () => _issuer.Issue(Guid.Empty, ["admin"]);

        issuing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_RejectsAnEmptyRoleCollection()
    {
        // A token with no roles authorizes nothing and signals a bug
        // upstream: every membership carries exactly one role.
        var issuing = () => _issuer.Issue(Guid.NewGuid(), []);

        issuing.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(new object?[] { new[] { "admin", null } })]
    [InlineData(new object?[] { new[] { "admin", "" } })]
    [InlineData(new object?[] { new[] { "admin", "   " } })]
    public void Issue_RejectsANullOrBlankRoleValue(string?[] roles)
    {
        var issuing = () => _issuer.Issue(Guid.NewGuid(), roles!);

        issuing.Should().Throw<ArgumentException>();
    }
}
