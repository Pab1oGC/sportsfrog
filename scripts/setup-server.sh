#!/usr/bin/env bash
set -euo pipefail

DEPLOY_PATH="/home/deployuser/sportfrog"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"

sudo mkdir -p "$DEPLOY_PATH"

sudo cp "$REPO_DIR/api/docker-compose.yml" "$DEPLOY_PATH/"
sudo cp "$REPO_DIR/api/.env.example" "$DEPLOY_PATH/.env"

sudo chown -R deployuser:deployuser "$DEPLOY_PATH"
sudo chmod 600 "$DEPLOY_PATH/.env"

echo "Setup complete. Edit $DEPLOY_PATH/.env before deploying."
