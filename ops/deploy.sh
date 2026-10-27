#!/usr/bin/env bash
# Deploys what is on main (3.0.0): pulls the code, builds the image of its version (t212ext-web:<version>),
# restarts on it, and tags it "latest", so a later `docker compose up -d` (a changed .env) keeps this version.
# The three newest versions' images stay, for a rollback without a build (docs/OPERATIONS.md, section 2); older
# ones, dangling layers and build cache older than a week go, as in traceon's deploy. Volumes are not touched.
# Usage: cd ~/t212ext && ./ops/deploy.sh
set -Eeuo pipefail

cd "$(dirname "$0")/.."
# The pull can bring a new copy of this script, and bash would go on with the old one it had already
# opened: once pulled, the new copy runs from the top.
if [ "${T212EXT_DEPLOY_PULLED:-}" != 1 ]; then
  git pull --ff-only
  T212EXT_DEPLOY_PULLED=1 exec ./ops/deploy.sh "$@"
fi

VERSION="$(tr -d '[:space:]' < VERSION)"
export T212EXT_VERSION="$VERSION"
echo "Deploying $VERSION"

docker compose build t212ext-web
docker compose up -d
docker tag "t212ext-web:$VERSION" t212ext-web:latest

for tag in $(docker images t212ext-web --format '{{.Tag}}' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | sort -rV | tail -n +4); do
  docker rmi "t212ext-web:$tag" > /dev/null || true
done
docker image prune -f > /dev/null
docker builder prune -f --filter "until=168h" > /dev/null

echo "Images kept: $(docker images t212ext-web --format '{{.Tag}}' | grep -E '^[0-9]+\.' | sort -rV | tr '\n' ' ')"
echo "Disk: $(df -h --output=pcent / | tail -n1 | tr -d ' ') used."
echo "T212Ext $VERSION is running."
