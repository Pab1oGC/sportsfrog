#!/usr/bin/env bash
set -euo pipefail

DEPLOY_PATH="/home/deployuser/sportfrog"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"

ssh pasantes "mkdir -p ${DEPLOY_PATH}"
ssh pasantes "mkdir -p ${DEPLOY_PATH}/docker/postgres/init"

scp "${REPO_DIR}/api/docker-compose.yml" pasantes:/tmp/docker-compose.yml
scp "${REPO_DIR}/api/.env.example" pasantes:/tmp/.env

# Copiar scripts SQL init para Postgres
scp "${REPO_DIR}/api/docker/postgres/init/01-roles.sql" pasantes:${DEPLOY_PATH}/docker/postgres/init/
ssh pasantes "chown postgres:postgres ${DEPLOY_PATH}/docker/postgres/init/01-roles.sql"

mv /tmp/docker-compose.yml ${DEPLOY_PATH}/
mv /tmp/.env ${DEPLOY_PATH}/.env
chown -R deployuser:deployuser ${DEPLOY_PATH}
chmod 600 ${DEPLOY_PATH}/.env
