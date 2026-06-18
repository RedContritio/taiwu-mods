$Script:ModBuildRoot = Split-Path $PSScriptRoot -Parent

function Get-ModManifest {
    param(
        [string]$ManifestPath = (Join-Path $PSScriptRoot "mods.json")
    )

    if (-not (Test-Path $ManifestPath)) {
        throw "Mod manifest not found: $ManifestPath"
    }

    return Get-Content $ManifestPath -Raw | ConvertFrom-Json
}

function Get-ModEntries {
    param(
        [switch]$IncludeDrafts,
        [string]$ManifestPath = (Join-Path $PSScriptRoot "mods.json")
    )

    $manifest = Get-ModManifest -ManifestPath $ManifestPath
    if ($IncludeDrafts) {
        return @($manifest.mods)
    }

    return @($manifest.mods | Where-Object { $_.status -eq "release" })
}

function Get-ModEntry {
    param(
        [Parameter(Mandatory)]
        [string]$ModName,
        [switch]$IncludeDrafts,
        [string]$ManifestPath = (Join-Path $PSScriptRoot "mods.json")
    )

    $entry = Get-ModEntries -IncludeDrafts:$IncludeDrafts -ManifestPath $ManifestPath |
        Where-Object { $_.name -eq $ModName } |
        Select-Object -First 1

    if (-not $entry) {
        throw "Mod '$ModName' is not listed in $ManifestPath. Use -IncludeDrafts for draft/sample mods."
    }

    return $entry
}

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return Join-Path $Script:ModBuildRoot ($Path -replace '/', [IO.Path]::DirectorySeparatorChar)
}

