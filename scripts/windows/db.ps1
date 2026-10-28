# Local PostgreSQL for development, kept entirely in <repo>\.postgres (gitignored):
# binaries in pgsql\, data in data\, log in server.log. Trust auth on localhost:5432, matching
# the connection strings in appsettings.Development.json and the tests. Delete data\ to start over.
#   db.ps1 [start]   download + initialise on first run, then start (no-op if already running)
#   db.ps1 stop
#   db.ps1 status
param([ValidateSet('start', 'stop', 'status')][string]$Action = 'start')

$ErrorActionPreference = 'Stop'
$pgRoot = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path '.postgres'
$bin = Join-Path $pgRoot 'pgsql\bin'
$data = Join-Path $pgRoot 'data'
$log = Join-Path $pgRoot 'server.log'

# EDB's portable Windows build. Only bin, lib and share are needed to run a server; the rest is pgAdmin, docs and headers.
$version = '17.11-5'
$url = "https://get.enterprisedb.com/postgresql/postgresql-$version-windows-x64-binaries.zip"
$sha256 = '80379B2C04D51C30225532E0AE04509899141E9957ED096FE749D7FD9DF8F82F'

function Install-Postgres {
    New-Item -ItemType Directory -Force $pgRoot | Out-Null
    $zip = Join-Path $pgRoot "postgresql-$version.zip"
    if (-not (Test-Path $zip)) {
        Write-Host "Downloading PostgreSQL $version (~380 MB)..."
        $ProgressPreference = 'SilentlyContinue'  # the progress bar makes Invoke-WebRequest many times slower in Windows PowerShell
        Invoke-WebRequest $url -OutFile $zip -UseBasicParsing
    }
    if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $sha256) {
        Remove-Item $zip
        throw "Checksum mismatch for $url; deleted the download, run again to retry."
    }

    Write-Host 'Extracting...'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -notmatch '^pgsql/(bin|lib|share)/' -or $entry.Name -eq '') { continue }
            $target = Join-Path $pgRoot $entry.FullName
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally {
        $archive.Dispose()
    }
    Remove-Item $zip
}

$pgCtl = Join-Path $bin 'pg_ctl.exe'

switch ($Action) {
    'start' {
        if (-not (Test-Path $pgCtl)) { Install-Postgres }
        if (-not (Test-Path $data)) {
            & (Join-Path $bin 'initdb.exe') -D $data -U postgres -A trust -E UTF8 --locale=C --no-instructions
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
        & $pgCtl status -D $data | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Host 'PostgreSQL is already running.'; exit 0 }
        & $pgCtl start -D $data -l $log -w
        if ($LASTEXITCODE -ne 0) { Write-Host "PostgreSQL failed to start; see $log"; exit $LASTEXITCODE }
    }
    'stop' { & $pgCtl stop -D $data -m fast; exit $LASTEXITCODE }
    'status' { & $pgCtl status -D $data; exit $LASTEXITCODE }
}
