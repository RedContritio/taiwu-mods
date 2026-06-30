# ForceEncounter Prisoner Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make 「情难自已」usable on already-imprisoned targets by adding a fully independent prisoner event chain (entry → inner choice → forced/intimate feedback) injected into the kidnapped-character interaction menu, reusing the shared backend settlement.

**Architecture:** A parallel set of four new Mod events (all new GUIDs) copied and trimmed from the normal chain, plus a dedicated prisoner text catalog and a dedicated outer tip config. The prisoner forced route settles directly via the shared backend combat resolution with `battleSucceeded=true` (no combat/guard machinery). The normal chain and shared text builders are not modified.

**Tech Stack:** C# net8.0 (game-side event/backend DLLs referenced via `$(TaiwuBackendDir)`), Lua config patches, a custom C# console contract-test runner (`tests/TaiwuMods.Tests`).

## Global Constraints

- ForceEncounter version must stay on the `1.0.0.x` line (contract pins `StartsWith("1.0.0.")`); never bump the minor. The 4th segment is auto-incremented by `deploy.ps1`/packaging — do NOT hand-edit `config.lua` `Version` or backend `[PluginConfig]` version.
- Event option button text must come from a text catalog constant, never a literal in `SetContent("...")` (contract forbids `SetContent("`).
- Do not modify the normal-chain events (`ForceEncounterEvent`, `ForceEncounterConsentChoiceEvent`, `ForceEncounterCombatResultEvent`, `ForceEncounterAcceptedResultEvent`) or the shared `ForceEncounterEventText` builders.
- New prisoner GUIDs (use exactly these):
  - Events: PrisonerEntry `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e50`, PrisonerConsentChoice `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e51`, PrisonerForcedResult `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e52`, PrisonerAcceptedResult `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e53`
  - Options: ExecutePrisoner `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e60`, PrisonerNormalEncounter `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e61`, PrisonerForceCombat `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e62`, PrisonerAbandon `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e63`, PrisonerForcedContinue `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e64`, PrisonerAcceptedContinue `7a1c2d3e-4f50-4617-8293-0a1b2c3d4e65`
- Native kidnapped-interaction menu event GUID: `2e651ccb-3a77-447a-a74f-c9a24a1a32d1` (EventGroup `KidnappedCharacterInteraction`, MainRoleKey `RoleTaiwu`, TargetRoleKey `CharacterId`).
- Prisoner outer tip: `Config/EventOptionTipsInfoPrisoner.lua`, `ConfigName="EventOptionTipsInfo"`, `SrcConfigRefName="袭击"`, `DestConfigRefName="ForceEncounter.PrisonerOptionTip"`, `TemplateId=119` (verified free; formal max id is 117, 118 used by the existing tip), `Guid={ ExecutePrisonerGuid }`.

## Commands (used throughout)

- Build events: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
- Build backend: `dotnet build ForceEncounter/ForceEncounter.Backend/ForceEncounter.Backend.csproj`
- Run contract tests: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
  - Success line: `All Taiwu mod contract tests passed.`

The test project links `ForceEncounterConstants.cs` as a source file but does NOT compile the Events/Backend assemblies, so contract assertions read source as text. Always run the project build (compile check) before the contract run.

## File Structure

- `ForceEncounter/ForceEncounter.Shared/ForceEncounterConstants.cs` — add prisoner GUID/key constants + `NativeKidnappedInteraction` (modify)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterEventIds.cs` — surface prisoner 事件/选项 ids (modify)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerText.cs` — prisoner text catalog (create)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerEntryEvent.cs` — prisoner outer entry (create)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerConsentChoiceEvent.cs` — prisoner inner choice + direct settlement (create)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerForcedResultEvent.cs` — prisoner forced success feedback (create)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerAcceptedResultEvent.cs` — prisoner intimate feedback (create)
- `ForceEncounter/ForceEncounter.Events/ForceEncounterEventPackage.cs` — register 4 events + inject into 2e651ccb (modify)
- `ForceEncounter/ForceEncounter.Backend/BackendPlugin.cs` — inject prisoner option on world/load (modify)
- `ForceEncounter/Config/EventOptionTipsInfoPrisoner.lua` — prisoner outer tip (create)
- `ForceEncounter/README.md`, `ForceEncounter/docs/development-notes.md` — docs (modify)
- `tests/TaiwuMods.Tests/Program.cs` — add `ForceEncounterPrisonerInteractionContract()` (modify, grown across tasks)

---

### Task 1: Constants + contract scaffold

**Files:**
- Modify: `ForceEncounter/ForceEncounter.Shared/ForceEncounterConstants.cs`
- Modify: `ForceEncounter/ForceEncounter.Events/ForceEncounterEventIds.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Produces (Shared `ForceEncounterConstants`):
  - `EventGuids.NativeKidnappedInteraction`, `EventGuids.PrisonerEntry`, `EventGuids.PrisonerConsentChoice`, `EventGuids.PrisonerForcedResult`, `EventGuids.PrisonerAcceptedResult`
  - `Options.ExecutePrisonerKey/ExecutePrisonerGuid`, `Options.PrisonerNormalEncounterKey/Guid`, `Options.PrisonerForceCombatKey/Guid`, `Options.PrisonerAbandonKey/Guid`, `Options.PrisonerForcedContinueKey/Guid`, `Options.PrisonerAcceptedContinueKey/Guid`
- Produces (Events `ForceEncounterEventIds`):
  - `事件.关押外层入口/关押内层选择/关押强制反馈/关押亲密反馈`, `事件.原生关押菜单`
  - `选项.情难自已关押/关押正常发生关系/关押强制关系/关押其他话题/关押强制反馈继续/关押亲密反馈继续` (each `EventOptionId`)

- [ ] **Step 1: Add the failing contract assertions**

In `tests/TaiwuMods.Tests/Program.cs`, register a new test in `RunAll()` right after the existing ForceEncounter line (around line 47):

```csharp
        Run("ForceEncounter interaction contract is wired", ForceEncounterInteractionContract);
        Run("ForceEncounter prisoner interaction contract is wired", ForceEncounterPrisonerInteractionContract);
