#!/bin/bash
# =============================================================================
# Creates the three SportFrog roles. Runs once, when the data volume is empty.
#
#   sportfrog_owner  - schema owner. Applies migrations. The only role that
#                      creates, alters and drops objects.
#   sportfrog_app    - the application. Reads and writes data, always under an
#                      organization context set with SET LOCAL. Cannot modify
#                      the schema or bypass row-level security policies.
#   sportfrog_public - the anonymous public view. Read-only, enforced by the
#                      engine and not by application code.
#
# The grants for the last two are issued by the InitialSchema migration,
# because they only make sense once the tables exist. This script only
# creates the roles and sets their passwords.
# =============================================================================
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE ROLE ${SPORTFROG_OWNER_USER} LOGIN PASSWORD '${SPORTFROG_OWNER_PASSWORD}';
    CREATE ROLE sportfrog_app          LOGIN PASSWORD '${SPORTFROG_APP_PASSWORD}';
    CREATE ROLE sportfrog_public       LOGIN PASSWORD '${SPORTFROG_PUBLIC_PASSWORD}';

    -- Extensions are installed by the container superuser, so the schema
    -- owner doesn't need that privilege. The migration declares them with
    -- IF NOT EXISTS and finds them already resolved here.
    CREATE EXTENSION IF NOT EXISTS pgcrypto;
    CREATE EXTENSION IF NOT EXISTS citext;

    -- The database and schema become the owner's: otherwise the tables would
    -- end up owned by the container superuser.
    ALTER DATABASE ${POSTGRES_DB} OWNER TO ${SPORTFROG_OWNER_USER};
    ALTER SCHEMA public OWNER TO ${SPORTFROG_OWNER_USER};

    -- No one else creates objects in public.
    REVOKE CREATE ON SCHEMA public FROM PUBLIC;
EOSQL
