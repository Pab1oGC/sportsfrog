namespace SportFrog.Api.Features.Rulebook;

/// <summary>Something a configuration says that its sport does not allow.</summary>
internal sealed record RulesetViolation(string Property, string Message);