```

Then add this method immediately after the closing brace of `ForceEncounterInteractionContract()` (after line 537):

```csharp
    private void ForceEncounterPrisonerInteractionContract()
    {
        ModEntry mod = _mods.Single(m => m.Name == "ForceEncounter");
        string sharedIds = ReadModFile(mod.Name, "ForceEncounter.Shared", "ForceEncounterConstants.cs");

        Assert(ExtractNestedConst(sharedIds, "EventGuids", "NativeKidnappedInteraction") == "2e651ccb-3a77-447a-a74f-c9a24a1a32d1", "ForceEncounter native kidnapped-interaction menu guid changed unexpectedly");
        foreach (string guidName in new[] { "PrisonerEntry", "PrisonerConsentChoice", "PrisonerForcedResult", "PrisonerAcceptedResult" })
        {
            Assert(!string.IsNullOrWhiteSpace(ExtractNestedConst(sharedIds, "EventGuids", guidName)), $"ForceEncounter prisoner event guid {guidName} is empty");
        }

        foreach (string optionName in new[] { "ExecutePrisoner", "PrisonerNormalEncounter", "PrisonerForceCombat", "PrisonerAbandon", "PrisonerForcedContinue", "PrisonerAcceptedContinue" })
        {
            Assert(!string.IsNullOrWhiteSpace(ExtractNestedConst(sharedIds, "Options", optionName + "Key")), $"ForceEncounter prisoner option key {optionName} is empty");
            Assert(!string.IsNullOrWhiteSpace(ExtractNestedConst(sharedIds, "Options", optionName + "Guid")), $"ForceEncounter prisoner option guid {optionName} is empty");
        }

        string eventIdsSource = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventIds.cs");
        Assert(eventIdsSource.Contains("原生关押菜单", StringComparison.Ordinal), "ForceEncounter event ids should expose the kidnapped-interaction menu guid");
        Assert(eventIdsSource.Contains("情难自已关押", StringComparison.Ordinal), "ForceEncounter event ids should expose the prisoner execute option");
    }
```

- [ ] **Step 2: Run the contract tests to verify the new test fails**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — output lists `ForceEncounter prisoner interaction contract is wired: missing id group EventGuids` or `missing const string NativeKidnappedInteraction`.

- [ ] **Step 3: Add the constants to `ForceEncounterConstants.cs`**

In `EventGuids` (after the `AcceptedResult` line), add:

```csharp
            public const string NativeKidnappedInteraction = "2e651ccb-3a77-447a-a74f-c9a24a1a32d1";
            public const string PrisonerEntry = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e50";
            public const string PrisonerConsentChoice = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e51";
            public const string PrisonerForcedResult = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e52";
            public const string PrisonerAcceptedResult = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e53";
```

In `Options` (after the `GuardContinueGuid` line), add:

```csharp
            public const string ExecutePrisonerKey = "ForceEncounter.ExecutePrisoner";
            public const string ExecutePrisonerGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e60";
            public const string PrisonerNormalEncounterKey = "ForceEncounter.PrisonerNormalEncounter";
            public const string PrisonerNormalEncounterGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e61";
            public const string PrisonerForceCombatKey = "ForceEncounter.PrisonerForceCombat";
            public const string PrisonerForceCombatGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e62";
            public const string PrisonerAbandonKey = "ForceEncounter.PrisonerAbandon";
            public const string PrisonerAbandonGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e63";
            public const string PrisonerForcedContinueKey = "ForceEncounter.PrisonerForcedContinue";
            public const string PrisonerForcedContinueGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e64";
            public const string PrisonerAcceptedContinueKey = "ForceEncounter.PrisonerAcceptedContinue";
            public const string PrisonerAcceptedContinueGuid = "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e65";
```

- [ ] **Step 4: Surface the ids in `ForceEncounterEventIds.cs`**

In `public static class 事件`, add after `亲密反馈`:

```csharp
            public const string 原生关押菜单 = ForceEncounterConstants.EventGuids.NativeKidnappedInteraction;
            public const string 关押外层入口 = ForceEncounterConstants.EventGuids.PrisonerEntry;
            public const string 关押内层选择 = ForceEncounterConstants.EventGuids.PrisonerConsentChoice;
            public const string 关押强制反馈 = ForceEncounterConstants.EventGuids.PrisonerForcedResult;
            public const string 关押亲密反馈 = ForceEncounterConstants.EventGuids.PrisonerAcceptedResult;
```

In `public static class 选项`, add after `护卫继续`:

```csharp
            public static readonly EventOptionId 情难自已关押 = new(ForceEncounterConstants.Options.ExecutePrisonerKey, ForceEncounterConstants.Options.ExecutePrisonerGuid);
            public static readonly EventOptionId 关押正常发生关系 = new(ForceEncounterConstants.Options.PrisonerNormalEncounterKey, ForceEncounterConstants.Options.PrisonerNormalEncounterGuid);
            public static readonly EventOptionId 关押强制关系 = new(ForceEncounterConstants.Options.PrisonerForceCombatKey, ForceEncounterConstants.Options.PrisonerForceCombatGuid);
            public static readonly EventOptionId 关押其他话题 = new(ForceEncounterConstants.Options.PrisonerAbandonKey, ForceEncounterConstants.Options.PrisonerAbandonGuid);
            public static readonly EventOptionId 关押强制反馈继续 = new(ForceEncounterConstants.Options.PrisonerForcedContinueKey, ForceEncounterConstants.Options.PrisonerForcedContinueGuid);
            public static readonly EventOptionId 关押亲密反馈继续 = new(ForceEncounterConstants.Options.PrisonerAcceptedContinueKey, ForceEncounterConstants.Options.PrisonerAcceptedContinueGuid);
