#!/usr/bin/env bash
# Production build: publish the API to out/api and build the Next.js app.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet publish backend/Zhadina.Api -c Release -o out/api
(cd frontend && npm ci && npm run build)
