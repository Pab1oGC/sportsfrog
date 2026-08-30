-- =============================================================================
-- AddQualifiersPerGroup
--
-- How many teams a group sends forward, as the organizers declared it —
-- "top 2", "top 4" — not as it was later carried out. PromoteGroupStage
-- still takes its own count at promotion time and is not driven by this
-- column: a promotion can use "best thirds" or an uneven count across
-- groups, which this single number per category cannot express.
--
-- What this column is for is narrower and public-facing: the standings
-- table, both the organizers' and the published one, reads it to highlight
-- the qualifying rows of an ordinary "top N per group" category while the
-- group stage is still being played, before anyone has promoted anything.
-- Null means the rule was never declared, or the format is not Groups, and
-- either way nothing is highlighted.
-- =============================================================================

ALTER TABLE categories ADD COLUMN qualifiers_per_group smallint;

COMMENT ON COLUMN categories.qualifiers_per_group IS
  'How many of each group advance, as organizers declared it up front — for
   highlighting the qualifying rows on the standings table. Not read by
   PromoteGroupStage, which takes its own count at promotion time and can
   differ from this (best-third wildcards, uneven counts per group).';
