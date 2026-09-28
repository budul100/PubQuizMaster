<#
.SYNOPSIS
    Restores a server backup (plain-SQL pg_dump, .sql.gz) into the local PostgreSQL
    Windows service for development.

.DESCRIPTION
    - Lists available backups from the locally synced HiDrive folder (newest first)
    - Checks dump integrity and required extensions BEFORE touching the local database
    - Drops and recreates the local database, creates or updates the login role
    - Restores the dump as superuser, so extensions and ownership work as on the server

.PARAMETER BackupFile
    Path to a specific .sql.gz or .sql file. Skips the selection menu.

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    .\Database-Restore_PubQuizMaster.ps1

.EXAMPLE
    .\Database-Restore_PubQuizMaster.ps1 -BackupFile D:\Temp\pubquizmaster_20260928_030000.sql.gz -Force
#>
[CmdletBinding()]
param(
    [string] $BackupFile,
    [switch] $Force
)

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Must match the file prefix used by the server backup script
$AppName = 'pubquizmaster'

# Local database; name and owner must match the server, because the dump contains OWNER TO statements
$DbName     = 'pubquizmaster'
$DbUser     = 'pubquizmaster'
$DbPassword = 'pubquizmaster'

$DbHost    = 'localhost'
$DbPort    = 5432
$SuperUser = 'postgres'

# Locally synced HiDrive folder (%HiDrive% points to the HiDrive user root)
$BackupDir = "$env:HiDrive\Backups\pubquizmaster"

# Optional explicit path to psql.exe; otherwise PATH and Program Files are searched
$PsqlPath = $null

# ---------------------------------------------------------------------------
# Implementation
# ---------------------------------------------------------------------------

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-Psql {
    if ($PsqlPath) {
        if (-not (Test-Path $PsqlPath)) { throw "psql not found at '$PsqlPath'." }
        return $PsqlPath
    }

    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # Pick the highest installed major version
    $candidate = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Directory.Parent.Name -replace '\D', '') } -Descending |
        Select-Object -First 1

    if (-not $candidate) { throw 'psql.exe not found. Set $PsqlPath in the configuration block.' }
    return $candidate.FullName
}

function Invoke-Psql {
    param(
        [Parameter(Mandatory)] [string] $Database,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    # -X: ignore psqlrc, -q: suppress command tags, ON_ERROR_STOP: abort on first error
    & $script:Psql -X -q -h $DbHost -p $DbPort -U $SuperUser -d $Database -v ON_ERROR_STOP=1 @Arguments
    if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE." }
}

function Expand-GzipFile {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $Destination
    )

    $inStream = [System.IO.File]::OpenRead($Source)
    try {
        $gzip = New-Object System.IO.Compression.GZipStream($inStream, [System.IO.Compression.CompressionMode]::Decompress)
        try {
            $outStream = [System.IO.File]::Create($Destination)
            try { $gzip.CopyTo($outStream) } finally { $outStream.Dispose() }
        }
        finally { $gzip.Dispose() }
    }
    finally { $inStream.Dispose() }
}

function Select-Backup {
    param(
        [Parameter(Mandatory)] [string] $Directory,
        [Parameter(Mandatory)] [string] $Filter
    )

    if (-not (Test-Path $Directory)) { throw "Backup directory not found: $Directory" }

    # Timestamps in file names sort lexicographically, newest first
    $files = @(Get-ChildItem -Path $Directory -Filter $Filter -File | Sort-Object Name -Descending)
    if ($files.Count -eq 0) { throw "No backups matching '$Filter' found in '$Directory'." }

    Write-Host ''
    Write-Host 'Available DB backups:'
    for ($i = 0; $i -lt $files.Count; $i++) {
        Write-Host ('{0,4}  {1}  ({2:N1} MB)' -f ($i + 1), $files[$i].Name, ($files[$i].Length / 1MB))
    }
    Write-Host ''

    while ($true) {
        $choice = Read-Host 'Select backup [1]'
        if ([string]::IsNullOrWhiteSpace($choice)) { return $files[0].FullName }

        $index = 0
        if ([int]::TryParse($choice, [ref] $index) -and $index -ge 1 -and $index -le $files.Count) {
            return $files[$index - 1].FullName
        }
        Write-Host "Please enter a number between 1 and $($files.Count)."
    }
}

function Get-RequiredExtensions {
    param([Parameter(Mandatory)] [string] $SqlFile)

    Select-String -Path $SqlFile -Pattern '^CREATE EXTENSION IF NOT EXISTS "?(\w+)"?' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Sort-Object -Unique
}

