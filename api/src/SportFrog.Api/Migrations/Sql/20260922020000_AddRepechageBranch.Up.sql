-- =============================================================================
-- AddRepechageBranch
--
-- The public calendar grouped every phased match of a category by round
-- number alone (see ReadPublicCalendar / the client's agruparPorRonda), which
-- is only meaningful within a single progression toward one final. A
-- repechage ladder is two of those, running side by side, each numbered from
-- 1 within its own half (see Repechage.BuildHalf) -- so round 1 of one half
-- and round 1 of the other collided under the same heading, interleaved with
-- the main bracket's own rounds whenever the numbers happened to match.
--
-- repechage_branch says which of the final's two competitors' halves a
-- repechage match belongs to -- 1 for the home side of the final, 2 for the
-- away side -- set once, by DrawRepechage, at the moment each half is
-- actually built. That is the one place this is known for certain: the two
-- halves never share a competitor, so nothing else needs to trace the
-- bracket's source-match graph back out to rediscover it later.
-- =============================================================================

ALTER TABLE matches ADD COLUMN repechage_branch smallint;

ALTER TABLE matches
    ADD CONSTRAINT ck_repechage_branch_values
        CHECK (repechage_branch IS NULL OR repechage_branch IN (1, 2)),
    ADD CONSTRAINT ck_repechage_branch_only_on_repechage
        CHECK (repechage_branch IS NULL OR is_repechage);

COMMENT ON COLUMN matches.repechage_branch IS
  'Which of the final''s two halves this repechage match settles the bronze
   for -- 1 or 2, null for anything that is not a repechage match. Lets a
   reader put both halves in their own section instead of interleaving them
   by round_number, which is only comparable within one half.';
