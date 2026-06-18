# Taiwu Mods

Formal-version Taiwu mod workspace for the split DreamLover mod family.

## Release Mods

The release matrix is controlled by [ModBuild/mods.json](ModBuild/mods.json).

| Mod | Status | Notes |
| --- | --- | --- |
| DreamLover | release | NPC affection, confession, and proposal behavior. |
| AntiNTR | release | Blocks protected NPC spouses from non-Taiwu make-love fixed actions. |
| FertilityControl | release | Controls fertility, pregnancy probability, cricket birth, and inbreeding behavior. |
| ForceEncounter | release | Adds a hostile interaction-menu option and backend interface for forced character actions. |
| ExampleMod | sample | Development environment sample only. |

## Common Commands

```powershell
# Build and validate all release mods.
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Test-All.ps1

# Build release workshop staging folders and zip archives.
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Package-All.ps1

# Deploy one built mod to a local game Mod directory.
.\deploy.ps1 DreamLover -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
```

Build scripts default to the local Steam path in [Directory.Build.props](Directory.Build.props), but `TAIWU_GAME_DIR` can override it:

```powershell
$env:TAIWU_GAME_DIR = "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu"
```

See [docs/build-and-release.md](docs/build-and-release.md) and [docs/testing.md](docs/testing.md) for the full workflow.
