#!/usr/bin/env bash
# Local development: API with hot reload on 127.0.0.1:5000, Next.js dev server on :3000.
set -euo pipefail
cd "$(dirname "$0")/.."

trap 'kill 0' EXIT

dotnet watch --project backend/Zhadina.Api --no-hot-reload run --launch-profile http &
(cd frontend && npm run dev -- -H 0.0.0.0 -p 3000) &

wait -n
