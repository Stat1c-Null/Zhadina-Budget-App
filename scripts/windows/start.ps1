# Production: run the published API (private, loopback) and Next.js (public port).
# If either process exits, stop the other.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

$procs = @(
    Start-Process dotnet -ArgumentList 'out/api/Zhadina.Api.dll', '--urls', 'http://127.0.0.1:5000' `
        -WorkingDirectory $root -NoNewWindow -PassThru
    Start-Process npx.cmd -ArgumentList 'next', 'start', '-H', '0.0.0.0', '-p', '3000' `
        -WorkingDirectory (Join-Path $root 'frontend') -NoNewWindow -PassThru
)

try {
    while (-not ($procs | Where-Object HasExited)) { Start-Sleep -Milliseconds 500 }
} finally {
    # taskkill /T stops the whole tree (npx -> node); Stop-Process would orphan the children.
    foreach ($p in $procs) { if (-not $p.HasExited) { taskkill /T /F /PID $p.Id | Out-Null } }
}