```

- [ ] **Step 5: Build events and run the contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS — `All Taiwu mod contract tests passed.`

- [ ] **Step 6: Commit**

```bash
git add ForceEncounter/ForceEncounter.Shared/ForceEncounterConstants.cs ForceEncounter/ForceEncounter.Events/ForceEncounterEventIds.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner chain constants and contract scaffold"
```

---

### Task 2: Prisoner text catalog

**Files:**
- Create: `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerText.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Produces: `internal static class ForceEncounterPrisonerText` with:
  - `string 构造内层说明(bool 亲密通过, bool 未成年)`
  - `string BuildForcedResultContent(bool targetIsTaiwuVillager, bool 未成年)`
  - `string BuildAcceptedResultContent(bool ok, bool succeeded, bool 未成年)`
- Consumes: nothing (self-contained literal text).

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()` in `Program.cs`:

```csharp
        string events = ReadProjectDirectorySource(mod.Name, "ForceEncounter.Events");
        Assert(events.Contains("class ForceEncounterPrisonerText", StringComparison.Ordinal), "ForceEncounter should have a dedicated prisoner text catalog");
        Assert(events.Contains("BuildForcedResultContent", StringComparison.Ordinal), "ForceEncounter prisoner text should build forced-result feedback");
        Assert(events.Contains("无从抗拒", StringComparison.Ordinal), "ForceEncounter prisoner inner description should read as a bound captive");
        Assert(!Regex.IsMatch(events, @"ForceEncounterPrisonerText[\s\S]{0,4000}?护卫"), "ForceEncounter prisoner text should not mention guards");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter should have a dedicated prisoner text catalog`.

- [ ] **Step 3: Create `ForceEncounterPrisonerText.cs`**

```csharp
namespace ForceEncounter.Events
{
    internal static class ForceEncounterPrisonerText
    {
        public static string 构造内层说明(bool 亲密通过, bool 未成年)
        {
            if (亲密通过)
            {
                return 未成年 ? 入口说明.未成年亲密 : 入口说明.成年亲密;
            }

            return 未成年 ? 入口说明.未成年强制 : 入口说明.成年强制;
        }

        public static string BuildForcedResultContent(bool targetIsTaiwuVillager, bool 未成年)
        {
            if (未成年)
            {
                return targetIsTaiwuVillager ? 强制反馈.未成年成功村民 : 强制反馈.未成年成功;
            }

            return targetIsTaiwuVillager ? 强制反馈.成年成功村民 : 强制反馈.成年成功;
        }

        public static string BuildAcceptedResultContent(bool ok, bool succeeded, bool 未成年)
        {
            if (!ok || !succeeded)
            {
                return 未成年 ? 亲密反馈.未成年结算失败 : 亲密反馈.成年结算失败;
            }

            return 未成年 ? 亲密反馈.未成年成功 : 亲密反馈.成年成功;
        }

        private static class 入口说明
        {
            public const string 成年亲密 = "<Character key=CharacterId str=Name/>虽被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，对<Character key=RoleTaiwu str=Name/>却素无怨怼之心。\n\n这一日<Character key=RoleTaiwu str=Name/>情动难耐，俯身将<Character key=CharacterId str=Name/>揽入怀中。<Character key=CharacterId str=Name/>怔了怔，到底没有别过脸去。";
            public const string 未成年亲密 = "<Character key=CharacterId str=Name/>被缚着手，蜷在<Character key=RoleTaiwu str=Name/>身边，见<Character key=RoleTaiwu str=Name/>凑近，并不十分闪躲。\n\n<Character key=RoleTaiwu str=Name/>握住<Character key=CharacterId str=Name/>的手，<Character key=CharacterId str=Name/>一颤，垂着头，连耳根都红透了。";
            public const string 成年强制 = "<Character key=CharacterId str=Name/>早已被缚住手脚，押在<Character key=RoleTaiwu str=Name/>面前，纵有满腔不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>居高临下地看着，眼底渐渐起了异样的念头。";
            public const string 未成年强制 = "<Character key=CharacterId str=Name/>早已被缚住手脚，蜷在角落里。见<Character key=RoleTaiwu str=Name/>一步步逼近，<Character key=CharacterId str=Name/>吓得直往墙根缩，却退无可退，无从抗拒。";
        }

        private static class 强制反馈
        {
            public const string 成年成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续，绳索偶尔挣动，却终究挣不脱。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息。";
            public const string 成年成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续。绳索只偶尔挣动了一下，便再没了声息。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息，连喘息都不敢太重，唯恐再生事端。";
            public const string 未成年成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索偶尔挣动，却终是徒劳，细弱的哭声时而被堵住似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡。";
            public const string 未成年成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索只偶尔挣动了一下便再没了声息，细弱的哭声时而被自己捂住了似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡，连抽泣都死死压在喉咙里。";
        }

