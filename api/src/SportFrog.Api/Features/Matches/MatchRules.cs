using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Matches;

/// <summary>The rules a match is played and read under.</summary>
/// <param name="Configuration">
/// The category's own ruleset where it has one, otherwise the competition's.
/// A category is allowed to vary the rules of its division, so the effective
/// one is the only one worth asking about.
/// </param>
internal sealed record MatchRules(Sport Sport, RulesetConfiguration Configuration);

/// <summary>Something a result says that its sport does not allow.</summary>
internal sealed record ResultViolation(string Property, string Message);
