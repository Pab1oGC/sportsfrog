using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Seeds the one organization and account allowed to register new
/// organizations — see <see cref="PlatformAuthority"/> and
/// <see cref="Features.Organizations.RegisterOrganization"/>, which this
/// migration exists to make possible at all on a fresh database: without an
/// account already holding a role there, nobody could ever pass the check
/// that now gates creating the first one.
/// </summary>
/// <remarks>
/// Unlike every other migration, this one does not hand its SQL to
/// <see cref="EmbeddedSql"/> verbatim: the account's password never belongs
/// in a file that ships with the source, seeded or not. The embedded file
/// carries two placeholders instead of real values, filled in here from
/// <see cref="EmailEnvironmentVariable"/> and
/// <see cref="PasswordEnvironmentVariable"/> — read the exact way
/// <see cref="SportFrogDbContextFactory"/> already reads the migrations
/// connection string, because both run in the same process at the same
/// time, and for the same reason: a secret an operator sets, not one this
/// project ships a default for.
/// </remarks>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260917020000_SeedPlatformOwner")]
public sealed class SeedPlatformOwner : Migration
{
    internal const string EmailEnvironmentVariable = "PLATFORM_ADMIN_EMAIL";
    internal const string PasswordEnvironmentVariable = "PLATFORM_ADMIN_PASSWORD";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var rawEmail = RequireEnvironmentVariable(EmailEnvironmentVariable);
        var rawPassword = RequireEnvironmentVariable(PasswordEnvironmentVariable);

        if (!Email.TryParse(rawEmail, out var email))
        {
            throw new InvalidOperationException(
                $"{EmailEnvironmentVariable} is not a valid email address.");
        }

        if (!Password.TryParse(rawPassword, out _))
        {
            throw new InvalidOperationException(
                $"{PasswordEnvironmentVariable} does not meet the platform's password policy " +
                $"(at least {Password.MinimumLength} characters, with an uppercase letter, a " +
                "lowercase letter, a digit and a symbol).");
        }

        // Same hasher and cost the running application would use — a
        // migration-time login and one created moments later through
        // AddMember must be indistinguishable in the database.
        var passwordHash = new BCryptPasswordHasher().Hash(rawPassword);

        var sql = EmbeddedSql.Read("20260917020000_SeedPlatformOwner.Up.sql")
            .Replace("{{EMAIL}}", EscapeLiteral(email.Value))
            .Replace("{{PASSWORD_HASH}}", EscapeLiteral(passwordHash));

        migrationBuilder.Sql(sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917020000_SeedPlatformOwner.Down.sql"));

    private static string RequireEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();

        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException(
                $"Set {name} before applying migrations — the platform's one " +
                "organization-creating account has no default to fall back to.")
            : value;
    }

    /// <summary>
    /// Single quotes doubled, the standard SQL string escape. Neither value
    /// is literal source — the address is operator input and the hash is
    /// BCrypt output — so this is not decorative.
    /// </summary>
    private static string EscapeLiteral(string value) => value.Replace("'", "''");
}
