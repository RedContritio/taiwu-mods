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

function Get-LuaNumberValue {
    param(
        [Parameter(Mandatory)]
        [string]$Text,
        [Parameter(Mandatory)]
        [string]$Key
    )

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Key))\s*=\s*(-?\d+)")
    if ($match.Success) {
        return [int]$match.Groups[1].Value
    }

    return $null
}

function Get-TaiwuGameDir {
    if (-not [string]::IsNullOrWhiteSpace($env:TAIWU_GAME_DIR)) {
        return $env:TAIWU_GAME_DIR
    }

    return "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu"
}

function Get-ConfigRefMap {
    param(
        [Parameter(Mandatory)]
        [string]$ConfigName
    )

    $mappingPath = Join-Path (Get-TaiwuGameDir) "The Scroll of Taiwu_Data\StreamingAssets\ConfigRefNameMapping\$ConfigName.ref.txt"
    if (-not (Test-Path $mappingPath)) {
        return $null
    }

    $lines = @(Get-Content $mappingPath -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $byName = New-Object "System.Collections.Generic.Dictionary[string,int]" -ArgumentList ([System.StringComparer]::Ordinal)
    $byId = New-Object "System.Collections.Generic.Dictionary[int,string]"
    for ($i = 0; $i + 1 -lt $lines.Count; $i += 2) {
        $name = $lines[$i]
        $id = 0
        if (-not [int]::TryParse($lines[$i + 1], [ref]$id)) {
            throw "Malformed ref mapping pair in $mappingPath near '$name' / '$($lines[$i + 1])'"
        }
        $byName[$name] = $id
        $byId[$id] = $name
    }

    return [PSCustomObject]@{
        Path = $mappingPath
        ByName = $byName
        ById = $byId
    }
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

function Test-ModAutoIncrementBuildVersion {
    param(
        [Parameter(Mandatory)]
        $Entry
    )

    $property = $Entry.PSObject.Properties["autoIncrementBuildVersion"]
    return $null -ne $property -and [bool]$property.Value
}

function Update-ModBuildVersion {
    param(
        [Parameter(Mandatory)]
        $Entry
    )

    $modDir = Join-Path $Script:ModBuildRoot $Entry.name
    $configPath = Join-Path $modDir "config.lua"
    if (-not (Test-Path $configPath)) {
        throw "Missing config.lua: $configPath"
    }

    $configText = Get-Content $configPath -Raw -Encoding UTF8
    $currentVersion = Get-LuaQuotedValue -Text $configText -Key "Version"
    if ([string]::IsNullOrWhiteSpace($currentVersion)) {
        throw "$($Entry.name) config.lua is missing Version"
    }

    $parts = @($currentVersion.Split('.') | ForEach-Object { [int]$_ })
    if ($parts.Count -ne 4) {
        throw "$($Entry.name) Version must use four numeric parts for build auto-increment: $currentVersion"
    }

    $parts[3] += 1
    $nextVersion = $parts -join "."
    $updatedConfigText = [regex]::Replace(
        $configText,
        '(?m)(^\s*Version\s*=\s*")([^"]+)(")',
        "`${1}$nextVersion`${3}",
        1)
    Set-Content -LiteralPath $configPath -Value $updatedConfigText -Encoding UTF8 -NoNewline

    $pluginPattern = '(?<prefix>PluginConfig\(\s*(?<mod>"' + [regex]::Escape($Entry.name) + '"|[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*(?<author>"[^"]+"|[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*")(?<version>[^"]+)(?<suffix>"\s*\))'
    foreach ($project in @($Entry.projects)) {
        $projectSourceFiles = Get-ProjectSourceFiles -ProjectPath (Resolve-RepoPath $project)
        $constantValues = Get-PluginConfigConstantValues -SourceFiles $projectSourceFiles
        foreach ($sourceFile in $projectSourceFiles) {
            $sourceText = Get-Content $sourceFile.FullName -Raw -Encoding UTF8
            if (-not [regex]::IsMatch($sourceText, $pluginPattern)) {
                continue
            }

            $updatedSourceText = [regex]::Replace(
                $sourceText,
                $pluginPattern,
                {
                    param($match)
                    $modId = Resolve-PluginConfigArgument -Argument $match.Groups["mod"].Value -ConstantValues $constantValues
                    if ($modId -ne $Entry.name) {
                        return $match.Value
                    }

                    return $match.Groups["prefix"].Value + $nextVersion + $match.Groups["suffix"].Value
                })
            Set-Content -LiteralPath $sourceFile.FullName -Value $updatedSourceText -Encoding UTF8 -NoNewline
        }
    }

    return $nextVersion
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

    $files = New-Object System.Collections.Generic.List[object]
    foreach ($file in @(Get-ChildItem $projectDir -Filter "*.cs" -Recurse | Where-Object {
        $_.FullName -notmatch "\\(bin|obj)\\"
    })) {
        $files.Add($file)
    }

    $projectText = Get-Content $ProjectPath -Raw -ErrorAction SilentlyContinue
    foreach ($match in [regex]::Matches($projectText, '<Compile\s+Include="(?<path>[^"]+\.cs)"')) {
        $linkedPath = Join-Path $projectDir $match.Groups["path"].Value
        if (Test-Path $linkedPath) {
            $files.Add((Get-Item $linkedPath))
        }
    }

    return @($files | Sort-Object FullName -Unique)
}

function Get-PluginConfigsFromSources {
    param(
        [Parameter(Mandatory)]
        [array]$SourceFiles,
        [string]$ExpectedModId
    )

    $configs = New-Object System.Collections.ArrayList
    $constantValues = Get-PluginConfigConstantValues -SourceFiles $SourceFiles
    foreach ($sourceFile in $SourceFiles) {
        $text = Get-Content $sourceFile.FullName -Raw
        foreach ($match in [regex]::Matches($text, 'PluginConfig\(\s*(?<mod>"[^"]+"|[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*(?<author>"[^"]+"|[A-Za-z_][A-Za-z0-9_.]*)\s*,\s*"(?<version>[^"]+)"\s*\)')) {
            [void]$configs.Add([PSCustomObject]@{
                File = $sourceFile.FullName
                ModId = Resolve-PluginConfigArgument -Argument $match.Groups["mod"].Value -ConstantValues $constantValues
                Author = Resolve-PluginConfigArgument -Argument $match.Groups["author"].Value -ConstantValues $constantValues
                Version = $match.Groups["version"].Value
            })
        }
    }

    return @($configs)
}

function Resolve-PluginConfigArgument {
    param(
        [Parameter(Mandatory)]
        [string]$Argument,
        [hashtable]$ConstantValues
    )

    $value = $Argument.Trim()
    $literalMatch = [regex]::Match($value, '^"(?<literal>[^"]*)"$')
    if ($literalMatch.Success) {
        return $literalMatch.Groups["literal"].Value
    }

    if ($ConstantValues -and $ConstantValues.ContainsKey($value)) {
        return $ConstantValues[$value]
    }

    return $value
}

function Get-PluginConfigConstantValues {
    param(
        [Parameter(Mandatory)]
        [array]$SourceFiles
    )

    $values = @{}
    foreach ($sourceFile in $SourceFiles) {
        $text = Get-Content $sourceFile.FullName -Raw
        foreach ($outerMatch in [regex]::Matches($text, 'public\s+static\s+class\s+(?<outer>[A-Za-z_][A-Za-z0-9_]*)[\s\S]*?public\s+static\s+class\s+Mod\s*\{(?<body>[\s\S]*?)\n\s*\}')) {
            $outerName = $outerMatch.Groups["outer"].Value
            foreach ($constMatch in [regex]::Matches($outerMatch.Groups["body"].Value, 'public\s+const\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"(?<value>[^"]*)"')) {
                $values["$outerName.Mod.$($constMatch.Groups["name"].Value)"] = $constMatch.Groups["value"].Value
            }
        }
    }

    return $values
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
        $configText = Get-Content $configPath -Raw -Encoding UTF8
        foreach ($requiredString in @("Title", "Description", "Version", "Author")) {
            $value = Get-LuaQuotedValue -Text $configText -Key $requiredString
            if ([string]::IsNullOrWhiteSpace($value)) {
                $errors.Add("config.lua missing non-empty string field: $requiredString")
            }
            if ($requiredString -eq "Version") {
                $configVersion = $value
            }
        }

        foreach ($recommendedString in @("Cover")) {
            $value = Get-LuaQuotedValue -Text $configText -Key $recommendedString
            if ([string]::IsNullOrWhiteSpace($value)) {
                $warnings.Add("config.lua has empty Workshop/display field: $recommendedString")
            }
        }

        $gameVersion = Get-LuaQuotedValue -Text $configText -Key "GameVersion"
        if ($Entry.status -eq "release" -and [string]::IsNullOrWhiteSpace($gameVersion)) {
            $errors.Add("release config.lua must set GameVersion to the supported game version")
        } elseif ([string]::IsNullOrWhiteSpace($gameVersion)) {
            $warnings.Add("config.lua has empty Workshop/display field: GameVersion")
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

    $configPatchDir = Join-Path $modDir "Config"
    if (Test-Path $configPatchDir) {
        foreach ($configPatch in @(Get-ChildItem $configPatchDir -Filter "*.lua" -File -Recurse)) {
            $patchText = Get-Content $configPatch.FullName -Raw -Encoding UTF8
            $patchConfigName = Get-LuaQuotedValue -Text $patchText -Key "ConfigName"
            $srcConfigRefName = Get-LuaQuotedValue -Text $patchText -Key "SrcConfigRefName"
            $destConfigRefName = Get-LuaQuotedValue -Text $patchText -Key "DestConfigRefName"
            $templateId = Get-LuaNumberValue -Text $patchText -Key "TemplateId"

            if ([string]::IsNullOrWhiteSpace($patchConfigName)) {
                $errors.Add("Config patch missing ConfigName: $($configPatch.FullName)")
                continue
            }

            $refMap = Get-ConfigRefMap -ConfigName $patchConfigName
            if ($null -eq $refMap) {
                $errors.Add("Config patch references unknown config table '$patchConfigName': $($configPatch.FullName)")
                continue
            }

            if ([string]::IsNullOrWhiteSpace($srcConfigRefName)) {
                $errors.Add("Config patch missing SrcConfigRefName; formal mod loading only supports cloning or replacing an existing config row: $($configPatch.FullName)")
            } elseif (-not $refMap.ByName.ContainsKey($srcConfigRefName)) {
                $errors.Add("Config patch SrcConfigRefName '$srcConfigRefName' does not exist in $($refMap.Path): $($configPatch.FullName)")
            }

            if ($null -ne $templateId -and $refMap.ById.ContainsKey($templateId)) {
                $errors.Add("Config patch TemplateId '$templateId' collides with vanilla '$($refMap.ById[$templateId])' in $($refMap.Path): $($configPatch.FullName)")
            }

            if (-not [string]::IsNullOrWhiteSpace($destConfigRefName) -and $refMap.ByName.ContainsKey($destConfigRefName)) {
                $errors.Add("Config patch DestConfigRefName '$destConfigRefName' collides with vanilla mapping in $($refMap.Path): $($configPatch.FullName)")
            }
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

        $pluginConfigs = Get-PluginConfigsFromSources -SourceFiles $sourceFiles -ExpectedModId $Entry.name
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

    # Carry sibling dependency DLLs that are not themselves declared plugins (e.g. a backend
    # plugin that bundles Roslyn for runtime C# eval). Other mods have none, so this is a no-op for them.
    $declaredSet = @(@($validation.DeclaredPlugins) | ForEach-Object { $_.ToLowerInvariant() })
    Get-ChildItem $validation.PluginsDir -File -Filter *.dll | Where-Object {
        $declaredSet -notcontains $_.Name.ToLowerInvariant()
    } | ForEach-Object {
        Copy-Item $_.FullName $destinationPlugins -Force
    }

    return $Destination
}
