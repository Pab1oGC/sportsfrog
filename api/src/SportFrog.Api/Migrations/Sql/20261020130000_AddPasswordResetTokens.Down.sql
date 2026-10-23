-- =============================================================================
-- AddPasswordResetTokens - rollback
-- =============================================================================

DROP TRIGGER IF EXISTS trg_password_reset_tokens_touch ON password_reset_tokens;

DROP TABLE IF EXISTS password_reset_tokens;
