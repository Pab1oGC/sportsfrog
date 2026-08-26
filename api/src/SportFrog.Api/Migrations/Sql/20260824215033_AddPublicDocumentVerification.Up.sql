-- =============================================================================
-- AddPublicDocumentVerification
--
-- Lets somebody holding a credential find out whether it is real, without
-- signing in and without being told anything the card does not already say.
-- Requirement RF-45.
--
-- The shape is the one the public competition view already uses: the address
-- carries slugs, no organization context exists yet, and a function owned by
-- the schema owner resolves the pair and establishes the context so every
-- read after it goes through row-level security normally. Anonymous access
-- becomes compatible with the isolation policies rather than an exception to
-- them.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Serials, compared the way a person types them
--
-- The code is printed grouped in fours and read off a card by somebody
-- standing at the side of a pitch. They will type it in lower case, or without
-- the hyphens, or with a space where a hyphen was. All of those are the same
-- code, so the comparison ignores everything that is not a letter or a digit
-- and ignores case — and this index is what keeps that comparison an index
-- lookup rather than a scan of every credential the organization ever issued.
-- -----------------------------------------------------------------------------

CREATE INDEX idx_issued_serial_lookup ON issued_documents (
    org_id,
    upper(regexp_replace(serial_number, '[^A-Za-z0-9]', '', 'g'))
);

-- -----------------------------------------------------------------------------
-- resolve_public_document
--
-- Deliberately does NOT require the competition to be published. A credential
-- is valid because it was issued, not because the league chose to publish its
-- standings; a referee checking a card on a Sunday morning should not be told
-- the card does not exist because nobody ticked a box in the settings.
--
-- It resolves and establishes the context. It does not decide whether the
-- document is valid — that is a reading, and readings go through the policies
-- like everything else.
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION resolve_public_document(p_org_slug citext, p_serial text)
RETURNS TABLE (org_id uuid, document_id uuid)
LANGUAGE plpgsql SECURITY DEFINER VOLATILE AS $fn$
DECLARE
    v_org_id      uuid;
    v_document_id uuid;
    v_serial      text;
BEGIN
    v_serial := upper(regexp_replace(coalesce(p_serial, ''), '[^A-Za-z0-9]', '', 'g'));

    IF v_serial = '' THEN
        RETURN;
    END IF;

    SELECT o.id INTO v_org_id
    FROM organizations o
    WHERE o.slug = p_org_slug
      AND o.is_active
      AND o.deleted_at IS NULL;

    IF v_org_id IS NULL THEN
        RETURN;
    END IF;

    -- set_config with is_local = true is SET LOCAL: it lasts for this
    -- transaction and is gone before the connection returns to the pool.
    PERFORM set_config('app.current_org', v_org_id::text, true);

    SELECT d.id INTO v_document_id
    FROM issued_documents d
    WHERE d.org_id = v_org_id
      AND upper(regexp_replace(d.serial_number, '[^A-Za-z0-9]', '', 'g')) = v_serial;

    IF v_document_id IS NULL THEN
        PERFORM set_config('app.current_org', '', true);
        RETURN;
    END IF;

    -- A revoked document resolves. Saying "no such card" to a card that was
    -- withdrawn would be the one answer this endpoint must never give: the
    -- whole reason it exists is to be able to say that a real serial is no
    -- longer valid.
    org_id := v_org_id;
    document_id := v_document_id;

    RETURN NEXT;
END;
$fn$;

COMMENT ON FUNCTION resolve_public_document(citext, text) IS
  'Resolves an organization slug and a printed serial, and establishes the
   isolation context for the rest of the transaction. Owned by the schema
   owner because issued_documents carries an isolation policy and no context
   is established when it is called.

   Deliberately narrow: it answers whether the pair names a document and
   nothing about it. Everything else is read afterwards, under the policies.';

REVOKE ALL ON FUNCTION resolve_public_document(citext, text) FROM PUBLIC;

GRANT EXECUTE ON FUNCTION resolve_public_document(citext, text) TO sportfrog_public;
GRANT EXECUTE ON FUNCTION resolve_public_document(citext, text) TO sportfrog_app;

-- -----------------------------------------------------------------------------
-- No new table grants
--
-- Everything a verification names — the document, the person's name, the team,
-- the club, the division, the competition — is already readable by the public
-- role, because the public competition view needed exactly those tables. This
-- migration adds a way in and not a way to more.
--
-- What it deliberately does not add is document_templates. What a card looks
-- like is the organization's design and has nothing to do with whether a
-- serial is valid.
-- -----------------------------------------------------------------------------
