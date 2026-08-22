-- =============================================================================
-- AddCurrentUserContext - rollback
--
-- Removes the policy first and the function it calls second: a policy whose
-- expression referenced a missing function would make the table unreadable.
--
-- Reverting this leaves signing in unable to discover an account's
-- memberships, which is the state this migration exists to leave behind.
-- =============================================================================

DROP POLICY IF EXISTS own_memberships ON organization_memberships;

DROP FUNCTION IF EXISTS current_user_id();
