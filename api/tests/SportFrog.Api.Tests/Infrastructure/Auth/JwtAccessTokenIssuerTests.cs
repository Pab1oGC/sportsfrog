using System.IdentityModel.Tokens.Jwt;
using AwesomeAssertions;
using Microsoft.IdentityModel.Tokens;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// RF-02 — after login, the access token must carry the user's identity and,
/// for each organization they belong to, the role they hold there:
/// everything downstream authorization decides on comes from here.
///
/// Authorization is never global. A role that travelled without the
/// organization it applies to would grant more than intended, so the token
/// has no way to express one.
/// </summary>
public sealed class JwtAccessTokenIssuerTests
{
    // 32 ASCII bytes: the minimum HS256 accepts without weakening the MAC.
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";
    private const string OtherSigningKey = "a-completely-different-key-32by";
    private const string Issuer = "sportfrog-api";
    private const string Audience = "sportfrog-clients";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    // An arbitrary organization, for the tests whose subject is the mechanics
    // of the token rather than which organization grants what.
    private static readonly Guid AnyOrganization = Guid.NewGuid();

    private readonly JwtAccessTokenIssuer _issuer = new(SigningKey, Issuer, Audience, Lifetime);

    private static JwtSecurityToken Read(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    /// <summary>
    /// The claim type that grants a role inside one organization. Spelled out
    /// here rather than taken from the production constant, so that a change
    /// to the wire format has to be made deliberately in two places.
    /// </summary>
    private static string OrganizationClaimType(Guid organizationId) => $"org:{organizationId}";

    private static OrganizationAccess Membership(MembershipRole role) =>
        new(AnyOrganization, role);

    [Fact]
    public void Issue_ProducesAWellFormedJwt()
    {
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);

        token.Split('.').Should().HaveCount(3);
        new JwtSecurityTokenHandler().CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void Issue_ProducesATokenThatValidatesWithTheConfiguredKey()
    {
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);

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
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);

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

        var token = _issuer.IssueForOrganizations(userId, [Membership(MembershipRole.Admin)]);

