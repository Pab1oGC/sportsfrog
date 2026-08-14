#!/usr/bin/env bash
# =============================================================================
# Creates a new migration: the C# class and its two SQL files.
#
# Replaces 'dotnet ef migrations add', which must NOT be used in this
# project: the schema is defined by the migrations' SQL, not by the EF model,
# so there is nothing to generate from the entities.
#
# The only mechanical work is keeping the timestamp in sync across four
# places -the class name, the [Migration] attribute, and the two .sql files
# the class references by string-. A mismatch there isn't caught by the
# compiler: it fails when the migration is applied. That's what this script
# takes care of.
#
# The .sql files are generated empty. The SQL itself is written by a person.
#
# Usage:
#   ./scripts/new-migration.sh AddClubPhone
#
# Works on Linux, macOS and on Windows with Git Bash.
# =============================================================================
set -euo pipefail

name="${1:-}"

if [[ -z "$name" ]]; then
    echo "Usage: $0 <MigrationName>" >&2
    echo "Example: $0 AddClubPhone" >&2
    exit 1
fi

# --- Validation ---------------------------------------------------------

if [[ ! "$name" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
    echo "Error: '$name' is not a valid C# identifier." >&2
    echo "Use PascalCase, no spaces or dashes (example: AddClubPhone)." >&2
    exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
migrations_dir="$script_dir/../src/SportFrog.Api/Migrations"
migrations_dir="$(cd "$migrations_dir" && pwd)"
sql_dir="$migrations_dir/Sql"

existing="$(find "$migrations_dir" -maxdepth 1 -name "*_$name.cs" -print -quit)"
if [[ -n "$existing" ]]; then
    echo "Error: a migration named '$name' already exists: $(basename "$existing")" >&2
    exit 1
fi

mkdir -p "$sql_dir"

# --- Names ----------------------------------------------------------------

# Same format EF Core uses. In UTC, so the ordering doesn't depend on the
# time zone of whoever created the migration.
timestamp="$(date -u +%Y%m%d%H%M%S)"
id="${timestamp}_${name}"

class_path="$migrations_dir/$id.cs"
up_path="$sql_dir/$id.Up.sql"
down_path="$sql_dir/$id.Down.sql"

# --- Templates --------------------------------------------------------------

cat > "$class_path" <<EOF
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// TODO: describe in one line what this migration changes and why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("$id")]
public sealed class $name : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("$id.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("$id.Down.sql"));
}
EOF

cat > "$up_path" <<EOF
-- =============================================================================
-- $name
--
-- TODO: describe the change.
--
-- If this migration creates a business table with org_id, don't forget
-- isolation, grants and the updated_at trigger:
--
--   CREATE TABLE example ( ... org_id uuid NOT NULL REFERENCES organizations(id) ... );
--
--   ALTER TABLE example ENABLE ROW LEVEL SECURITY;
--   ALTER TABLE example FORCE ROW LEVEL SECURITY;
--   CREATE POLICY tenant_isolation ON example
--       USING (org_id = current_org_id())
--       WITH CHECK (org_id = current_org_id());
--
--   GRANT SELECT, INSERT, UPDATE, DELETE ON example TO sportfrog_app;
--   -- and GRANT SELECT ... TO sportfrog_public only if the public view needs it
--
--   CREATE TRIGGER trg_example_touch BEFORE UPDATE ON example
--       FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
--
-- A new column doesn't need any of the above.
-- =============================================================================


EOF

cat > "$down_path" <<EOF
-- =============================================================================
-- $name - rollback
--
-- TODO: undo exactly what the .Up.sql file does, in reverse order.
--
-- Not optional: it's what lets a failed deployment be undone without
-- recreating the database. Worth testing by rolling back and reapplying.
-- =============================================================================


EOF

# --- Result -------------------------------------------------------------

echo ""
echo "Migration '$id' created:"
echo "  src/SportFrog.Api/Migrations/$id.cs"
echo "  src/SportFrog.Api/Migrations/Sql/$id.Up.sql"
echo "  src/SportFrog.Api/Migrations/Sql/$id.Down.sql"
echo ""
echo "Next step: write the SQL in the two .sql files and apply with"
echo "  dotnet ef database update --project src/SportFrog.Api"
echo ""
echo "The .sql files embed themselves into the assembly: the .csproj already picks them up by wildcard."
echo ""
