# Production build: publish the API to out/api and build the Next.js app.
$ErrorActionPreference = 'Stop'

# Native commands don't throw on failure in Windows PowerShell, so check each exit code.
function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Push-Location (Join-Path $PSScriptRoot '..\..')
try {
    Invoke-Native { dotnet publish backend/Zhadina.Api -c Release -o out/api }
    Set-Location frontend
    Invoke-Native { npm ci }
    Invoke-Native { npm run build }
} finally {
    Pop-Location
}
