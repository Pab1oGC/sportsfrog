-- =============================================================================
-- AddMembershipScopes
--
-- Authorization today is one scalar per membership: a role, ranked and
-- compared with MembershipRoleAuthority.Reaches(), that applies to the whole
-- organization a membership belongs to. That is correct for an operator who
-- runs every competition in a league, but it has no way to say "operator,
-- only in this competition" or "operator, only for this club's own roster" --
-- both real cases (an event's own administrator, a club's delegate) that a
-- single ranked role cannot express no matter where it sits on the scale:
-- the authority a scope grants is not more or less than an unscoped role, it
-- is the same role answering a narrower question.
--
-- This table is that narrower question, kept deliberately independent of
-- the rank in membership_role: a membership with no rows here is unaffected
-- and reaches its role everywhere in the organization, exactly as before
-- this migration. A membership WITH rows reaches that role only inside the
-- competitions or clubs those rows name. Nothing reads this table yet, so
-- applying this migration changes nothing observable until rows are written
-- and the endpoints that must consult it are updated to do so.
--
-- One row names exactly one target, enforced by ck_membership_scope_target
-- the same way issued_documents.ck_revocation already ties a status to a
-- nullable column: scope_type decides which of competition_id/club_id is the
-- one that must be set, and the other must be null. A membership that should
-- reach several competitions gets several rows, not a wider one.
-- =============================================================================

CREATE TYPE membership_scope_type AS ENUM ('competition', 'club');

CREATE TABLE membership_scopes (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id         uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,

    -- The membership this scope narrows. Cascades: a scope row has no
    -- meaning once the membership it qualifies is gone.
    membership_id  uuid NOT NULL REFERENCES organization_memberships(id) ON DELETE CASCADE,

    scope_type     membership_scope_type NOT NULL,

    -- Matches categories.competition_id and document_batches.competition_id:
    -- a competition is withdrawn by logical delete (DeleteCompetition), never
    -- dropped outright, so this reference is never actually exercised in
    -- practice -- it stays CASCADE only for consistency with its siblings.
    competition_id uuid REFERENCES competitions(id) ON DELETE CASCADE,

    -- Matches teams.club_id: a club is never physically removed either (its
    -- own comment: "a club that ever competed has history hanging off it"),
    -- so this is exercised even less than the one above.
    club_id        uuid REFERENCES clubs(id) ON DELETE RESTRICT,

    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_membership_scope_target CHECK (
        (scope_type = 'competition') = (competition_id IS NOT NULL)
        AND (scope_type = 'club') = (club_id IS NOT NULL)
    )
);

COMMENT ON TABLE membership_scopes IS
  'Narrows a membership''s role to specific competitions or clubs. Absence,
   not presence, is the common case: most memberships have no rows here and
   reach their role across the whole organization, unchanged from before
   this table existed.';

COMMENT ON COLUMN membership_scopes.scope_type IS
  'Which of competition_id/club_id is the one this row sets -- see
   ck_membership_scope_target. Not inferred from which column is non-null,
   so a row can never exist in the ambiguous state of setting neither or
   both.';

-- One row per (membership, target): a membership scoped twice to the same
-- competition would be meaningless to reach() and impossible to remove
-- unambiguously by target. Partial, because scope_type (and so the
-- irrelevant column) varies per row -- a plain unique index across both FK
-- columns would allow the same competition_id to repeat behind different
-- club_id nulls.
CREATE UNIQUE INDEX uq_membership_scope_competition
    ON membership_scopes(membership_id, competition_id)
    WHERE scope_type = 'competition';

CREATE UNIQUE INDEX uq_membership_scope_club
    ON membership_scopes(membership_id, club_id)
    WHERE scope_type = 'club';

-- The lookup the authorization check actually runs: every scope a
-- membership holds, regardless of type. The two partial indexes above
-- enforce uniqueness; they don't serve an unfiltered scan across both types.
CREATE INDEX idx_membership_scopes_membership ON membership_scopes(membership_id);

ALTER TABLE membership_scopes ENABLE ROW LEVEL SECURITY;
ALTER TABLE membership_scopes FORCE ROW LEVEL SECURITY;

-- Scoped to sportfrog_app only, not to {public} (the default a bare CREATE
-- POLICY would take): this table IS an authorization decision, never a
-- public-portal concern, and never read through a SECURITY DEFINER function
-- the way resolve_public_competition()/resolve_public_document() read
-- competitions -- so it needs none of the roles those two reach for.
CREATE POLICY tenant_isolation ON membership_scopes
    TO sportfrog_app
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON membership_scopes TO sportfrog_app;

CREATE TRIGGER trg_membership_scopes_touch BEFORE UPDATE ON membership_scopes
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
