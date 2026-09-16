<#
.SYNOPSIS
    Lists text files that are not valid UTF-8, e.g. files saved as Windows-1252.

.PARAMETER Root
    Folder to scan. Defaults to the solution root (parent of _Helpers).

.PARAMETER Fix
    Converts the listed files from Windows-1252 to UTF-8 without BOM.
    Only use it after checking the list, the conversion assumes Windows-1252.

.EXAMPLE
    ./_Helpers/Check-Encoding.ps1
    ./_Helpers/Check-Encoding.ps1 -Fix
#>
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$Fix
)

$extensions = @('.cs', '.razor', '.cshtml', '.css', '.js', '.json', '.csproj', '.props', '.md', '.sql', '.sh', '.bat', '.ps1')
$excluded = '[\\/](bin|obj|node_modules|\.git|\.vs)[\\/]'

# Throws on invalid byte sequences instead of replacing them
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if ($Fix) {
    # Needed on PowerShell 7 (.NET) for code page 1252, harmless on Windows PowerShell
    try { [System.Text.Encoding]::RegisterProvider([System.Text.CodePagesEncodingProvider]::Instance) } catch { }
    $windows1252 = [System.Text.Encoding]::GetEncoding(1252)
}

$invalid = @()

Get-ChildItem -Path $Root -Recurse -File |
    Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() -and $_.FullName -notmatch $excluded } |
    ForEach-Object {
        $file = $_
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)

        try {
            [void]$strictUtf8.GetString($bytes)
        }
        catch {
            $invalid += $file.FullName
        }
    }

if ($invalid.Count -eq 0) {
    Write-Output "All files are valid UTF-8."
    exit 0
}

foreach ($path in $invalid) {
    if ($Fix) {
        $text = $windows1252.GetString([System.IO.File]::ReadAllBytes($path))
        [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
        Write-Output "Converted: $path"
    }
    else {
        Write-Output "Not UTF-8: $path"
    }
}

if (-not $Fix) { exit 1 }
