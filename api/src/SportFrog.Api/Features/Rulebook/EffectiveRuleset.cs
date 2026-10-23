namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Which reglamento actually applies: a stage's own override where it is
/// playing under one, its category's where that one has one, the
/// competition's otherwise.
/// </summary>
/// <remarks>
/// Written in six places before the two-level overload existed —
/// <c>MatchRulesLookup</c>, <c>EventPolicy</c>, <c>StandingsQuery</c>,
/// <c>LeadersQuery</c>, <c>ReadCategories</c>, <c>ScheduleCalendar</c> —
/// each as its own <c>category.RulesetId ?? competition.RulesetId</c>, with
/// its own copy of the same one-line comment explaining it. One rule, asked
/// the same way everywhere a category's own answer and its competition's
/// are both in scope.
///
/// The three-level overload is for the two callers that know which stage a
/// particular match is actually playing: <c>MatchRulesLookup</c> and
/// <c>EventPolicy</c>, both resolving the reglamento of one fixture rather
/// than of a category in general. This is the level a league that plays its
/// group stage to three sets and its final to five finally has to declare
/// that in — see <c>Infrastructure.Persistence.Entities.CategoryStage.RulesetId</c>'s
/// own remarks for why nothing could say that before this existed, and why
/// <c>UpdateCategory</c>'s refusal to rewrite a reglamento once a competition
/// has played under it is a per-stage question now, not a per-category one.
/// </remarks>
internal static class EffectiveRuleset
{
    public static Guid Of(Guid? categoryRulesetId, Guid competitionRulesetId) =>
        categoryRulesetId ?? competitionRulesetId;

    public static Guid Of(Guid? stageRulesetId, Guid? categoryRulesetId, Guid competitionRulesetId) =>
        stageRulesetId ?? categoryRulesetId ?? competitionRulesetId;
}
