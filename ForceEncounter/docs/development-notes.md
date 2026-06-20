# ForceEncounter Development Notes

This file records ForceEncounter-specific design decisions and native-path findings.
Keep general Taiwu mod development workflow in the Codex skill; keep this file for
this mod's mechanics, user-approved design, and verified native side effects.

## Design Decisions

- The player-facing actor is Taiwu. Do not keep a setting that allows targeting Taiwu as the action target.
- Do not use combat power as a success gate. If force is required, start formal combat and use combat result.
- Do not let fertility block the player's intent.
- Costs belong on inner commit options, not the outer hostile menu entry.
- The accepted cost is 5 days of action time:

```csharp
new OptionConsumeInfo((sbyte)8, 5, true)
```

- Abandoning from the inner choice must not consume cost, start combat, add hatred, or record success/failure.
- If an intimate acceptance check passes, allow a non-combat branch but still record success through the same backend success path.
- If acceptance fails, show an inner choice: proceed with force/combat or abandon.
- Outer hostile rows may show `BehaviorEgoistic` / `「唯我」` styling, but behavior effects and costs must happen only when the user commits.
- Normal age and special-age presentations may differ, but their commit choices should stay mechanically aligned unless the user requests a different consequence.

## Versioning

- `ForceEncounter` / `情难自已` starts at `0.0.0.1` and uses four numeric version parts.
- `ModBuild/mods.json` marks it with `autoIncrementBuildVersion: true`.
- Artifact-producing builds increment only the fourth version component and synchronize `config.lua` with backend `[PluginConfig]`.
- Raw `dotnet build` is for development verification and does not bump the mod version.

## Event Flow

- Outer hostile menu option: `ForceEncounter.Execute` / `情难自已`; opens or probes but should not consume.
- Probe mode: checks whether relationship/personality permits non-combat acceptance; should not mutate relationship state.
- Accepted commit mode: after the user chooses the accepted inner option, applies success.
- Forced combat mode: after the user chooses force, starts formal combat and resolves success/failure from combat result.

## Relationship And Acceptance Rules

Current user-approved intent:

- Existing spouse whose spouse is alive and is not Taiwu counts as already attached to another person.
- Existing admiration counts as attached to another person only when exactly one admired character is alive and that character is not Taiwu.
- Multiple living admired characters imply the character is not exclusively attached.
- Dead admired characters do not count as current attachment.
- Taiwu villagers are generally easier for Taiwu.
- Existing attachment to another person cancels Taiwu village leniency back to normal difficulty.
- Implemented existing attachment means: the target has any living spouse other than Taiwu, or the target adores exactly one living character other than Taiwu. Multiple living adored characters, only dead adored characters, or sole adoration toward Taiwu do not count as an exclusive attachment to another person.
- For villagers only, a forced route may avoid hatred at sufficiently high relationship.
- Non-villagers should treat the forced route as severe unless the user changes the design.
- If the target unilaterally adores Taiwu, it is eligible for the intimate route and uses lover-like difficulty, but accepted success applies the same 30% favorability penalty discount as the Taiwu-villager forced route.
- `谷中密友` feature `685` gets a special acceptance route only for the native close-friend Taiwu id. If the target is already attached to another person, this route requires target-to-Taiwu favorability higher than native `Favorite2` (`type > 2`; `Language_CN/ui_language.txt` maps `LK_Favor_Type_8` to `融洽`) and applies a reduced favorability penalty on accepted success.
- Accepted-route success records rape success. Spouse, mutual lover, unattached villager, and unattached `谷中密友` accepted success does not add forced-route hatred or extra favorability beyond native ordinary talk semantics.
- Taiwu villagers bypass guard interception. Guard warnings and guard-front combat apply only to non-villager forced routes.

## Native Integration Findings

- `AddOptionToEvent` is the formal extension path for adding to the hostile event; it is not the same as editing the native compiled `EventOptions` array.
- Keep backend registration under runtime `ModIdStr`; avoid hard-coded development mod ids.
- Re-add runtime event extension on new world and loaded archive if event extension state can reset.
- Use `OptionConsumeInfos` for cost display and consumption. Do not write costs only into text.
- Use native helpers for relationship side effects when available. For hatred/enemy creation, prefer `EventHelper.ApplyRelationBecomeEnemy(target, actor)` over direct `DomainManager.Character.AddRelation` so native life records, secrets, and mood effects remain aligned.

## Native Side Effects

Useful confirmed paths:

- Native forced sexual encounter success calls `actor.MakeLove(context, target, isRape: true)`, records rape success, creates `SecretInformationCollection.AddRape(actorId, targetId)`, then adds it through `DomainManager.Information.AddSecretInformation`.
- `AddRape` creates secret information template `106`.
- `SecretInformationEffectItem(106)` has actor fame conditions and negative fame actions such as ids `66`, `63`, `62`, `65`, and `82`.
- Fame changes occur when the secret-information processor applies/disseminates the secret, not because favorability, alertness, or happiness changed directly.
- Failed forced encounter should not create the rape secret unless native code for that path does.
- Failed forced encounter may still create native hatred/enemy side effects if the selected branch calls `ApplyRelationBecomeEnemy`.
- Become-enemy secret template `115` exists, but its fame-apply content is effectively empty in the inspected build. Do not assume hatred itself lowers Taiwu fame.
- Native rape handling changes target favorability through `ChangeFavorabilityOptionalMonthlyEvolution`; it does not appear to directly change target happiness on that path.
- Native become-enemy behavior has personality-based happiness changes. Preserve those by using the native helper rather than custom relation mutation.
- Alertness does not directly change happiness. It affects favorability cap toward Taiwu, favorability delta percent by alertness level, and harmful interaction success rates.

## Test And Debug Expectations

- Debug mode can be enabled by default for rapid in-game testing when the user requests it, but keep it explicit and visible in config.
- Cover at least four paths in contract or manual test plans when touching the flow:
  - normal-age intimate acceptance
  - normal-age combat
  - special-age intimate acceptance
  - special-age combat
- Tests should lock native-path contracts:
  - use of `OptionConsumeInfos`
  - wait-confirm/cancel behavior
  - runtime mod id
  - native enemy helper
  - native rape secret creation when success is intended
