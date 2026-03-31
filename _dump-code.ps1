$root        = "."
$extensions  = @("*.cs", "*.html", "*.axaml")
$maxChars    = 120000
$outputBase  = "project_dump"
$excludeDirs = @("bin", "obj")

$fileIndex   = 1
$currentSize = 0
$currentFile = "${outputBase}_${fileIndex}.txt"

Clear-Content $currentFile -ErrorAction SilentlyContinue

Get-ChildItem -Path $root -Recurse -Include $extensions | Where-Object {
    $path = $_.FullName
    -not ($excludeDirs | Where-Object { $path -match "\\$_\\" })
} | ForEach-Object {
    $header  = "// ===== FILE: $($_.FullName) =====`n"
    $content = (Get-Content $_.FullName -Raw) + "`n`n"
    $block   = $header + $content
    $blockSize = $block.Length

    # Roll over to next file if needed
    if (($currentSize + $blockSize) -gt $maxChars -and $currentSize -gt 0) {
        $fileIndex++
        $currentFile = "${outputBase}_${fileIndex}.txt"
        Clear-Content $currentFile -ErrorAction SilentlyContinue
        $currentSize = 0
    }

    Add-Content $currentFile $block
    $currentSize += $blockSize
}

Write-Host "Done. $fileIndex file(s) created."