        private static class 亲密反馈
        {
            public const string 成年成功 = "也不知过了多久，二人才渐渐安顿下来。<Character key=CharacterId str=Name/>鬓发散乱，面上红潮未褪，只侧身倚着<Character key=RoleTaiwu str=Name/>，半晌不曾开口。\n\n<Character key=RoleTaiwu str=Name/>伸手替<Character key=CharacterId str=Name/>拢了拢碎发，指节擦过耳际时，<Character key=CharacterId str=Name/>微微一缩，随即又松弛下来，连呼吸都轻了。";
            public const string 成年结算失败 = "孰料临到此时，<Character key=CharacterId str=Name/>却忽然偏过头去，呼吸急促了几分。<Character key=RoleTaiwu str=Name/>怔了怔，到底不忍勉强，只低低说了句什么，二人便这么依偎着，许久没有再进一步。";
            public const string 未成年成功 = "过了许久，<Character key=CharacterId str=Name/>仍蜷着身子，耳根的红一路蔓延到颈侧。<Character key=RoleTaiwu str=Name/>替<Character key=CharacterId str=Name/>理了理揉皱的衣领，<Character key=CharacterId str=Name/>这才抬起眼飞快地看了一眼，又迅速垂下目光，手指却悄悄勾住了<Character key=RoleTaiwu str=Name/>的袖角。";
            public const string 未成年结算失败 = "<Character key=CharacterId str=Name/>忽然偏过头去，眼眶微微泛红，呼吸又急又浅。<Character key=RoleTaiwu str=Name/>伸出的手顿在半空，半晌叹了口气，替<Character key=CharacterId str=Name/>把滑落的衣带重新系好，不再迫近。";
        }
    }
}
```

- [ ] **Step 4: Build events and run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS — `All Taiwu mod contract tests passed.`

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerText.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner text catalog"
```

---

### Task 3: Prisoner outer entry event

**Files:**
- Create: `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerEntryEvent.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: `ForceEncounterEventCosts.BuildPreviewCosts`, `ForceEncounterEventRuntime.*`, `ForceEncounterEventText.按钮.情难自已`, ids from Task 1.
- Produces: event GUID `关押外层入口`; on select, probes the shared backend and returns `关押内层选择`.

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        Assert(events.Contains("class ForceEncounterPrisonerEntryEvent", StringComparison.Ordinal), "ForceEncounter should have a dedicated prisoner entry event");
        Assert(Regex.IsMatch(events, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.情难自已关押\.Key,[\s\S]*?Behavior\s*=\s*EventOptionBehavior\.BehaviorEgoistic,[\s\S]*?OnOptionSelect\s*=\s*Execute"), "ForceEncounter prisoner entry should use native egoistic styling and probe on select");
        Assert(events.Contains("return ForceEncounterEventIds.事件.关押内层选择", StringComparison.Ordinal), "ForceEncounter prisoner entry should route to the prisoner inner choice");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter should have a dedicated prisoner entry event`.

- [ ] **Step 3: Create `ForceEncounterPrisonerEntryEvent.cs`**

```csharp
using System;
using System.Collections.Generic;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;
using TaiwuMod.Common;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterPrisonerEntryEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerEntryEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押外层入口);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = string.Empty;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.情难自已关押.Key,
                    OptionGuid = ForceEncounterEventIds.选项.情难自已关押.Guid,
                    Behavior = EventOptionBehavior.BehaviorEgoistic,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OptionConsumeInfos = new List<OptionConsumeInfo>(),
                    OnOptionVisibleCheck = IsVisible,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = Execute
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.情难自已);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            RefreshPreviewCosts();
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }

        private bool IsVisible()
        {
            RefreshPreviewCosts();
            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);
            if (允许未成年)
            {
                return true;
            }

            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return true;
            }

            return !ForceEncounterEventRuntime.需要未成年提示(ForceEncounterEventRuntime.GetActorId(), targetId);
        }

        private bool CanExecute()
        {
            RefreshPreviewCosts();
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return false;
            }

            int actorId = ForceEncounterEventRuntime.GetActorId();
            if (actorId == targetId)
            {
                return false;
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return false;
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return false;
            }

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组 ||
                target.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组)
            {
                return false;
            }

            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);
            if (!允许未成年 &&
                (actor.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组 ||
                 target.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组))
            {
                return false;
            }

            return true;
        }

        private int RefreshPreviewCosts()
        {
            string modId = GetRuntimeModId();
            int days = ForceEncounterEventCosts.GetActionTimeCostDays(modId);
            EventOptions[0].OptionConsumeInfos = ForceEncounterEventCosts.BuildPreviewCosts(modId);
            return days;
        }

        private string Execute()
        {
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return string.Empty;
            }

            int actorId = ForceEncounterEventRuntime.GetActorId();
            ForceEncounterEventRuntime.StoreInteractionArgs(ArgBox, actorId, targetId);
            ArgBox.Set(EventArgBox.OptionWaitConfirmKey, ForceEncounterEventIds.等待确认.外层预览);

            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.探测);

            if (result != null &&
                result.Get(ForceEncounterEventIds.后端.结算结果, out int resolution) &&
                (resolution == ForceEncounterEventIds.探测结果.亲密通过 ||
                 resolution == ForceEncounterEventIds.探测结果.需要战斗选择))
            {
                ArgBox.Set(ForceEncounterEventIds.后端.结算结果, resolution);
                return ForceEncounterEventIds.事件.关押内层选择;
            }

            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
```

Note: the `out int targetId` inline declaration in `CanExecute` requires C# inline-out; if the project's lang version rejects it, declare `int targetId = -1;` before the `if` and pass `ref targetId`/`out`. Use whichever matches the existing files (the existing events use `ref targetId` with a prior declaration — prefer that form):

```csharp
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return false;
            }
```

