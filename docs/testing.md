# Testing Plan

Testing is split into four layers: static gates, build gates, package/deploy smoke tests, and in-game integration checks.

## Automated Local Gate

Run before publishing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Test-All.ps1
```

This performs:

- Release-only project builds.
- Mod layout and `config.lua` validation.
- `[PluginConfig]` identity and version validation.
- Harmony target existence validation against `_decompiled`.
- Contract tests for manifest/project/config wiring.
- Contract tests for settings key coverage.
- Contract tests for Harmony source patch coverage.
- Contract tests for ForceEncounter's interaction-event path and combat-result event.
- Pure helper tests for key mod rules that do not need a running game.
- Temporary package layout validation under `artifacts/test-package`.

Acceptance:

- 0 build errors.
- 0 build warnings for release projects.
- No validation errors.
- Warnings for empty `Cover` and `GameVersion` are allowed until real Workshop assets/version policy are added.

## Draft Gate

Use this when checking everything in the workspace, including non-release mods:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Validate-Mod.ps1 -IncludeDrafts
```

Expected current result: release mods pass; `ExampleMod` is a sample and may warn about missing README. Sample warnings should not block release-only packaging.

## Package Smoke Test

Create Workshop staging directories and zips:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ModBuild\Package-All.ps1
```

Acceptance:

- `artifacts/workshop/DreamLover`
- `artifacts/workshop/AntiNTR`
- `artifacts/workshop/FertilityControl`
- One `.zip` per release mod.
- Each package contains `config.lua`, `Settings.Lua`, `README.md`, and `Plugins/<DeclaredPlugin>.dll`.
- Mods with event packages also contain `Events/EventLib/<DeclaredEventPackage>.dll` and any `Config/*.lua` files.
- Packages do not contain source files or project files.

## Local Game Smoke Test

Deploy one mod at a time into a real formal-version game install:

```powershell
.\deploy.ps1 DreamLover -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
.\deploy.ps1 AntiNTR -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
.\deploy.ps1 FertilityControl -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
.\deploy.ps1 ForceEncounter -GameModDir "D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod"
```

Acceptance:

- The game lists the mod with its configured title.
- The mod can be enabled without loader errors.
- Settings UI shows the expected settings.
- Starting or loading a save does not produce plugin initialization errors.

## In-Game Integration Checks

Use a clean test save and enable debug settings where available.

DreamLover:

- Enable `EnableEnamor`, advance one month, and confirm eligible NPCs can gain adored relation toward Taiwu.
- Enable `EnablePursued`, use an NPC already adoring Taiwu, advance one month, and confirm confession can create mutual relation.
- Enable `EnableMarry`, use an eligible mutual-love NPC, advance one month, and confirm formal marriage rules still gate invalid marriages.
- Enable `ForgetMe` while disabling enamor and pursued behavior; an unreciprocated adored relation should be removed after month transition.

AntiNTR:

- Protect Taiwu spouse/adored relation.
- Advance several months.
- Confirm protected spouses do not keep non-Taiwu make-love fixed-action results.
- Confirm legitimate protected couple behavior remains allowed when `AllowCouple` is enabled.

FertilityControl:

- Set total fertility to 100% and test a pair under child limit.
- Enable child-limit edge cases and confirm pregnancy is blocked when the formal limit is reached.
- Set individual fertility to 0 and confirm pregnancy remains blocked.
- Set cricket rate to 0% and 100% in separate checks and confirm pregnancy state type, expected birth date, and cricket luck behavior.

ForceEncounter:

- Enable `ForceEncounter`, load the main map, choose an adult living NPC, open the character interaction event window, and confirm the `敌对` tab includes `情难自已`.
- Select `情难自已` and confirm the backend log reports `Taiwu->target`.
- With `ForceSuccess` off, confirm success/failure follows formal combat and fertility checks.
- With `ForceSuccess` on, confirm the success branch executes and optional record/hatred/favorability settings only affect this forced branch.
- Confirm default settings reject attempts where the target is Taiwu, the actor equals the target, or either character is not adult/alive.

## Integration Automation Roadmap

Current automated contract tests live in `tests/TaiwuMods.Tests` and run as part of `ModBuild\Test-All.ps1`.

Short term:

- Extend `tests/TaiwuMods.Tests` with more pure helper tests for setting-driven logic.
- Keep using `_decompiled` name checks for Harmony target drift.
- Keep package content assertions for "no `.cs`/`.csproj` in release package".

Medium term:

- Add a private runner or local scheduled task with the formal game installed.
- Start the game with a dedicated test profile and prebuilt smoke-test save.
- Collect game logs after loading each mod and fail on plugin initialization exceptions.

Long term:

- Build deterministic save fixtures for month-transition checks.
- Add replay scripts or UI automation for known test scenarios.
- Store sanitized before/after save diffs for relation and pregnancy state assertions.
