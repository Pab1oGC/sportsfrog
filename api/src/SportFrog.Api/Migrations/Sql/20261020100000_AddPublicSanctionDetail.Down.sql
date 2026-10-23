-- =============================================================================
-- AddPublicSanctionDetail - rollback
--
-- Restores sanction_blocks_entry to the self-contained body
-- AddSanctionEntryGate originally gave it, so dropping
-- public_active_sanctions below does not leave it dangling.
-- =============================================================================

CREATE OR REPLACE FUNCTION sanction_blocks_entry(p_athlete_id uuid, p_competition_id uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER AS $fn$
    SELECT EXISTS (
        SELECT 1
        FROM sanctions s
        WHERE s.athlete_id = p_athlete_id
          AND s.competition_id = p_competition_id
          AND s.voided_at IS NULL
          AND s.lifted_at IS NULL
          AND (
              (
                  s.completion_rule IN ('matches', 'both')
                  AND s.matches_required > (
                      SELECT count(*)
                      FROM matches m
                      WHERE m.competition_id = s.competition_id
                        AND m.status IN ('finished', 'walkover')
                        AND m.scheduled_at IS NOT NULL
                        AND m.scheduled_at >= s.imposed_at
                        AND (m.home_team_id = s.team_id OR m.away_team_id = s.team_id)
                  )
              )
              OR
              (
                  s.completion_rule IN ('fine', 'both')
                  AND s.fine_waived_at IS NULL
                  AND s.fine_amount > COALESCE((
                      SELECT sum(p.amount)
                      FROM sanction_payments p
                      WHERE p.sanction_id = s.id
                        AND p.voided_at IS NULL
                        AND p.review_state <> 'rejected'
                  ), 0)
              )
          )
    )
$fn$;

COMMENT ON FUNCTION sanction_blocks_entry(uuid, uuid) IS
  'Whether a person has an unresolved sanction for a competition -- see
   AddSanctionEntryGate''s own header for why this exists as a bare boolean
   instead of a grant on sanctions itself, and for the risk of drift against
   SanctionLedger.Resolve that comes with writing the same rule twice.';

DROP FUNCTION IF EXISTS public_active_sanctions(uuid, uuid);