- [ ] **Step 4: Build events and run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerEntryEvent.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner outer entry event"
```

---

### Task 4: Prisoner inner choice event (direct settlement)

**Files:**
- Create: `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerConsentChoiceEvent.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: probe resolution stored in ArgBox by Task 3; `ForceEncounterEventRuntime.CallBackend`/`CallCombatBackend`/`StoreCombatSettlement`/`StoreAcceptedSettlement`/`ConfirmOuterWaitOption`; `ForceEncounterEventCosts.BuildCommitConditions/Costs`; `ForceEncounterPrisonerText.构造内层说明`.
- Produces: event GUID `关押内层选择`; routes to `关押强制反馈` (forced), `关押亲密反馈` (intimate), or `原生关押菜单` (abandon).

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        Assert(events.Contains("class ForceEncounterPrisonerConsentChoiceEvent", StringComparison.Ordinal), "ForceEncounter should have a dedicated prisoner inner choice event");
        Assert(events.Contains("CallCombatBackend(GetRuntimeModId(), actorId, targetId, true)", StringComparison.Ordinal), "ForceEncounter prisoner forced route should settle directly as success without combat");
        Assert(Regex.IsMatch(events, @"class ForceEncounterPrisonerConsentChoiceEvent[\s\S]*?return ForceEncounterEventIds\.事件\.原生关押菜单"), "ForceEncounter prisoner abandon should return to the kidnapped-interaction menu");
        Assert(events.Contains("return ForceEncounterEventIds.事件.关押强制反馈", StringComparison.Ordinal), "ForceEncounter prisoner forced route should route to the prisoner forced feedback");
        Assert(events.Contains("return ForceEncounterEventIds.事件.关押亲密反馈", StringComparison.Ordinal), "ForceEncounter prisoner intimate route should route to the prisoner intimate feedback");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter should have a dedicated prisoner inner choice event`.

- [ ] **Step 3: Create `ForceEncounterPrisonerConsentChoiceEvent.cs`**

```csharp
using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterPrisonerConsentChoiceEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerConsentChoiceEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押内层选择);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.关押其他话题.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押正常发生关系.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押正常发生关系.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(string.Empty),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(string.Empty),
                    OnOptionVisibleCheck = IsAcceptedResolution,
                    OnOptionAvailableCheck = CanCommitAcceptedResolution,
                    OnOptionSelect = NormalEncounter
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押强制关系.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押强制关系.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = true,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(string.Empty),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(string.Empty),
                    OnOptionVisibleCheck = IsForcedResolution,
                    OnOptionSelect = ForceProceed
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押其他话题.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押其他话题.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Abandon
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.正常发生关系);
            EventOptions[1].SetContent(ForceEncounterEventText.按钮.强制关系);
            EventOptions[2].SetContent(ForceEncounterEventText.按钮.其他话题);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            string modId = GetRuntimeModId();
            EventOptions[0].OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(modId);
            EventOptions[0].OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(modId);
            EventOptions[1].OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(modId);
            EventOptions[1].OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(modId);
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool 亲密通过 = IsAcceptedResolution();
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);
            string content = ForceEncounterPrisonerText.构造内层说明(亲密通过, 未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private bool IsAcceptedResolution()
        {
            int resolution = -1;
            return ArgBox != null &&
                   ArgBox.Get(ForceEncounterEventIds.后端.结算结果, ref resolution) &&
                   resolution == ForceEncounterEventIds.探测结果.亲密通过;
        }

        private bool IsForcedResolution()
        {
            int resolution = -1;
            return ArgBox != null &&
                   ArgBox.Get(ForceEncounterEventIds.后端.结算结果, ref resolution) &&
                   resolution == ForceEncounterEventIds.探测结果.需要战斗选择;
        }

        private bool CanCommitAcceptedResolution()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                return false;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.探测);
            if (result != null &&
                result.Get(ForceEncounterEventIds.后端.结算结果, out int resolution) &&
                resolution == ForceEncounterEventIds.探测结果.亲密通过)
            {
                return true;
            }

            ArgBox.Set(ForceEncounterEventIds.后端.结算结果, ForceEncounterEventIds.探测结果.需要战斗选择);
            return false;
        }

        private string NormalEncounter()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.亲密提交);
            bool ok = false;
            bool succeeded = false;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.Reason, out reason);
            }

            if (reason == ForceEncounterConstants.Reasons.NeedCombatChoice)
            {
                ArgBox.Set(ForceEncounterEventIds.后端.结算结果, ForceEncounterEventIds.探测结果.需要战斗选择);
                return ForceEncounterEventIds.事件.关押内层选择;
            }

            ForceEncounterEventRuntime.StoreAcceptedSettlement(ArgBox, ok, succeeded, reason);
            if (ok && succeeded)
            {
                ForceEncounterEventRuntime.ConfirmOuterWaitOption(ArgBox);
            }

            return ForceEncounterEventIds.事件.关押亲密反馈;
        }

        private string ForceProceed()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            ForceEncounterEventRuntime.ConfirmOuterWaitOption(ArgBox);

            SerializableModData result = ForceEncounterEventRuntime.CallCombatBackend(GetRuntimeModId(), actorId, targetId, true);
            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
                result.Get(ForceEncounterConstants.Response.AppliedEnmity, out appliedEnmity);
            }

            ForceEncounterEventRuntime.StoreCombatSettlement(
                ArgBox,
                ok,
                succeeded,
                targetIsTaiwuVillager,
                ForceEncounterConstants.Reasons.TargetAlreadyPrisoner,
                appliedEnmity);

            return ForceEncounterEventIds.事件.关押强制反馈;
        }

        private string Abandon()
        {
            return ForceEncounterEventIds.事件.原生关押菜单;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
```

- [ ] **Step 4: Build events and run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerConsentChoiceEvent.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner inner choice with direct settlement"
```

---

### Task 5: Prisoner forced feedback event

**Files:**
- Create: `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerForcedResultEvent.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: settlement stored in ArgBox by Task 4 (`战斗结算成功`/`战斗分支成功`/`战斗目标是太吾村民`); `ForceEncounterPrisonerText.BuildForcedResultContent`; `ForceEncounterEventText.按钮.离开`.
- Produces: event GUID `关押强制反馈`; single continue option closes the event.

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        Assert(events.Contains("class ForceEncounterPrisonerForcedResultEvent", StringComparison.Ordinal), "ForceEncounter should have a dedicated prisoner forced feedback event");
        Assert(events.Contains("ForceEncounterPrisonerText.BuildForcedResultContent", StringComparison.Ordinal), "ForceEncounter prisoner forced feedback should use the prisoner text catalog");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter should have a dedicated prisoner forced feedback event`.

- [ ] **Step 3: Create `ForceEncounterPrisonerForcedResultEvent.cs`**

```csharp
using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterPrisonerForcedResultEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerForcedResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押强制反馈);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.关押强制反馈继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押强制反馈继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押强制反馈继续.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Continue
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.离开);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool targetIsTaiwuVillager = false;
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗目标是太吾村民, ref targetIsTaiwuVillager);
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);

            string content = ForceEncounterPrisonerText.BuildForcedResultContent(targetIsTaiwuVillager, 未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private string Continue()
        {
            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
```

- [ ] **Step 4: Build events and run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerForcedResultEvent.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner forced feedback event"
```

---

### Task 6: Prisoner intimate feedback event

**Files:**
- Create: `ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerAcceptedResultEvent.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: accepted settlement stored in ArgBox by Task 4 (`亲密结算成功`/`亲密分支成功`); `ForceEncounterPrisonerText.BuildAcceptedResultContent`.
- Produces: event GUID `关押亲密反馈`; single continue option closes the event.

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        Assert(events.Contains("class ForceEncounterPrisonerAcceptedResultEvent", StringComparison.Ordinal), "ForceEncounter should have a dedicated prisoner intimate feedback event");
        Assert(events.Contains("ForceEncounterPrisonerText.BuildAcceptedResultContent", StringComparison.Ordinal), "ForceEncounter prisoner intimate feedback should use the prisoner text catalog");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter should have a dedicated prisoner intimate feedback event`.

- [ ] **Step 3: Create `ForceEncounterPrisonerAcceptedResultEvent.cs`**

```csharp
using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterPrisonerAcceptedResultEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerAcceptedResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押亲密反馈);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.关押亲密反馈继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押亲密反馈继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押亲密反馈继续.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Continue
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.离开);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool ok = false;
            bool succeeded = false;
            ArgBox?.Get(ForceEncounterEventIds.参数.亲密结算成功, ref ok);
            ArgBox?.Get(ForceEncounterEventIds.参数.亲密分支成功, ref succeeded);
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);

            string content = ForceEncounterPrisonerText.BuildAcceptedResultContent(ok, succeeded, 未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private string Continue()
        {
            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
```

- [ ] **Step 4: Build events and run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Expected: Build succeeded.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterPrisonerAcceptedResultEvent.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner intimate feedback event"
```

---

### Task 7: Register events + inject into the kidnapped menu

**Files:**
- Modify: `ForceEncounter/ForceEncounter.Events/ForceEncounterEventPackage.cs`
- Modify: `ForceEncounter/ForceEncounter.Backend/BackendPlugin.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: the four prisoner event classes (Tasks 3–6) and the `情难自已关押` option key.
- Produces: prisoner option present in the live `2e651ccb` menu after package load and after world/archive runtime resets.

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        string package = ReadProjectDirectorySource(mod.Name, "ForceEncounter.Events");
        Assert(package.Contains("new ForceEncounterPrisonerEntryEvent()", StringComparison.Ordinal), "ForceEncounter prisoner entry event is not registered");
        Assert(package.Contains("new ForceEncounterPrisonerConsentChoiceEvent()", StringComparison.Ordinal), "ForceEncounter prisoner consent choice event is not registered");
        Assert(package.Contains("new ForceEncounterPrisonerForcedResultEvent()", StringComparison.Ordinal), "ForceEncounter prisoner forced feedback event is not registered");
        Assert(package.Contains("new ForceEncounterPrisonerAcceptedResultEvent()", StringComparison.Ordinal), "ForceEncounter prisoner intimate feedback event is not registered");
        Assert(package.Contains("ForceEncounterEventIds.事件.原生关押菜单", StringComparison.Ordinal), "ForceEncounter should extend the kidnapped-interaction menu");
        Assert(Regex.IsMatch(package, @"AddOptionToEvent\(\s*ForceEncounterEventIds\.事件\.原生关押菜单,\s*ForceEncounterEventIds\.事件\.关押外层入口,\s*ForceEncounterEventIds\.选项\.情难自已关押\.Key"), "ForceEncounter should inject the prisoner entry option into the kidnapped-interaction menu");

        string backend = ReadModFile(mod.Name, "ForceEncounter.Backend", "BackendPlugin.cs");
        Assert(Regex.IsMatch(backend, @"AddOptionToEvent\(\s*ForceEncounterEventIds\.NativeKidnappedInteractionEventGuid,\s*ForceEncounterEventIds\.PrisonerEntryEventGuid,\s*ForceEncounterEventIds\.ExecutePrisonerOptionKey"), "ForceEncounter backend should restore the prisoner menu option after runtime resets");
```

Also add the backend id surface assertions:

```csharp
        string backendIds = ReadModFile(mod.Name, "ForceEncounter.Backend", "ForceEncounterEventIds.cs");
        Assert(backendIds.Contains("NativeKidnappedInteractionEventGuid", StringComparison.Ordinal), "ForceEncounter backend ids should expose the kidnapped-interaction menu guid");
        Assert(backendIds.Contains("PrisonerEntryEventGuid", StringComparison.Ordinal), "ForceEncounter backend ids should expose the prisoner entry guid");
        Assert(backendIds.Contains("ExecutePrisonerOptionKey", StringComparison.Ordinal), "ForceEncounter backend ids should expose the prisoner execute option key");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `ForceEncounter prisoner entry event is not registered`.

- [ ] **Step 3: Register events + package-level injection**

In `ForceEncounter/ForceEncounter.Events/ForceEncounterEventPackage.cs`, extend `EventList`:

```csharp
            EventList = new List<TaiwuEventItem>
            {
                new ForceEncounterEvent(),
                new ForceEncounterConsentChoiceEvent(),
                new ForceEncounterGuardInterceptEvent(),
                new ForceEncounterCombatResultEvent(),
                new ForceEncounterCapturedTargetDispositionEvent(),
                new ForceEncounterAcceptedResultEvent(),
                new ForceEncounterPrisonerEntryEvent(),
                new ForceEncounterPrisonerConsentChoiceEvent(),
                new ForceEncounterPrisonerForcedResultEvent(),
                new ForceEncounterPrisonerAcceptedResultEvent()
            };
```

After the existing `EventHelper.AddOptionToEvent(...原生敌对菜单...)` block, add:

```csharp
            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.事件.原生关押菜单,
                ForceEncounterEventIds.事件.关押外层入口,
                ForceEncounterEventIds.选项.情难自已关押.Key);
```

- [ ] **Step 4: Surface backend ids**

In `ForceEncounter/ForceEncounter.Backend/ForceEncounterEventIds.cs`, add after `NativeEnemyInteractionEventGuid`/`EventGuid`/`OptionKey`:

```csharp
        public const string NativeKidnappedInteractionEventGuid = ForceEncounterConstants.EventGuids.NativeKidnappedInteraction;
        public const string PrisonerEntryEventGuid = ForceEncounterConstants.EventGuids.PrisonerEntry;
        public const string ExecutePrisonerOptionKey = ForceEncounterConstants.Options.ExecutePrisonerKey;
```

- [ ] **Step 5: Inject in BackendPlugin**

In `ForceEncounter/ForceEncounter.Backend/BackendPlugin.cs`, replace the body of `EnsureHostileMenuOption()`:

```csharp
        private static void EnsureHostileMenuOption()
        {
            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.NativeEnemyInteractionEventGuid,
                ForceEncounterEventIds.EventGuid,
                ForceEncounterEventIds.OptionKey);
            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.NativeKidnappedInteractionEventGuid,
                ForceEncounterEventIds.PrisonerEntryEventGuid,
                ForceEncounterEventIds.ExecutePrisonerOptionKey);
            DebugLog("Ensured hostile menu option");
        }
