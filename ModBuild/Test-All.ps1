param(
    [switch]$IncludeDrafts,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

& "$PSScriptRoot\Build-All.ps1" -Configuration $Configuration -IncludeDrafts:$IncludeDrafts
& "$PSScriptRoot\Validate-HarmonyTargets.ps1" -IncludeDrafts:$IncludeDrafts

dotnet run --project (Join-Path $Script:ModBuildRoot "tests\TaiwuMods.Tests\TaiwuMods.Tests.csproj") -c $Configuration

$testPackageRoot = Join-Path $Script:ModBuildRoot "artifacts\test-package"
New-Item -ItemType Directory -Force -Path $testPackageRoot | Out-Null

foreach ($entry in (Get-ModEntries -IncludeDrafts:$IncludeDrafts)) {
    $destination = Join-Path $testPackageRoot $entry.name
    Copy-ModFiles -Entry $entry -Destination $destination -Clean | Out-Null

    $expectedConfig = Join-Path $destination "config.lua"
    $expectedSettings = Join-Path $destination "Settings.Lua"
    $expectedPlugins = Join-Path $destination "Plugins"
    if (-not (Test-Path $expectedConfig) -or -not (Test-Path $expectedSettings) -or -not (Test-Path $expectedPlugins)) {
        throw "Packaged layout is incomplete for $($entry.name)"
    }

    $forbiddenFiles = @(Get-ChildItem $destination -Recurse -File | Where-Object {
        $_.Extension -in @(".cs", ".csproj", ".sln", ".pdb") -or
        $_.FullName -match "\\(bin|obj|\.git)\\"
    })
    if ($forbiddenFiles.Count -gt 0) {
        $list = $forbiddenFiles | ForEach-Object { $_.FullName }
        throw "Packaged layout contains development files for $($entry.name):`n - $($list -join "`n - ")"
    }
}

Write-Host "All automated build/package checks passed."
