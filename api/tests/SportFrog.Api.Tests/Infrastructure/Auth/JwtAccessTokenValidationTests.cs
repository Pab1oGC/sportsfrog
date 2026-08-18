using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AwesomeAssertions;
using Microsoft.IdentityModel.Tokens;
using SportFrog.Api.Infrastructure.Auth;

namespace SportFrog.Api.Tests.Infrastructure.Auth;

/// <summary>
/// RF-02 — a tampered or expired token must be rejected with a specific
/// error, not silently accepted or crash the request pipeline.
/// </summary>
public sealed class JwtAccessTokenValidationTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";
    private const string Issuer = "sportfrog-api";
    private const string Audience = "sportfrog-clients";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    // A clock the test fully controls, so "expired" doesn't mean Thread.Sleep.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }

    private static void Validate(string token) =>
        new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ClockSkew = TimeSpan.Zero,
            },
            out _);

    [Fact]
    public void ValidateToken_RejectsATokenThatHasExpired()
    {
        // Issued 30 minutes ago with a 15-minute lifetime: 15 minutes past
        // its exp, well outside any clock skew.
        var issuer = new JwtAccessTokenIssuer(
            SigningKey, Issuer, Audience, Lifetime,
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-30)));

        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);

        var validating = () => Validate(token);

        validating.Should().Throw<SecurityTokenExpiredException>();
    }

    [Fact]
    public void ValidateToken_AcceptsATokenThatHasNotYetExpired()
    {
        // Paired control: proves the previous rejection is about expiry
        // specifically, not a blanket failure.
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);

        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);

        var validating = () => Validate(token);

        validating.Should().NotThrow();
    }

    [Fact]
    public void ValidateToken_RejectsATokenWhoseRoleWasEscalatedAfterSigning()
    {
        // An attacker edits their own token's payload, promoting "viewer" to
        // "admin", and resubmits it with the original signature untouched.
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);
        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);
        var segments = token.Split('.');

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(segments[1]));
        var escalatedJson = payloadJson.Replace("\"role\":\"viewer\"", "\"role\":\"admin\"");
        escalatedJson.Should().NotBe(payloadJson, "the substitution must actually change the payload");

        var escalatedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(escalatedJson));
        var tamperedToken = $"{segments[0]}.{escalatedPayload}.{segments[2]}";

        var validating = () => Validate(tamperedToken);

        validating.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    [Fact]
    public void ValidateToken_RejectsATokenWithASingleCharacterAlteredInThePayload()
    {
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);
        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);
        var segments = token.Split('.');

        var flippedPayload = FlipOneCharacter(segments[1]);
        var tamperedToken = $"{segments[0]}.{flippedPayload}.{segments[2]}";

        var validating = () => Validate(tamperedToken);

        validating.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    [Fact]
    public void ValidateToken_RejectsATokenWithASingleCharacterAlteredInTheSignature()
    {
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);
        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);
        var segments = token.Split('.');

        var flippedSignature = FlipOneCharacter(segments[2]);
        var tamperedToken = $"{segments[0]}.{segments[1]}.{flippedSignature}";

        var validating = () => Validate(tamperedToken);

        validating.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    [Fact]
    public void ValidateToken_RejectsATokenWithItsAlgorithmDowngradedToNone()
    {
        // The classic "alg: none" downgrade: strip the signature and claim
        // none was needed. Must be rejected, not trusted as unsigned.
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);
        var token = issuer.Issue(Guid.NewGuid(), ["viewer"]);
        var segments = token.Split('.');

        var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(segments[0]));
        var noneHeaderJson = headerJson.Replace("HS256", "none");
        var noneHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(noneHeaderJson));
        var downgradedToken = $"{noneHeader}.{segments[1]}.";

        var validating = () => Validate(downgradedToken);

        validating.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void ValidateToken_RejectsAnEmptyString()
    {
        var validating = () => Validate("");

        validating.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateToken_RejectsAStringThatIsNotAJwtAtAll()
    {
        // Goes through our own validator rather than the raw handler: the
        // library reports unreadable input as an ArgumentException, which
        // would read as a programming fault and become a server error. Every
        // refusal has to reach the caller as one family it can answer with an
        // authentication failure.
        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);

        var validating = () => issuer.Validate("no-es-un-jwt");

        validating.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void ValidateToken_RejectsAProperlySignedTokenThatIsMissingTheSubjectClaim()
    {
        // Hand-built and correctly signed, but with no "sub": only the
        // issuer holds the key, so this simulates a bug in whatever crafts
        // the payload rather than a forgery. The reading side must still
        // refuse to trust a token it can't attach to a user.
        var tokenWithoutSubject = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim("role", "viewer")],
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));
        var token = new JwtSecurityTokenHandler().WriteToken(tokenWithoutSubject);

        var issuer = new JwtAccessTokenIssuer(SigningKey, Issuer, Audience, Lifetime);

        var validating = () => issuer.Validate(token);

        validating.Should().Throw<SecurityTokenException>();
    }

    private static string FlipOneCharacter(string segment)
    {
        var chars = segment.ToCharArray();
        var index = chars.Length / 2;
        chars[index] = chars[index] == 'A' ? 'B' : 'A';
        return new string(chars);
    }
}