        Read(token).Subject.Should().Be(userId.ToString());
    }

    [Fact]
    public void Issue_EmbedsEachRoleUnderTheOrganizationItAppliesTo()
    {
        // The case the design names: a referee who operates one league and
        // only consults another. A flat list of roles could say that both
        // roles exist, but never which one applies where.
        var operatedLeague = Guid.NewGuid();
        var consultedLeague = Guid.NewGuid();

        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [
            new OrganizationAccess(operatedLeague, MembershipRole.Operator),
            new OrganizationAccess(consultedLeague, MembershipRole.Viewer),
        ]);

        var claims = Read(token).Claims.ToList();

        claims.Should().Contain(c =>
            c.Type == OrganizationClaimType(operatedLeague) && c.Value == "operator");
        claims.Should().Contain(c =>
            c.Type == OrganizationClaimType(consultedLeague) && c.Value == "viewer");

        // No extra grant beyond the two asked for.
        claims.Count(c => c.Type.StartsWith("org:")).Should().Be(2);
    }

    [Fact]
    public void Issue_EmbedsNoRoleThatIsNotTiedToAnOrganization()
    {
        // A bare "role" claim would let a careless permission check pass in
        // an organization where the user is only a viewer. The per-organization
        // model exists to prevent exactly that, so the claim must not appear.
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);

        Read(token).Claims.Should().NotContain(c => c.Type == "role");
    }

    [Theory]
    [InlineData(MembershipRole.Owner, "owner")]
    [InlineData(MembershipRole.Admin, "admin")]
    [InlineData(MembershipRole.Operator, "operator")]
    [InlineData(MembershipRole.Recorder, "recorder")]
    [InlineData(MembershipRole.Viewer, "viewer")]
    public void Issue_EmbedsEachRoleDefinedByRF02(MembershipRole role, string expectedClaimValue)
    {
        // The expected wire value is stated literally rather than derived
        // from the enum, so a change in how roles are spelled on the wire
        // fails here instead of agreeing with itself.
        var organization = Guid.NewGuid();

        var token = _issuer.IssueForOrganizations(
            Guid.NewGuid(), [new OrganizationAccess(organization, role)]);

        Read(token).Claims.Should().Contain(c =>
            c.Type == OrganizationClaimType(organization) && c.Value == expectedClaimValue);
    }

    [Fact]
    public void Issue_SetsTheConfiguredIssuerAndAudience()
    {
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);
        var jwt = Read(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void Issue_SetsAnExpirationConsistentWithTheConfiguredLifetime()
    {
        var before = DateTime.UtcNow;

        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [Membership(MembershipRole.Admin)]);

        var expiresAt = Read(token).ValidTo;

        // A short-lived token, as the design calls for: long enough to be
        // usable, short enough to bound the damage of a leaked one.
        expiresAt.Should().BeCloseTo(before + Lifetime, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Issue_GivesEachTokenAUniqueIdentifier()
    {
        var userId = Guid.NewGuid();

        var first = Read(_issuer.IssueForOrganizations(userId, [Membership(MembershipRole.Admin)]));
        var second = Read(_issuer.IssueForOrganizations(userId, [Membership(MembershipRole.Admin)]));

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
        var issuing = () => _issuer.IssueForOrganizations(
            Guid.Empty, [Membership(MembershipRole.Admin)]);

        issuing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_RejectsAnEmptyOrganizationCollection()
    {
        // A token that reaches no organization authorizes nothing and signals
        // a bug upstream: a user with no membership cannot sign in.
        var issuing = () => _issuer.IssueForOrganizations(Guid.NewGuid(), []);

        issuing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_RejectsAnAbsentOrganizationId()
    {
        var issuing = () => _issuer.IssueForOrganizations(
            Guid.NewGuid(), [new OrganizationAccess(Guid.Empty, MembershipRole.Admin)]);

        issuing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_RejectsTheSameOrganizationTwice()
    {
        // A membership is unique per (organization, user), so two entries for
        // one organization mean the caller built the list wrong. Left
        // unchecked, the token would grant two roles in the same place and
        // which one wins would depend on lookup order.
        var organization = Guid.NewGuid();

        var issuing = () => _issuer.IssueForOrganizations(Guid.NewGuid(), [
            new OrganizationAccess(organization, MembershipRole.Owner),
            new OrganizationAccess(organization, MembershipRole.Viewer),
        ]);

        issuing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindRole_ReturnsTheRoleHeldInThatOrganization()
    {
        var operatedLeague = Guid.NewGuid();
        var consultedLeague = Guid.NewGuid();
        var token = _issuer.IssueForOrganizations(Guid.NewGuid(), [
            new OrganizationAccess(operatedLeague, MembershipRole.Operator),
            new OrganizationAccess(consultedLeague, MembershipRole.Viewer),
        ]);

        var principal = _issuer.Validate(token);

        JwtAccessTokenIssuer.FindRole(principal, operatedLeague)
            .Should().Be(MembershipRole.Operator);
        JwtAccessTokenIssuer.FindRole(principal, consultedLeague)
            .Should().Be(MembershipRole.Viewer);
    }

    [Fact]
    public void FindRole_ReturnsNull_ForAnOrganizationTheTokenDoesNotReach()
    {
        // Not an exceptional condition: it is the authorization answer the
        // caller has to turn into a refusal.
        var token = _issuer.IssueForOrganizations(
            Guid.NewGuid(), [new OrganizationAccess(Guid.NewGuid(), MembershipRole.Owner)]);

        var principal = _issuer.Validate(token);

        JwtAccessTokenIssuer.FindRole(principal, Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void CurrentUserId_ReturnsTheSubjectTheTokenWasIssuedFor()
    {
        var userId = Guid.NewGuid();
        var token = _issuer.IssueForOrganizations(userId, [Membership(MembershipRole.Viewer)]);

        var principal = _issuer.Validate(token);

        JwtAccessTokenIssuer.CurrentUserId(principal).Should().Be(userId);
    }
}
