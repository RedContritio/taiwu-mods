param(
    [Parameter(Mandatory)]
    [string]$ModName,
    [switch]$IncludeDrafts,
    [switch]$IncludeSymbols,
    [string]$Configuration = "Release",
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\workshop"
}

$entry = Get-ModEntry -ModName $ModName -IncludeDrafts:$IncludeDrafts
foreach ($project in @($entry.projects)) {
    dotnet build (Resolve-RepoPath $project) -c $Configuration -v:minimal
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$packageDir = Join-Path $OutputDir $entry.name
Copy-ModFiles -Entry $entry -Destination $packageDir -Clean -IncludeSymbols:$IncludeSymbols | Out-Null

$zipPath = Join-Path $OutputDir "$($entry.name).zip"
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force
Write-Host "Package directory: $packageDir"
Write-Host "Package archive:    $zipPath"
