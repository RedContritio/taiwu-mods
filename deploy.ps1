param(
    [Parameter(Mandatory)]
    [string]$ModName,
    [string]$GameModDir = "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod",
    [switch]$IncludeDrafts,
    [switch]$IncludeSymbols
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild\ModBuild.Common.ps1"

$entry = Get-ModEntry -ModName $ModName -IncludeDrafts:$IncludeDrafts
$modDest = Join-Path $GameModDir $ModName
Copy-ModFiles -Entry $entry -Destination $modDest -IncludeSymbols:$IncludeSymbols | Out-Null

Write-Host "Deployed '$ModName' -> $modDest"
