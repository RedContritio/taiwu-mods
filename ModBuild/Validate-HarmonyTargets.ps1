param(
    [switch]$IncludeDrafts,
    [string]$TargetsPath = (Join-Path $PSScriptRoot "harmony-targets.json"),
    [string]$DecompiledDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "_decompiled")
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

if (-not (Test-Path $TargetsPath)) {
    throw "Harmony target manifest not found: $TargetsPath"
}
if (-not (Test-Path $DecompiledDir)) {
    throw "Decompiled source directory not found: $DecompiledDir"
}

$releaseNames = @(Get-ModEntries -IncludeDrafts:$IncludeDrafts | ForEach-Object { $_.name })
$manifest = Get-Content $TargetsPath -Raw | ConvertFrom-Json
$decompiledText = Get-ChildItem $DecompiledDir -Filter "*.cs" -Recurse | ForEach-Object {
    Get-Content $_.FullName -Raw
} | Out-String

$hasErrors = $false
foreach ($target in @($manifest.targets)) {
    if ($releaseNames -notcontains $target.mod) {
        continue
    }

    Write-Host "[$($target.mod)] Harmony targets"
    foreach ($pattern in @($target.patterns)) {
        if ($decompiledText.Contains($pattern)) {
            Write-Host "  OK: $pattern"
        } else {
            $hasErrors = $true
            Write-Host "  ERROR: missing $pattern" -ForegroundColor Red
        }
    }
}

if ($hasErrors) {
    exit 1
}