```

- [ ] **Step 6: Build events + backend, run contract tests**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Run: `dotnet build ForceEncounter/ForceEncounter.Backend/ForceEncounter.Backend.csproj`
Expected: Build succeeded for both.
Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add ForceEncounter/ForceEncounter.Events/ForceEncounterEventPackage.cs ForceEncounter/ForceEncounter.Backend/ForceEncounterEventIds.cs ForceEncounter/ForceEncounter.Backend/BackendPlugin.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: register prisoner events and inject into kidnapped menu"
```

---

### Task 8: Prisoner outer tip config

**Files:**
- Create: `ForceEncounter/Config/EventOptionTipsInfoPrisoner.lua`
- Modify: `tests/TaiwuMods.Tests/Program.cs`

**Interfaces:**
- Consumes: `ExecutePrisonerGuid` (`7a1c2d3e-4f50-4617-8293-0a1b2c3d4e60`).
- Produces: hover tip for the prisoner outer option. Auto-validated by the existing `ModConfigPatchesReferenceFormalConfigNames` test (SrcConfigRefName must exist, DestConfigRefName/TemplateId must not collide with the formal mapping).

- [ ] **Step 1: Add the failing contract assertions**

Append to `ForceEncounterPrisonerInteractionContract()`:

```csharp
        string prisonerTip = ReadModFile(mod.Name, "Config", "EventOptionTipsInfoPrisoner.lua");
        Assert(prisonerTip.Contains("ConfigName = \"EventOptionTipsInfo\"", StringComparison.Ordinal), "ForceEncounter prisoner tip should add native event option help metadata");
        Assert(prisonerTip.Contains("SrcConfigRefName = \"袭击\"", StringComparison.Ordinal), "ForceEncounter prisoner tip should clone the formal hostile tips row");
        Assert(prisonerTip.Contains("Guid = { \"7a1c2d3e-4f50-4617-8293-0a1b2c3d4e60\" }", StringComparison.Ordinal), "ForceEncounter prisoner tip should map to the prisoner execute option guid");
        Assert(prisonerTip.Contains("作罢不消耗行动力", StringComparison.Ordinal), "ForceEncounter prisoner tip should explain that abandon does not consume action time");
        Assert(!prisonerTip.Contains("开战", StringComparison.Ordinal), "ForceEncounter prisoner tip should not mention combat");
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: FAIL — `Could not find file ...EventOptionTipsInfoPrisoner.lua` surfaced as the prisoner contract failure.

- [ ] **Step 3: Create `ForceEncounter/Config/EventOptionTipsInfoPrisoner.lua`**

```lua
return {
    ConfigName = "EventOptionTipsInfo",
    SrcConfigRefName = "袭击",
    DestConfigRefName = "ForceEncounter.PrisonerOptionTip",
    TemplateId = 119,
    Data = {
        Title = "情难自已",
        Desc = "对已被你擒下、无力反抗之人为所欲为。若情投意合则两厢情愿；否则径直得手。作罢不消耗行动力。",
        Guid = { "7a1c2d3e-4f50-4617-8293-0a1b2c3d4e60" },
    },
}
```

- [ ] **Step 4: Run contract tests**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS — including `config declarations match project outputs` and `mod config patches reference formal config names` (TemplateId 119 and DestConfigRefName `ForceEncounter.PrisonerOptionTip` do not collide with the formal mapping).

- [ ] **Step 5: Commit**

```bash
git add ForceEncounter/Config/EventOptionTipsInfoPrisoner.lua tests/TaiwuMods.Tests/Program.cs
git commit -m "ForceEncounter: add prisoner outer option tip config"
```

---

### Task 9: Documentation

**Files:**
- Modify: `ForceEncounter/README.md`
- Modify: `ForceEncounter/docs/development-notes.md`

**Interfaces:** none (docs only).

- [ ] **Step 1: Update README**

In `ForceEncounter/README.md`, add a subsection under `## 功能` (after the `### 敌对互动选项` section):

