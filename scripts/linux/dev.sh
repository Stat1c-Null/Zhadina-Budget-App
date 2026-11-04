#!/usr/bin/env bash
# Local development: API with hot reload on 127.0.0.1:5000, Next.js dev server on :3000.
set -euo pipefail
cd "$(dirname "$0")/../.."

# Stop early with a clear message, since a taken port otherwise surfaces as a long stack trace.
# This runs before the trap so that exiting here doesn't signal anything.
ports_in_use=()
for port in 5000 3000; do
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    ports_in_use+=("$port")
  fi
done

if ((${#ports_in_use[@]} > 0)); then
  echo "Can't start: port ${ports_in_use[*]} already in use. An earlier dev.sh may still be running." >&2
  for port in "${ports_in_use[@]}"; do
    echo "  See what holds port $port: fuser -v -n tcp $port" >&2
  done
  echo "  List dev servers: pgrep -af 'dotnet watch|next dev'" >&2
  echo "  A leftover dotnet watch can ignore a normal kill. Stop it with: kill -KILL <pids>" >&2
  exit 1
fi

trap 'kill 0' EXIT

dotnet watch --project backend/Zhadina.Api --no-hot-reload run --launch-profile http &
(cd frontend && npm run dev -- -H 0.0.0.0 -p 3000) &

wait -n
