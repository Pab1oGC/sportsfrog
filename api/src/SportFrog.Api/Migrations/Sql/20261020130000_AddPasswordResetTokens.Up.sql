-- =============================================================================
-- AddPasswordResetTokens
--
-- ChangePassword (RF-02) can only serve someone who still knows the current
-- password: it verifies it before it does anything. Whoever has forgotten it
-- has no way in at all, and this table is what gives them one — a single-use
-- proof, mailed to the address the account is registered under, that stands
-- in for the password they cannot produce.
--
-- Shaped after refresh_tokens, and for the same reasons: only the digest is
-- stored, so a database dump yields nothing that can be presented; and there
-- is no org_id, because a password belongs to a person and not to an
-- organization, so the table carries no isolation policy (see section 11 of
-- InitialSchema for which tables do and why).
--
-- Where it parts from refresh_tokens is the lifecycle. A renewal token is
-- revoked — something done to it, possibly in bulk, when a session must
-- stop working. This one is consumed: it buys exactly one password change
-- and is spent by it. consumed_at records both outcomes, since a token
-- dropped in favour of a freshly requested one is spent just as finally as
-- one that was actually redeemed.
-- =============================================================================

CREATE TABLE password_reset_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  text        NOT NULL UNIQUE,
    expires_at  timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

COMMENT ON COLUMN password_reset_tokens.token_hash IS
  'SHA-256 of the token, hex encoded, exactly as refresh_tokens.token_hash:
   a plain digest rather than a key derivation function, because the lookup
   is an equality match on a unique column that a per-token salt would make
   impossible, and the input is 256 random bits rather than something a
   person chose. See RefreshTokenFactory.Hash.';

COMMENT ON COLUMN password_reset_tokens.consumed_at IS
  'When the token stopped being redeemable, whether because it was redeemed
   or because a later request for the same account superseded it. NULL is the
   only state in which it can still be presented, and only until expires_at.';

-- The two queries this table answers. Redeeming one arrives by token and is
-- already served by the UNIQUE constraint on token_hash; issuing one has to
-- find the account's outstanding tokens to supersede them, which is this
-- partial index. Not unique: whether an account may hold more than one
-- pending token at a time is the issuing endpoint's rule to enforce, and
-- forbidding it here would turn a second, legitimate request into a database
-- error instead.
CREATE INDEX idx_password_reset_tokens_user_pending
    ON password_reset_tokens(user_id) WHERE consumed_at IS NULL;

-- Expiry is swept by the same query that redeems, so an abandoned token
-- needs no job to clean it up; this index is what makes deleting them in
-- bulk cheap if that ever becomes worth doing.
CREATE INDEX idx_password_reset_tokens_expires ON password_reset_tokens(expires_at);

-- InitialSchema's blanket GRANT covered only the tables that existed when it
-- ran, so every table added since names itself here.
GRANT SELECT, INSERT, UPDATE, DELETE ON password_reset_tokens TO sportfrog_app;

-- Deliberately nothing for sportfrog_public: the public role reads only what
-- the public site shows, and a reset token is the one thing in the schema
-- that is as good as a password.

CREATE TRIGGER trg_password_reset_tokens_touch BEFORE UPDATE ON password_reset_tokens
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
