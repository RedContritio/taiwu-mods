# Build And Release

This repository uses a shared first-level build toolkit in `ModBuild/`. It is intentionally isolated from mod business code so it can later become a Git-managed sub-repository if more Taiwu mods reuse it.

## Release Matrix

`ModBuild/mods.json` is authoritative:

- `status = release`: built, validated, packaged, and eligible for Workshop upload.
- `status = draft`: kept in the workspace but excluded from release gates by default.
- `status = sample`: build sample, excluded from release gates by default.

Current release mods are `DreamLover`, `AntiNTR`, `FertilityControl`, and `ForceEncounter`.

## Workshop Layout

Each packaged release mod is staged as:

```text
<ModName>/
  config.lua
  Settings.Lua
  README.md
  cover.png              # optional until real assets exist
  Plugins/
    <DeclaredPlugin>.dll
    <DeclaredPlugin>.deps.json
```

Formal packages exclude source projects, `obj`, `.cs`, `.csproj`, `.sln`, `.git`, and `.pdb` by default. Use `-IncludeSymbols` only for debug packages.

## Commands

Validate release metadata without building:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Validate-Mod.ps1
```

Build release projects and validate their produced plugin files:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Build-All.ps1
```

Run the full local release gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Test-All.ps1
```

Create Workshop staging folders and zip archives:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Package-All.ps1
```

Package one mod:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Package-Mod.ps1 -ModName DreamLover
```

Deploy one built mod to a local game install:

```powershell
.\deploy.ps1 DreamLover -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
```

## Validation Rules

Blocking checks:

- `config.lua`, `Settings.Lua`, and project files exist.
- `config.lua` has non-empty `Title`, `Description`, `Version`, and `Author`.
- `config.lua` has `Source`, `Visibility`, `TagList`, `HasArchive`, `ChangeConfig`, and `NeedRestartWhenSettingChanged`.
- `config.lua` declares at least one frontend or backend plugin DLL.
- Declared plugin DLLs exist after build.
- Each release project has source files and at least one `[PluginConfig]`.
- `[PluginConfig]` ModId matches the folder and manifest name.
- `[PluginConfig]` version is compatible with `config.lua Version`; `2.0.0` and `2.0.0.0` are considered compatible.
- Harmony target names listed in `ModBuild/harmony-targets.json` still exist under `_decompiled`.

Warnings:

- `Cover` is empty.
- `GameVersion` is empty.
- A plugin `.deps.json` is missing.
- `Plugins` contains DLLs not declared in `config.lua`.

Warnings are visible but do not block release yet because the project does not currently contain real Workshop cover assets or an agreed target game version string.

## Future Sub-Repository Boundary

If the tooling is split into its own Git repository later, move these files together:

```text
ModBuild/
  ModBuild.Common.ps1
  mods.json
  harmony-targets.json
  Build-All.ps1
  Validate-Mod.ps1
  Validate-HarmonyTargets.ps1
  Package-Mod.ps1
  Package-All.ps1
  Test-All.ps1
```

The host repository only needs to provide mod folders and a `mods.json` manifest.