```markdown
### 关押目标互动选项

对已被太吾关押（俘虏）的目标，在俘虏面板「互动」菜单（原生擒获互动事件 `2e651ccb`）中加入独立的「情难自已」入口。该入口与普通敌对入口相互独立：拥有自己的外层提示、内层说明与反馈文案。亲密判定照常运行；非亲密目标因已被制服、无力反抗，选择「更进一步」后不进入战斗，直接按强制成功结算并保留关押状态。行动力成本与普通强制路线一致；「其他话题」返回俘虏菜单，不消耗、不结仇。
```

- [ ] **Step 2: Update development notes**

In `ForceEncounter/docs/development-notes.md`, append under `## 事件流程`:

```markdown
- 关押目标走独立事件链：`关押外层入口 → 关押内层选择 → 关押强制反馈 / 关押亲密反馈`（全部新 GUID，注册在同一事件包）。入口注入到原生擒获互动菜单 `2e651ccb`（`KidnappedCharacterClicked = 12` 触发，`TargetRoleKey = CharacterId`），与普通敌对菜单 `7c70ce0c` 互斥。
- 关押强制路线不经 `StartForcedCombat`：内层「更进一步」直接以 `battleSucceeded = true` 调共享后端强制结算（俘虏从不开战），再进入关押强制反馈页；不触发护卫、开战戒心或战斗结果路由。
- 关押链文案独立于普通链：`ForceEncounterPrisonerText` 维护关押内层说明（亲密/强制 × 成年/未成年）、关押强制成功反馈（成年/未成年 × 村民）与关押亲密反馈；普通 `ForceEncounterEventText` 不改。
- 外层提示独立：`Config/EventOptionTipsInfoPrisoner.lua`（TemplateId 119，绑定关押外层选项 GUID），去掉开战措辞；按钮标签沿用「（情难自已……）」。
```

