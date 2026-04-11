param(
    [string]$projectFilter = "",   # e.g. "MyApp.Core" — filters to that subfolder only
    [string[]]$excludeFiles = @()  # e.g. @("appsettings*.json", "*.Designer.cs")
)

$extensions  = @("*.sln", "*.csproj", "*.cs", "*.axaml", "*.html", "*.cshtml", "*.razor", "*.razor.css", "*.yml", "*.json")
$excludeDirs = @("bin", "obj", ".git", "node_modules")
$maxChars    = 120000

$root         = Split-Path $PSScriptRoot -Parent
$slnFile      = Get-ChildItem -Path $root -Filter "*.sln" -File | Select-Object -First 1
$solutionName = if ($slnFile) { [System.IO.Path]::GetFileNameWithoutExtension($slnFile.Name) } else { Split-Path $root -Leaf }

# Determine scan root — full solution or a single project subfolder
$scanRoot = if ($projectFilter -ne "") {
    Join-Path $root $projectFilter
} else {
    $root
}

if (-not (Test-Path $scanRoot)) {
    Write-Host "ERROR: Project folder not found: $scanRoot"
    exit 1
}

$outputLabel = if ($projectFilter -ne "") { "${solutionName}_${projectFilter}" } else { $solutionName }
$outputBase  = Join-Path $root "${outputLabel}_Code"

$allFiles = Get-ChildItem -Path $scanRoot -Recurse -Include $extensions | Where-Object {
    $file = $_
    $path = $file.FullName

    if ($excludeDirs | Where-Object { $path -match "\\$_\\" }) { return $false }
    if ($excludeFiles | Where-Object { $file.Name -like $_ }) { return $false }

    return $true
} | Sort-Object FullName

$totalFiles = $allFiles.Count
Write-Host "Found $totalFiles file(s) to process (scan root: $scanRoot)..."

# --- Collect all blocks ---
$allBlocks = [System.Collections.Generic.List[string]]::new()

$allFiles | ForEach-Object {
    $relativePath = $_.FullName.Substring($root.Length).TrimStart('\', '/')
    $header       = "## FILE: $relativePath`n`n``````$($_.Extension.TrimStart('.'))`n"
    $content      = (Get-Content $_.FullName -Raw -Encoding UTF8) + "`n``````" + "`n`n"
    $allBlocks.Add($header + $content)
}

# --- Determine total chunks ---
$tempSize  = 0
$tempIndex = 1
foreach ($block in $allBlocks) {
    if (($tempSize + $block.Length) -gt $maxChars -and $tempSize -gt 0) {
        $tempIndex++
        $tempSize = 0
    }
    $tempSize += $block.Length
}
$totalChunks = $tempIndex

# --- Preamble helper ---
function Get-Preamble {
    param([int]$ChunkIndex, [int]$TotalChunks, [string]$SolutionName)
    return @"
<!--
INSTRUCTIONS FOR AI:
- This is chunk $ChunkIndex of $TotalChunks of the '$SolutionName' codebase export.
- Do NOT start any analysis, summary, or response until ALL $TotalChunks chunks have been provided.
- After each chunk except the last, simply confirm receipt (e.g. "Chunk $ChunkIndex of $TotalChunks received. Please continue.").
- Begin your analysis only after the user explicitly confirms that all chunks have been uploaded.
-->

"@
}

# --- Write chunks ---
$fileIndex   = 1
$currentSize = 0
$currentFile = "${outputBase}_${fileIndex}.md"

$preamble = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $outputLabel
Set-Content $currentFile $preamble -Encoding UTF8
$currentSize = $preamble.Length

$filesProcessed = 0
foreach ($block in $allBlocks) {
    $filesProcessed++
    Write-Progress -Activity "Dumping project files" `
                   -Status "Writing block $filesProcessed / $totalFiles" `
                   -PercentComplete (($filesProcessed / $totalFiles) * 100)

    if (($currentSize + $block.Length) -gt $maxChars -and $currentSize -gt 0) {
        Write-Host "  -> Rolling over to chunk $($fileIndex + 1) (current: $currentSize chars)"
        $fileIndex++
        $currentFile = "${outputBase}_${fileIndex}.md"
        $preamble = Get-Preamble -ChunkIndex $fileIndex -TotalChunks $totalChunks -SolutionName $outputLabel
        Set-Content $currentFile $preamble -Encoding UTF8
        $currentSize = $preamble.Length
    }

    Add-Content $currentFile $block -Encoding UTF8
    $currentSize += $block.Length
}

Write-Progress -Activity "Dumping project files" -Completed
Write-Host "Done. $fileIndex chunk(s) created, $filesProcessed source files included."
