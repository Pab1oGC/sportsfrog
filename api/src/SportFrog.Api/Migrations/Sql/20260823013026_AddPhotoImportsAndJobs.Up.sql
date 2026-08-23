-- =============================================================================
-- AddPhotoImportsAndJobs
--
-- Two things the system has never had: work that outlives a request, and a
-- record of it.
--
-- A batch of photographs is the first genuinely slow thing here. Each one is
-- decoded, turned upright, resized, re-encoded and uploaded; four hundred of
-- them is minutes, not milliseconds, and no browser waits that long. So the
-- upload is accepted, the archive is put away, and the work happens
-- afterwards — which means there has to be a row saying it is happening, and
-- what became of it.
-- =============================================================================

CREATE TYPE photo_import_state AS ENUM ('queued', 'running', 'finished', 'failed');

CREATE TABLE photo_imports (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id        uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    file_name     text        NOT NULL,
    archive_key   text        NOT NULL,
    status        photo_import_state NOT NULL DEFAULT 'queued',
    total         integer     NOT NULL DEFAULT 0,
    matched       integer     NOT NULL DEFAULT 0,
    failed        integer     NOT NULL DEFAULT 0,
    results       jsonb       NOT NULL DEFAULT '[]'::jsonb,
    failure       text,
    requested_by  uuid NOT NULL REFERENCES users(id),
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    finished_at   timestamptz
);

CREATE INDEX idx_photo_imports_org ON photo_imports(org_id, created_at DESC);

COMMENT ON COLUMN photo_imports.archive_key IS
  'The uploaded archive, in object storage under the organization''s prefix.
   Kept rather than discarded once the batch finishes: it is the evidence of
   what was actually sent when somebody asks why a photograph did not arrive,
   and it is what a re-run would read.';

COMMENT ON COLUMN photo_imports.results IS
  $comment$What became of each file in the archive, one entry per file:
[
  { "file": "12345678.jpg", "outcome": "attached",   "athlete": "..." },
  { "file": "IMG_2831.jpg", "outcome": "unmatched" },
  { "file": "roto.png",     "outcome": "unreadable" }
]
Names the file rather than only counting it, because "seven failed" tells an
operator nothing they can act on and "IMG_2831.jpg matched nobody" tells them
exactly which file to rename.$comment$;

COMMENT ON COLUMN photo_imports.failure IS
  'Why the batch as a whole could not be processed — a corrupt archive, an
   unreachable bucket. Distinct from a file that failed: those are in
   results, and the batch still finished.';

ALTER TABLE photo_imports ENABLE ROW LEVEL SECURITY;
ALTER TABLE photo_imports FORCE ROW LEVEL SECURITY;

-- The same isolation as everything else, and it matters more here than
-- usual: a background job runs with no request behind it, so this policy is
-- the only thing standing between a job and another organization's batch.
CREATE POLICY tenant_isolation ON photo_imports
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON photo_imports TO sportfrog_app;

-- Nothing here is public. A batch of children's photographs is not something
-- the anonymous view has any business reading (RNF-16).

CREATE TRIGGER trg_photo_imports_touch BEFORE UPDATE ON photo_imports
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- =============================================================================
-- Background jobs
--
-- Hangfire keeps its queue in this same database, in its own schema.
--
-- The application user is given USAGE and CREATE there and nowhere else, so
-- the job library can build and migrate its own tables while remaining unable
-- to touch a single business table's definition. The alternative — writing
-- Hangfire's ten tables and their indexes by hand in a migration — would pin
-- this project to one version of somebody else's internal schema and break
-- silently on the first upgrade.
--
-- The public role is given nothing at all. The queue is not readable by the
-- anonymous view under any circumstances: job arguments carry identifiers,
-- and eventually the arguments of a document batch.
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS hangfire AUTHORIZATION sportfrog_owner;

GRANT USAGE, CREATE ON SCHEMA hangfire TO sportfrog_app;

COMMENT ON SCHEMA hangfire IS
  'Background job queue. Owned by the schema owner, writable and extendable
   by the application user, invisible to the public one. Not part of the
   business schema: nothing here is backed up or restored with the league
   data, because a queue of work in flight is not a fact about a competition.';
