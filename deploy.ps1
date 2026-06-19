param(
    [Parameter(Mandatory)]
    [string]$ModName,
    [string]$GameModDir = "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod",
    [switch]$IncludeDrafts,
    [switch]$IncludeSymbols,
    [switch]$Clean,
    [string]$Configuration = "Release",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild\ModBuild.Common.ps1"

$entry = Get-ModEntry -ModName $ModName -IncludeDrafts:$IncludeDrafts
$modDest = Join-Path $GameModDir $ModName

if (-not $NoBuild) {
    if (Test-ModAutoIncrementBuildVersion -Entry $entry) {
        $nextVersion = Update-ModBuildVersion -Entry $entry
        Write-Host "Bumped $($entry.name) Version -> $nextVersion"
    }

    foreach ($project in @($entry.projects)) {
        dotnet build (Resolve-RepoPath $project) -c $Configuration -v:minimal
    }
}

if ($Clean -and (Test-Path $modDest)) {
    if (-not (Test-Path $GameModDir)) {
        throw "Game mod directory not found: $GameModDir"
    }

    $resolvedGameModDir = (Resolve-Path $GameModDir).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedModDest = (Resolve-Path $modDest).Path
    $expectedPrefix = $resolvedGameModDir + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedModDest.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the game mod directory: $resolvedModDest"
    }

    Remove-Item -LiteralPath $resolvedModDest -Recurse -Force
}

Copy-ModFiles -Entry $entry -Destination $modDest -IncludeSymbols:$IncludeSymbols | Out-Null

Write-Host "Deployed '$ModName' -> $modDest"