# --- Resolve input files ----------------------------------------------------

if (-not $env:HiDrive -and -not $BackupFile) {
    throw 'Environment variable HiDrive is not set. Pass -BackupFile explicitly.'
}

$dbFile = if ($BackupFile) { (Resolve-Path $BackupFile).Path } else { Select-Backup -Directory $BackupDir -Filter "${AppName}_*.sql.gz" }

$script:Psql = Resolve-Psql
$previousPgPassword = $env:PGPASSWORD
$tempSqlFile = $null

try {
    Write-Host ''
    Write-Host "Using $(& $script:Psql --version)"

    if (-not $env:PGPASSWORD) {
        $secure = Read-Host "Password for PostgreSQL superuser '$SuperUser'" -AsSecureString
        $env:PGPASSWORD = (New-Object System.Net.NetworkCredential('', $secure)).Password
    }

    # --- Prepare and validate dump (nothing is changed locally yet) ---------

    if ($dbFile -like '*.gz') {
        Write-Host '=== Decompressing dump ==='
        $tempSqlFile = Join-Path ([System.IO.Path]::GetTempPath()) ("{0}_{1}.sql" -f $AppName, [guid]::NewGuid())
        Expand-GzipFile -Source $dbFile -Destination $tempSqlFile
        $sqlFile = $tempSqlFile
    }
    else {
        $sqlFile = $dbFile
    }

    $tail = Get-Content -Path $sqlFile -Tail 5
    if (-not ($tail -match 'PostgreSQL database dump complete')) {
        throw 'Dump file looks incomplete (end marker missing).'
    }

    Write-Host '=== Checking required extensions ==='
    foreach ($extension in @(Get-RequiredExtensions -SqlFile $sqlFile)) {
        $available = Invoke-Psql -Database 'postgres' -Arguments @(
            '-t', '-A', '-c', "SELECT count(*) FROM pg_available_extensions WHERE name = '$extension';")
        if ([int]$available -eq 0) {
            throw "Extension '$extension' is required by the dump but not installed on the local PostgreSQL server."
        }
        Write-Host "    ${extension}: available"
    }

    # --- Confirm ----------------------------------------------------------------

    if (-not $Force) {
        Write-Host ''
        Write-Host "DB backup: $(Split-Path $dbFile -Leaf)"
        Write-Host ''
        Write-Host "WARNING: Database '$DbName' on ${DbHost}:$DbPort will be DROPPED and recreated."

        $answer = Read-Host 'Continue? (y/N)'
        if ($answer -notin @('y', 'Y', 'yes')) {
            Write-Host 'Aborted.'
            return
        }
    }

    # --- Restore database -------------------------------------------------------

    Write-Host "=== Dropping database '$DbName' (if exists) ==="
    Invoke-Psql -Database 'postgres' -Arguments @('-c', "DROP DATABASE IF EXISTS $DbName WITH (FORCE);")

    Write-Host "=== Creating or updating role '$DbUser' ==="
    $escapedPassword = $DbPassword -replace "'", "''"
    $roleSql = "DO `$`$ BEGIN " +
        "IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$DbUser') THEN " +
        "CREATE ROLE $DbUser LOGIN PASSWORD '$escapedPassword'; " +
        "ELSE ALTER ROLE $DbUser LOGIN PASSWORD '$escapedPassword'; " +
        "END IF; END `$`$;"
    Invoke-Psql -Database 'postgres' -Arguments @('-c', $roleSql)

    Write-Host "=== Creating database '$DbName' ==="
    Invoke-Psql -Database 'postgres' -Arguments @('-c', "CREATE DATABASE $DbName OWNER $DbUser;")

    Write-Host '=== Restoring dump ==='
    Invoke-Psql -Database $DbName -Arguments @('-f', $sqlFile)

    $tableCount = Invoke-Psql -Database $DbName -Arguments @(
        '-t', '-A', '-c', "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';")
    Write-Host "    $tableCount tables in public schema"

    Write-Host ''
    Write-Host "=== Done: $(Split-Path $dbFile -Leaf) ==="
    Write-Host "Connection string: Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword"
    Write-Host 'Note: run "dotnet ef database update" to verify the migration state.'
}
finally {
    if ($tempSqlFile -and (Test-Path $tempSqlFile)) { Remove-Item $tempSqlFile -Force }
    $env:PGPASSWORD = $previousPgPassword
}
