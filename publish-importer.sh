#!/usr/bin/env bash
# Reproducibly build the Plane->Waypoint importer. Output is gitignored (WAY-29):
# the 88MB publish tree must never be committed again — regenerate it here instead.
set -euo pipefail
cd "$(dirname "$0")"
dotnet publish src/Waypoint.Importer/Waypoint.Importer.csproj -c Release -o publish-importer "$@"
echo "Importer published to ./publish-importer (gitignored)."
