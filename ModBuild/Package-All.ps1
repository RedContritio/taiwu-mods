param(
    [switch]$IncludeDrafts,
    [switch]$IncludeSymbols,
    [string]$Configuration = "Release",
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\workshop")
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

foreach ($entry in (Get-ModEntries -IncludeDrafts:$IncludeDrafts)) {
    & "$PSScriptRoot\Package-Mod.ps1" -ModName $entry.name -Configuration $Configuration -OutputDir $OutputDir -IncludeDrafts:$IncludeDrafts -IncludeSymbols:$IncludeSymbols
}
