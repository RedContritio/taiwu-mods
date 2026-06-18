param(
    [string]$ModName,
    [switch]$IncludeDrafts,
    [switch]$RequireBuiltPlugins
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\ModBuild.Common.ps1"

if ($ModName) {
    $entries = @(Get-ModEntry -ModName $ModName -IncludeDrafts:$IncludeDrafts)
} else {
    $entries = Get-ModEntries -IncludeDrafts:$IncludeDrafts
}

$hasErrors = $false
foreach ($entry in $entries) {
    $result = Test-ModStructure -Entry $entry -RequireBuiltPlugins:$RequireBuiltPlugins
    Write-Host "[$($result.Status)] $($result.Name)"

    foreach ($errorItem in $result.Errors) {
        $hasErrors = $true
        Write-Host "  ERROR: $errorItem" -ForegroundColor Red
    }
    foreach ($warningItem in $result.Warnings) {
        Write-Host "  WARN:  $warningItem" -ForegroundColor Yellow
    }
    if ($result.Errors.Count -eq 0 -and $result.Warnings.Count -eq 0) {
        Write-Host "  OK"
    }
}

if ($hasErrors) {
    exit 1
}
