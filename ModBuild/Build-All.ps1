param(
    [switch]$IncludeDrafts,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

$entries = Get-ModEntries -IncludeDrafts:$IncludeDrafts
foreach ($entry in $entries) {
    Write-Host "Building $($entry.name) [$($entry.status)]"
    foreach ($project in @($entry.projects)) {
        $projectPath = Resolve-RepoPath $project
        dotnet build $projectPath -c $Configuration -v:minimal
    }
}

foreach ($entry in $entries) {
    $validation = Assert-ModIsValid -Entry $entry -RequireBuiltPlugins
    foreach ($warning in $validation.Warnings) {
        Write-Warning "[$($entry.name)] $warning"
    }
}

Write-Host "Build and validation completed."
