-- =============================================================================
-- AddSanctionEntryGate
--
-- One boolean, reachable by sportfrog_public: whether a person is currently
-- blocked from entering on the strength of an unresolved sanction.
--
-- The public verification endpoint (resolve_public_document,
-- Features.Public.VerifyDocument) answers "is this card valid" to anybody on
-- the internet, with no organization context of their own -- RNF-16 already
-- refuses that endpoint every disciplinary detail, including why a document
-- was revoked. A sanction is the same kind of detail, which is exactly why
-- sportfrog_public was never granted anything on sanctions, sanction_payments
-- or matches (see those tables' own migrations). This function exists so the
-- public answer can still say "no habilitada para ingresar" -- the one fact a
-- steward at a gate actually needs from a QR code -- without that grant ever
-- being given: SECURITY DEFINER runs it as the schema owner, and the caller
-- gets a bare true/false back, nothing it could read a reason out of.
--
-- The condition mirrors SportFrog.Domain.Discipline.SanctionLedger.Resolve
-- exactly, reduced to the one question this needs answered:
--
--   not served = (rule wants matches AND not enough of them played)
--             OR (rule wants a fine AND it is neither waived nor paid off)
--
-- which, because a rule that does not ask for a term makes that term's half
-- of the OR false on its own, needs no branch per rule the way the C# does.
-- A sanction already voided or lifted never blocks, matching Resolve's own
-- precedence that both outrank the terms entirely.
--
-- This is deliberately the only place that condition is ever written twice.
-- Keeping the two in sync by hand is the price of answering this from SQL at
-- all; if SanctionLedger.Resolve's own rule ever changes, this function has
-- to change with it, and there is nothing beyond this comment to remind
-- whoever makes that change to come back here.
-- =============================================================================

-- sanctions and sanction_payments did not exist when
-- AllowSchemaOwnerThroughTenantIsolation added sportfrog_owner back to every
-- isolated table's tenant_isolation policy for exactly this reason: a
-- SECURITY DEFINER function executes as the schema owner, and FORCE ROW
-- LEVEL SECURITY (set on both tables) means even that owner needs a policy
-- that actually names it, or it reads zero rows back the moment it queries
-- past app.current_org -- the same failure mode that migration's own header
-- describes resolve_public_competition() and resolve_public_document()
-- hitting first. sanction_blocks_entry below is the third SECURITY DEFINER
-- function this schema has, and matches (which it also reads) already
-- carries the fix; these two tables are the ones that were missing it.
ALTER POLICY tenant_isolation ON sanctions TO sportfrog_app, sportfrog_owner;
ALTER POLICY tenant_isolation ON sanction_payments TO sportfrog_app, sportfrog_owner;

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
  'Whether a person has an unresolved sanction for a competition -- see this
   migration''s own header for why this exists as a bare boolean instead of
   a grant on sanctions itself, and for the risk of drift against
   SanctionLedger.Resolve that comes with writing the same rule twice.';

REVOKE ALL ON FUNCTION sanction_blocks_entry(uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION sanction_blocks_entry(uuid, uuid) TO sportfrog_public;

-- Deliberately not granted to sportfrog_app: an authenticated read already
-- has its own grant on sanctions, sanction_payments and matches, and reaches
-- SanctionLedger.Resolve directly for the richer answer (why, how much, how
-- many matches remain) this boolean was never meant to give.
