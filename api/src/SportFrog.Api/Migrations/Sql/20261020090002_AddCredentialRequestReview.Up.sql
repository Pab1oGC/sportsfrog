-- =============================================================================
-- AddCredentialRequestReview
--
-- The platform organization's half of a credential request: reviewing it
-- and approving or rejecting it. RequestCredentialRequest and
-- CancelCredentialRequest only ever act inside the asking organization's
-- own isolation context; approving one acts inside a DIFFERENT organization
-- than the caller's own, which tenant_isolation exists specifically to
-- refuse. Reading the queue has the same problem at a wider scale: it has
-- to see every organization's pending request at once, and no single
-- current_org answers that.
--
-- Both are solved the way resolve_public_competition and
-- resolve_public_document already solve the same shape of problem: a
-- narrow SECURITY DEFINER function, together with a policy that applies to
-- nobody but the schema owner those functions run as (sportfrog_owner is
-- not a role any public- or application-facing connection string
-- authenticates as -- see AllowSchemaOwnerThroughTenantIsolation). The
-- actual approval or rejection never runs through these: it happens inside
-- OrganizationJobScope, under the request's own organization, through the
-- ordinary tenant_isolation policy sportfrog_app already holds there.
-- =============================================================================

ALTER TABLE credential_requests
    ADD COLUMN reviewed_by      uuid REFERENCES users(id),
    ADD COLUMN reviewed_at      timestamptz,
    ADD COLUMN rejection_reason text,
    ADD COLUMN batch_id         uuid REFERENCES document_batches(id) ON DELETE SET NULL;

COMMENT ON COLUMN credential_requests.batch_id IS
  'The batch approving this request raised. Set to null if the batch record
   is ever removed: the request still happened, and losing the batch is not
   a reason to lose the record that it was approved.';

CREATE POLICY platform_review ON credential_requests
    FOR SELECT TO sportfrog_owner
    USING (true);

COMMENT ON POLICY platform_review ON credential_requests IS
  'Lets the two functions below see across every organization. Scoped to
   sportfrog_owner alone, which only they run as -- nothing public- or
   application-facing ever authenticates that way (see
   AllowSchemaOwnerThroughTenantIsolation). tenant_isolation itself is
   untouched and still governs sportfrog_app exactly as before.';

CREATE OR REPLACE FUNCTION read_pending_credential_requests()
RETURNS TABLE (
    id             uuid,
    org_id         uuid,
    org_name       text,
    org_slug       text,
    competition_id uuid,
    category_id    uuid,
    team_id        uuid,
    valid_from     date,
    valid_to       date,
    requested_by   uuid,
    created_at     timestamptz
)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT
        r.id, r.org_id, o.name, o.slug::text,
        r.competition_id, r.category_id, r.team_id,
        r.valid_from, r.valid_to, r.requested_by, r.created_at
    FROM credential_requests r
    JOIN organizations o ON o.id = r.org_id
    WHERE r.status = 'pending'
    ORDER BY r.created_at;
$fn$;

REVOKE ALL ON FUNCTION read_pending_credential_requests() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION read_pending_credential_requests() TO sportfrog_app;

-- Deliberately not joined to competitions for a name: competitions carries
-- its own tenant_isolation, and this function's platform_review exception is
-- scoped to credential_requests alone. Widening it onto another business
-- table is a decision of its own, not a side effect of this one.

CREATE OR REPLACE FUNCTION credential_request_org(p_id uuid)
RETURNS TABLE (org_id uuid)
LANGUAGE sql SECURITY DEFINER STABLE AS $fn$
    SELECT org_id FROM credential_requests WHERE id = p_id;
$fn$;

REVOKE ALL ON FUNCTION credential_request_org(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION credential_request_org(uuid) TO sportfrog_app;