function Get-LuaQuotedValue {
    param(
        [Parameter(Mandatory)]
        [string]$Text,
        [Parameter(Mandatory)]
        [string]$Key
    )

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Key))\s*=\s*""([^""]*)""")
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

function Get-LuaBoolValue {
    param(
        [Parameter(Mandatory)]
        [string]$Text,
        [Parameter(Mandatory)]
        [string]$Key
    )

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Key))\s*=\s*(true|false)")
    if ($match.Success) {
        return [bool]::Parse($match.Groups[1].Value)
    }

    return $null
}

function Get-DeclaredPluginNames {
    param(
        [Parameter(Mandatory)]
        [string]$ConfigText
    )

    $plugins = New-Object System.Collections.ArrayList
    foreach ($blockName in @("FrontendPlugins", "BackendPlugins")) {
        $block = [regex]::Match($ConfigText, "(?s)$blockName\s*=\s*\{(?<body>.*?)\}")
        if ($block.Success) {
            foreach ($match in [regex]::Matches($block.Groups["body"].Value, '"([^"]+\.dll)"')) {
                [void]$plugins.Add($match.Groups[1].Value)
            }
        }
    }

    return @($plugins | Sort-Object -Unique)
}

function Get-DeclaredEventPackageNames {
    param(
        [Parameter(Mandatory)]
        [string]$ConfigText
    )

    $block = [regex]::Match($ConfigText, "(?s)EventPackages\s*=\s*\{(?<body>.*?)\}")
    if (-not $block.Success) {
        return @()
    }

    return @([regex]::Matches($block.Groups["body"].Value, '"([^"]+\.dll)"') | ForEach-Object {
        $_.Groups[1].Value
    } | Sort-Object -Unique)
}

function Test-ProjectHasEventPackage {
    param(
        [Parameter(Mandatory)]
        [array]$SourceFiles
    )

    foreach ($sourceFile in $SourceFiles) {
        $text = Get-Content $sourceFile.FullName -Raw
        if ($text -match ":\s*EventPackage\b") {
            return $true
        }
    }

    return $false
}

function Test-VersionCompatible {
    param(
        [Parameter(Mandatory)]
        [string]$ConfigVersion,
        [Parameter(Mandatory)]
        [string]$PluginVersion
    )

    $configParts = @($ConfigVersion.Split('.') | ForEach-Object { [int]$_ })
    $pluginParts = @($PluginVersion.Split('.') | ForEach-Object { [int]$_ })
    $max = [Math]::Max($configParts.Count, $pluginParts.Count)

    for ($i = 0; $i -lt $max; $i++) {
        $configPart = if ($i -lt $configParts.Count) { $configParts[$i] } else { 0 }
        $pluginPart = if ($i -lt $pluginParts.Count) { $pluginParts[$i] } else { 0 }
        if ($configPart -ne $pluginPart) {
            return $false
        }
    }

    return $true
}

function Get-ProjectSourceFiles {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    $projectDir = Split-Path $ProjectPath -Parent
    if (-not (Test-Path $projectDir)) {
        return @()
    }

    return @(Get-ChildItem $projectDir -Filter "*.cs" -Recurse | Where-Object {
        $_.FullName -notmatch "\\(bin|obj)\\"
    })
}

function Get-PluginConfigsFromSources {
    param(
        [Parameter(Mandatory)]
        [array]$SourceFiles
    )

    $configs = New-Object System.Collections.ArrayList
    foreach ($sourceFile in $SourceFiles) {
        $text = Get-Content $sourceFile.FullName -Raw
        foreach ($match in [regex]::Matches($text, 'PluginConfig\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)')) {
            [void]$configs.Add([PSCustomObject]@{
                File = $sourceFile.FullName
                ModId = $match.Groups[1].Value
                Author = $match.Groups[2].Value
                Version = $match.Groups[3].Value
            })
        }
    }

    return @($configs)
}

function Test-ModStructure {
    param(
        [Parameter(Mandatory)]
        $Entry,
        [switch]$RequireBuiltPlugins
    )

    $errors = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]
    $modDir = Join-Path $Script:ModBuildRoot $Entry.name
    $configPath = Join-Path $modDir "config.lua"
    $settingsPath = Join-Path $modDir "Settings.Lua"
    $readmePath = Join-Path $modDir "README.md"
    $pluginsDir = Join-Path $modDir "Plugins"

    if (-not (Test-Path $modDir)) {
        $errors.Add("Mod directory not found: $modDir")
    }
    if (-not (Test-Path $configPath)) {
        $errors.Add("Missing config.lua: $configPath")
    }
    if (-not (Test-Path $settingsPath)) {
        $errors.Add("Missing Settings.Lua: $settingsPath")
    }
    if (-not (Test-Path $readmePath)) {
        $warnings.Add("Missing README.md: $readmePath")
    }

    $declaredPlugins = @()
    $declaredEventPackages = @()
    $configVersion = $null
    if (Test-Path $configPath) {
        $configText = Get-Content $configPath -Raw
        foreach ($requiredString in @("Title", "Description", "Version", "Author")) {
            $value = Get-LuaQuotedValue -Text $configText -Key $requiredString
            if ([string]::IsNullOrWhiteSpace($value)) {
                $errors.Add("config.lua missing non-empty string field: $requiredString")
            }
            if ($requiredString -eq "Version") {
                $configVersion = $value
            }
        }

        foreach ($recommendedString in @("Cover", "GameVersion")) {
            $value = Get-LuaQuotedValue -Text $configText -Key $recommendedString
            if ([string]::IsNullOrWhiteSpace($value)) {
                $warnings.Add("config.lua has empty Workshop/display field: $recommendedString")
            }
        }

        foreach ($requiredNumberOrBool in @("Source", "Visibility")) {
            if (-not [regex]::IsMatch($configText, "(?m)^\s*$requiredNumberOrBool\s*=")) {
                $errors.Add("config.lua missing field: $requiredNumberOrBool")
            }
        }

        foreach ($requiredBool in @("HasArchive", "ChangeConfig", "NeedRestartWhenSettingChanged")) {
            if ($null -eq (Get-LuaBoolValue -Text $configText -Key $requiredBool)) {
                $errors.Add("config.lua missing boolean field: $requiredBool")
            }
        }

        if (-not [regex]::IsMatch($configText, "(?s)TagList\s*=\s*\{.*?\}")) {
            $errors.Add("config.lua missing TagList block")
        }

        if (-not [regex]::IsMatch($configText, "(?s)(FrontendPlugins|BackendPlugins)\s*=\s*\{.*?\}")) {
            $errors.Add("config.lua must declare FrontendPlugins or BackendPlugins")
        }

        $declaredPlugins = Get-DeclaredPluginNames -ConfigText $configText
        $declaredEventPackages = Get-DeclaredEventPackageNames -ConfigText $configText
        if ($declaredPlugins.Count -eq 0) {
            $errors.Add("config.lua declares no plugin dlls")
        }
    }

    if ($RequireBuiltPlugins -and $declaredPlugins.Count -gt 0) {
        if (-not (Test-Path $pluginsDir)) {
            $errors.Add("Plugins directory not found after build: $pluginsDir")
        } else {
            $actualDlls = @(Get-ChildItem $pluginsDir -Filter "*.dll" | ForEach-Object { $_.Name })
            foreach ($actualDll in $actualDlls) {
                if ($declaredPlugins -notcontains $actualDll) {
                    $warnings.Add("Plugin dll is present but not declared in config.lua: $actualDll")
                }
            }

            foreach ($plugin in $declaredPlugins) {
                $pluginPath = Join-Path $pluginsDir $plugin
                $depsPath = Join-Path $pluginsDir ([IO.Path]::ChangeExtension($plugin, ".deps.json"))
                if (-not (Test-Path $pluginPath)) {
                    $errors.Add("Declared plugin is missing: $pluginPath")
                }
                if (-not (Test-Path $depsPath)) {
                    $warnings.Add("Plugin deps file is missing: $depsPath")
                }
            }

            foreach ($eventPackage in $declaredEventPackages) {
                $eventPackagePath = Join-Path $modDir (Join-Path "Events\EventLib" $eventPackage)
                if (-not (Test-Path $eventPackagePath)) {
                    $errors.Add("Declared event package is missing: $eventPackagePath")
                }
            }
        }
    }

    foreach ($project in @($Entry.projects)) {
        $projectPath = Resolve-RepoPath $project
        if (-not (Test-Path $projectPath)) {
            $errors.Add("Project file not found: $projectPath")
            continue
        }

        $sourceFiles = Get-ProjectSourceFiles -ProjectPath $projectPath
        if ($sourceFiles.Count -eq 0) {
            $errors.Add("Project has no source files: $projectPath")
            continue
        }

        $pluginConfigs = Get-PluginConfigsFromSources -SourceFiles $sourceFiles
        $hasEventPackage = Test-ProjectHasEventPackage -SourceFiles $sourceFiles
        if ($pluginConfigs.Count -eq 0) {
            if (-not $hasEventPackage) {
                $errors.Add("Project has source files but no [PluginConfig] entry or EventPackage: $projectPath")
                continue
            }
        }

        foreach ($pluginConfig in $pluginConfigs) {
            if ($pluginConfig.ModId -ne $Entry.name) {
                $errors.Add("[PluginConfig] ModId '$($pluginConfig.ModId)' must match folder/manifest name '$($Entry.name)': $($pluginConfig.File)")
            }
            if ($configVersion -and -not (Test-VersionCompatible -ConfigVersion $configVersion -PluginVersion $pluginConfig.Version)) {
                $errors.Add("[PluginConfig] version '$($pluginConfig.Version)' is not compatible with config.lua Version '$configVersion': $($pluginConfig.File)")
            }
        }
    }

    return [PSCustomObject]@{
        Name = $Entry.name
        Status = $Entry.status
        ModDir = $modDir
        ConfigPath = $configPath
        PluginsDir = $pluginsDir
        DeclaredPlugins = $declaredPlugins
        DeclaredEventPackages = $declaredEventPackages
        Errors = @($errors)
        Warnings = @($warnings)
    }
}

function Assert-ModIsValid {
    param(
        [Parameter(Mandatory)]
        $Entry,
        [switch]$RequireBuiltPlugins
    )

    $result = Test-ModStructure -Entry $Entry -RequireBuiltPlugins:$RequireBuiltPlugins
    if ($result.Errors.Count -gt 0) {
        $message = "Mod '$($Entry.name)' failed validation:`n - " + ($result.Errors -join "`n - ")
        throw $message
    }

    return $result
}

function Copy-ModFiles {
    param(
        [Parameter(Mandatory)]
        $Entry,
        [Parameter(Mandatory)]
        [string]$Destination,
        [switch]$Clean,
        [switch]$IncludeSymbols
    )

    $validation = Assert-ModIsValid -Entry $Entry -RequireBuiltPlugins
    if ($Clean -and (Test-Path $Destination)) {
        $resolvedDestination = (Resolve-Path $Destination).Path
        if (-not $resolvedDestination.StartsWith($Script:ModBuildRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a destination outside the repository root: $resolvedDestination"
        }
        Remove-Item -LiteralPath $resolvedDestination -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $destinationPlugins = Join-Path $Destination "Plugins"
    New-Item -ItemType Directory -Force -Path $destinationPlugins | Out-Null

    Copy-Item $validation.ConfigPath $Destination -Force
    Copy-Item (Join-Path $validation.ModDir "Settings.Lua") $Destination -Force

    foreach ($optionalFile in @("README.md", "cover.png")) {
        $path = Join-Path $validation.ModDir $optionalFile
        if (Test-Path $path) {
            Copy-Item $path $Destination -Force
        }
    }

    $configDir = Join-Path $validation.ModDir "Config"
    if (Test-Path $configDir) {
        Copy-Item $configDir $Destination -Recurse -Force
    }

    $eventsDir = Join-Path $validation.ModDir "Events"
    if (Test-Path $eventsDir) {
        $destinationEvents = Join-Path $Destination "Events"
        New-Item -ItemType Directory -Force -Path $destinationEvents | Out-Null
        Get-ChildItem $eventsDir -Recurse -File | Where-Object {
            $_.Extension -in @(".dll", ".txt", ".twes")
        } | ForEach-Object {
            $relativePath = $_.FullName.Substring($eventsDir.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
            $targetPath = Join-Path $destinationEvents $relativePath
            New-Item -ItemType Directory -Force -Path (Split-Path $targetPath -Parent) | Out-Null
            Copy-Item $_.FullName $targetPath -Force
        }
    }

    foreach ($plugin in @($validation.DeclaredPlugins)) {
        $pluginPath = Join-Path $validation.PluginsDir $plugin
        Copy-Item $pluginPath $destinationPlugins -Force

        $depsPath = Join-Path $validation.PluginsDir ([IO.Path]::ChangeExtension($plugin, ".deps.json"))
        if (Test-Path $depsPath) {
            Copy-Item $depsPath $destinationPlugins -Force
        }

        if ($IncludeSymbols) {
            $pdbPath = Join-Path $validation.PluginsDir ([IO.Path]::ChangeExtension($plugin, ".pdb"))
            if (Test-Path $pdbPath) {
                Copy-Item $pdbPath $destinationPlugins -Force
            }
        }
    }

    return $Destination
}
