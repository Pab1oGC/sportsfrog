-- Only the organization: the membership cascades with it (see
-- organization_memberships.org_id ON DELETE CASCADE). The user row is left
-- alone on purpose -- users are soft-deleted everywhere else in this schema
-- because a user can be the recorded author of results, documents and audit
-- log entries, and a migration rollback is not the place to violate that.
DELETE FROM organizations WHERE slug = 'frogtech-solutions';
