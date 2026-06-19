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
  Config/                # optional formal config patches
    <ConfigPatch>.lua
  Events/                # optional event packages
    EventLib/
      <DeclaredEventPackage>.dll
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

Deploy one mod to a local game install. This produces a local game build: for mods marked
`autoIncrementBuildVersion`, it bumps the fourth version component, builds the projects, then
copies the runnable layout into the game `Mod` directory.

```powershell
.\deploy.ps1 DreamLover -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
```

Use `-NoBuild` only when deliberately copying an already-built layout without consuming a new
build number.

## Validation Rules

Blocking checks:

- `config.lua`, `Settings.Lua`, and project files exist.
- `config.lua` has non-empty `Title`, `Description`, `Version`, and `Author`.
- `config.lua` has `Source`, `Visibility`, `TagList`, `HasArchive`, `ChangeConfig`, and `NeedRestartWhenSettingChanged`.
- Release `config.lua` declares a non-empty supported `GameVersion`; an empty value is treated by the formal Mod manager as outdated.
- `config.lua` declares at least one frontend or backend plugin DLL.
- Declared plugin DLLs exist after build.
- Declared event package DLLs exist after build.
- Formal config patches reference existing official `SrcConfigRefName` values.
- Formal config patch `TemplateId` and `DestConfigRefName` values do not collide with official mappings.
- Each release project has source files and at least one `[PluginConfig]`.
- `[PluginConfig]` ModId matches the folder and manifest name.
- `[PluginConfig]` version is compatible with `config.lua Version`; `2.0.0` and `2.0.0.0` are considered compatible.
- Harmony target names listed in `ModBuild/harmony-targets.json` still exist under `_decompiled`.

Warnings:

- `Cover` is empty.
- A plugin `.deps.json` is missing.
- `Plugins` contains DLLs not declared in `config.lua`.

Warnings are visible but do not block release yet because the project does not currently contain real Workshop cover assets.

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
