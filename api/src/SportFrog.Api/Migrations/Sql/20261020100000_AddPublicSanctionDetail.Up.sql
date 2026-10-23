-- =============================================================================
-- AddPublicSanctionDetail
--
-- Widens what sportfrog_public can learn about an unresolved sanction,
-- past the bare boolean sanction_blocks_entry (AddSanctionEntryGate)
-- already answers. This is a deliberate reversal of that migration's own
-- stance, not a bug fix: product now wants a steward (or a parent, or
-- anybody else holding the printed card) to see WHICH kind of sanction is
-- blocking entry -- by matches, by a fine, or both, how many matches are
-- left, what fine balance remains, whether the physical credential is
-- withheld -- from the public QR itself, with no session.
--
-- What stays withheld, on purpose, is everything that migration's own RNF-16
-- reasoning still protects: the free-text `reason` a sanction carries (the
-- actual disciplinary story -- "agredió al árbitro", say), and anything that
-- would require a grant on sanctions/sanction_payments/matches beyond what a
-- SECURITY DEFINER function hands back. The split mirrors
-- Features.Documents.ScanCredential's own ActiveSanction versus this one:
-- same shape, minus Id, AthleteId and Reason.
--
-- public_active_sanctions is the one place the resolution condition is
-- computed -- sanction_blocks_entry is redefined below to call it instead of
-- repeating its own copy of the same WHERE clause, so there is exactly one
-- SQL version of "is this sanction still owed" to keep in sync against
-- SportFrog.Domain.Discipline.SanctionLedger.Resolve, not two.
-- =============================================================================

CREATE OR REPLACE FUNCTION public_active_sanctions(p_athlete_id uuid, p_competition_id uuid)
RETURNS TABLE (
    completion_rule sanction_completion_rule,
    matches_remaining int,
    fine_balance numeric,
    credential_withheld boolean
)
LANGUAGE sql STABLE SECURITY DEFINER AS $fn$
    SELECT
        s.completion_rule,
        CASE WHEN s.completion_rule IN ('matches', 'both')
            THEN GREATEST(0, s.matches_required - served.matches_served)
        END,
        CASE WHEN s.completion_rule IN ('fine', 'both')
            THEN CASE WHEN s.fine_waived_at IS NOT NULL THEN 0 ELSE s.fine_amount - paid.fine_paid END
        END,
        s.credential_withheld_at IS NOT NULL
    FROM sanctions s
    CROSS JOIN LATERAL (
        SELECT count(*) AS matches_served
        FROM matches m
        WHERE m.competition_id = s.competition_id
          AND m.status IN ('finished', 'walkover')
          AND m.scheduled_at IS NOT NULL
          AND m.scheduled_at >= s.imposed_at
          AND (m.home_team_id = s.team_id OR m.away_team_id = s.team_id)
    ) served
    CROSS JOIN LATERAL (
        SELECT COALESCE(sum(p.amount), 0) AS fine_paid
        FROM sanction_payments p
        WHERE p.sanction_id = s.id
          AND p.voided_at IS NULL
          AND p.review_state <> 'rejected'
    ) paid
    WHERE s.athlete_id = p_athlete_id
      AND s.competition_id = p_competition_id
      AND s.voided_at IS NULL
      AND s.lifted_at IS NULL
      AND (
          (s.completion_rule IN ('matches', 'both') AND s.matches_required > served.matches_served)
          OR
          (s.completion_rule IN ('fine', 'both') AND s.fine_waived_at IS NULL AND s.fine_amount > paid.fine_paid)
      )
$fn$;

COMMENT ON FUNCTION public_active_sanctions(uuid, uuid) IS
  'Every sanction still owed by a person for a competition, reduced to what
   sportfrog_public may see -- never the reason, never an id. See this
   migration''s own header for why, and for the drift risk against
   SanctionLedger.Resolve that comes with computing this in SQL at all.';

REVOKE ALL ON FUNCTION public_active_sanctions(uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public_active_sanctions(uuid, uuid) TO sportfrog_public;

-- Deliberately not granted to sportfrog_app: an authenticated read reaches
-- ActiveSanctions/SanctionLedger.Resolve directly for the richer answer
-- (reason, id, athlete name) this function was never meant to give.

CREATE OR REPLACE FUNCTION sanction_blocks_entry(p_athlete_id uuid, p_competition_id uuid)
RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER AS $fn$
    SELECT EXISTS (SELECT 1 FROM public_active_sanctions(p_athlete_id, p_competition_id))
$fn$;

COMMENT ON FUNCTION sanction_blocks_entry(uuid, uuid) IS
  'Whether a person has an unresolved sanction for a competition -- now a
   thin wrapper over public_active_sanctions (AddPublicSanctionDetail) so
   the resolution condition itself lives in exactly one SQL function, not
   copied between the two.';
