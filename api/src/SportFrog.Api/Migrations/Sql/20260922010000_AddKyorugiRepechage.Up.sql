-- =============================================================================
-- AddKyorugiRepechage
--
-- Kyorugi does not play a third-place match: it runs a repechage instead.
-- Once the final's two competitors are known, whoever lost to either of them
-- on the way there has the right to fight again, for one of two bronze
-- medals rather than a single third place. See SportFrog.Domain.Scheduling.
-- Repechage for the ladder itself and Features/Draw/DrawRepechage.cs for the
-- operation that draws it.
--
-- categories.uses_repechage is the organizer's own switch, off by default so
-- a plain knockout with no third place keeps behaving exactly as it always
-- has. Not restricted to a particular sport at the schema level, the same
-- choice qualifiers_per_group already made: DrawRepechage is where this is
-- actually enforced, and a category that never turns it on simply never has
-- that endpoint do anything.
--
-- matches.is_repechage tells a repechage ladder's own matches apart from the
-- category's single run at the title. Both still carry a non-null phase and
-- still resolve through BracketWinnerPropagation exactly like any other
-- bracket match; this flag exists only for the two readers that scan every
-- phased match of a category assuming they all belong to one progression
-- toward one final -- AdvanceBracket's current-round lookup and the public
-- page's champion lookup -- which is exactly the assumption a bronze ladder
-- running alongside the final would otherwise break.
-- =============================================================================

ALTER TABLE categories ADD COLUMN uses_repechage boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN categories.uses_repechage IS
  'Whether this category''s knockout settles third place by repechage -- two
   bronze medals, fought for by whoever the two finalists beat along the way
   -- instead of no third place at all. The organizer''s own switch; see
   SportFrog.Domain.Scheduling.Repechage.';

ALTER TABLE matches ADD COLUMN is_repechage boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN matches.is_repechage IS
  'Whether this fixture belongs to a repechage ladder rather than the
   category''s own run at the title. Still resolves through
   BracketWinnerPropagation like any other bracket match; exists only so
   AdvanceBracket and the public champion lookup can exclude it from the
   single progression toward one final they each assume.';
