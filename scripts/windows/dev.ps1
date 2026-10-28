# Local development: API with hot reload on 127.0.0.1:5000, Next.js dev server on :3000.
# If either process exits (or you press Ctrl+C), stop the other.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# The API migrates on startup, so the database must be up first. It stays running afterwards (db.ps1 stop).
& (Join-Path $PSScriptRoot 'db.ps1') start
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$procs = @(
    Start-Process dotnet -ArgumentList 'watch', '--project', 'backend/Zhadina.Api', '--no-hot-reload', 'run', '--launch-profile', 'http' `
        -WorkingDirectory $root -NoNewWindow -PassThru
    Start-Process npm.cmd -ArgumentList 'run', 'dev', '--', '-H', '0.0.0.0', '-p', '3000' `
        -WorkingDirectory (Join-Path $root 'frontend') -NoNewWindow -PassThru
)

try {
    while (-not ($procs | Where-Object HasExited)) { Start-Sleep -Milliseconds 500 }
} finally {
    # taskkill /T stops the whole tree (npm -> node, dotnet watch -> API); Stop-Process would orphan the children.
    foreach ($p in $procs) { if (-not $p.HasExited) { taskkill /T /F /PID $p.Id | Out-Null } }
}