- [ ] **Step 3: Run contract tests (sanity)**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ForceEncounter/README.md ForceEncounter/docs/development-notes.md
git commit -m "ForceEncounter: document prisoner interaction entry"
```

---

### Task 10: Full build, deploy, and in-game verification

**Files:** none (verification only).

**Interfaces:** none.

- [ ] **Step 1: Full solution build**

Run: `dotnet build ForceEncounter/ForceEncounter.Events/ForceEncounter.Events.csproj`
Run: `dotnet build ForceEncounter/ForceEncounter.Backend/ForceEncounter.Backend.csproj`
Expected: Build succeeded (no warnings about unresolved game refs).

- [ ] **Step 2: Run the full contract suite**

Run: `dotnet run --project tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`
Expected: `All Taiwu mod contract tests passed.`

- [ ] **Step 3: Deploy to the game**

Run: `pwsh ./deploy.ps1` (bumps the auto-increment 4th version segment in `config.lua` + backend `[PluginConfig]`, builds, and copies into the game Mods folder).
Expected: deploy completes; `config.lua` Version and backend `[PluginConfig]` version stay equal and on the `1.0.0.x` line.

- [ ] **Step 4: In-game verification (manual checklist)**

Launch the game, enable ForceEncounter in 模组管理 if needed, load a save, and capture a non-baby NPC so they are Taiwu's prisoner. Then verify:
  1. Open the prisoner panel → 互动 menu shows 「（情难自已……）」; hover shows the prisoner tip with no 「开战」 wording.
  2. Adult ordinary prisoner: click → inner page shows the captive forced description + 「（更进一步……）」 and 「其他话题」.
  3. Choose 「其他话题」 → returns to the prisoner 互动 menu; no combat, no failure record, no enmity, no action-time spent.
  4. Choose 「更进一步」 → no combat; prisoner forced-success feedback shows; life record / favorability / enmity / secret match a normal forced success; prisoner stays imprisoned.
  5. Intimate-eligible prisoner (e.g., imprisoned spouse): click → 「（半推半就……）」 commit → prisoner intimate feedback; no combat, no forced enmity.
  6. Minor prisoner: inner description shows the minor captive variant; feedback uses the minor success text.
  7. Action-time cost: 「更进一步」/「半推半就」 commit consumes `ActionTimeCostDays`; 「其他话题」 consumes nothing.
  8. Regression: a normal (un-imprisoned) NPC's 敌对 「情难自已」 in `7c70ce0c` behaves and reads exactly as before.

- [ ] **Step 5: Commit any deploy-version sync**

```bash
git add ForceEncounter/config.lua ForceEncounter/ForceEncounter.Backend/BackendPlugin.cs
git commit -m "ForceEncounter: sync deployed prisoner-entry build version"
```

(Only if `deploy.ps1` changed the version files. If nothing changed, skip.)

## Self-Review Notes

- **Spec coverage:** A=Task1, B(events P1–P4)=Tasks 3–6, C(text)=Task 2, D(tip)=Task 8, E(register+inject)=Task 7, F(backend unchanged; injection only)=Task 7, G(docs+version)=Tasks 9/10. Flow steps 1–5 covered by Tasks 3–7; test plan = Task 10.
- **Normal-chain isolation:** No task edits `ForceEncounterEvent`, `ForceEncounterConsentChoiceEvent`, `ForceEncounterCombatResultEvent`, `ForceEncounterAcceptedResultEvent`, or `ForceEncounterEventText` — the contract assertions pinning those (e.g., `构造内层说明(亲密通过, 未成年, 有护卫)`, abandon `?? 原生敌对菜单`) remain satisfied.
- **No literal SetContent:** prisoner events call `SetContent(ForceEncounterEventText.按钮.*)`, never a string literal (keeps `!SetContent("` assertion green).
- **Version:** no manual version edit anywhere (keeps `StartsWith("1.0.0.")`); deploy auto-increments the 4th segment.
- **TemplateId 119 / DestConfigRefName `ForceEncounter.PrisonerOptionTip`:** verified free against the formal `EventOptionTipsInfo.ref.txt` (max id 117; 118 used by the existing tip).
