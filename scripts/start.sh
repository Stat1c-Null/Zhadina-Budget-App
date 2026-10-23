#!/usr/bin/env bash
# Production: run the published API (private, loopback) and Next.js (public port).
# If either process exits, stop the other so the platform restarts the whole app.
set -euo pipefail
cd "$(dirname "$0")/.."

trap 'kill 0' EXIT

dotnet out/api/Zhadina.Api.dll --urls http://127.0.0.1:5000 &
(cd frontend && npx next start -H 0.0.0.0 -p 3000) &

wait -n
