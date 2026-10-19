-- =============================================================================
-- AddPublishedIssuedDocuments
--
-- The policy ScopePublicReadsToPublishedCompetitions named and deliberately
-- left undone: that migration narrowed issued_documents' tenant_isolation to
-- sportfrog_app, because VerifyDocument (public credential verification) was
-- not mapped in Program.cs at the time and so needed no public read at all
-- yet — but "narrow tenant_isolation" and "add the published_* replacement"
-- are two different steps, and only the first one happened for this table.
-- Its own comment named this exactly: "it will simply see nothing until a
-- published_issued_documents-shaped policy is added for it". Re-enabling the
-- route since then (see Program.cs) without this migration means every scan
-- resolves the serial through resolve_public_document() and then reads zero
-- rows back — empty, not an error, which is the correct failure mode that
-- comment asked for, but still not a working endpoint.
--
-- Shaped exactly like published_matches and its siblings: visible once it
-- traces back to a competition that is public and not deleted. Deliberately
-- not "org_id = current_org_id()" the way tenant_isolation itself reads —
-- that is the exact shape ScopePublicReadsToPublishedCompetitions existed to
-- close, because any session authenticated as sportfrog_public can call
-- set_config('app.current_org', ...) itself with no permission check at all,
-- and a policy keyed on that session variable would let it list every
-- document of any organization it names, not merely resolve the one serial
-- resolve_public_document already vetted. Keying on the competition's own
-- is_public column instead ties this to a fact about the row, not a value
-- the connecting session controls.
--
-- No column on issued_documents needs excluding the way deleted_at is
-- excluded on other published_* tables: the entity's own comment is that it
-- "does not support deletion, physical or logical" at all.
-- =============================================================================

CREATE POLICY published_issued_documents ON issued_documents
    FOR SELECT TO sportfrog_public
    USING (
        EXISTS (
            SELECT 1
            FROM competitions c
            WHERE c.id = issued_documents.competition_id
              AND c.is_public
              AND c.deleted_at IS NULL
        )
    );

COMMENT ON POLICY published_issued_documents ON issued_documents IS
  'Lets a published competition''s own issued documents be read by
   sportfrog_public, the same shape and the same reasoning published_matches
   already has. See this migration''s own header for why this is keyed on
   the competition''s is_public rather than on current_org_id().';
