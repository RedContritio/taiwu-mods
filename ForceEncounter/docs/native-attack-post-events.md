# 原生出手袭击后事件树

本文记录正式版「敌对 - 出手袭击」在战斗开始后和战斗结束后的原生事件路径，供 `ForceEncounter` 后续扩展“像袭击一样有丰富的事件后行动”时对齐。

## 资料来源

- 事件代码：`_scratch/decompiled-events/CharacterInteraction_Oppose/ConchShip.EventConfig.Taiwu/*.cs`
- 原生中文文本：`%TAIWU_GAME_DIR%/Event/EventLanguages/Taiwu_EventPackage_CharacterInteraction_Oppose_Language_CN.txt`
- 护卫状态：`_scratch/decompiled-gamedata-check/CharacterDomain/GameData.Domains.Character.CharacterDomain.decompiled.cs`

原生事件代码里的 `GetReplacedContentString()` 和 `OnOption*GetReplacedContent()` 经常是 `string.Empty`，真实显示文本来自 `EventLanguages`。查袭击事件时必须同时看二者。

## CombatResultType

`CombatResult` 是战斗系统写入 `ArgBox["CombatResult"]` 的结果枚举。原生袭击后事件只按它路由，不在 router 里重新判定胜负。

| 值 | 名称 | `IsPlayerWin` | 原生袭击语义 |
| --- | --- | --- | --- |
| `0` | `PlayerWin` | 是 | 太吾胜，目标或主敌被击倒，进入处置页或卫起保护页 |
| `1` | `EnemyWin` | 否 | 太吾败，进入失败反馈 |
| `2` | `PlayerFlee` | 否 | 太吾逃跑 |
| `3` | `EnemyFlee` | 是 | 敌方逃跑；对“是否让目标失去护卫”来说仍视为太吾赢过这场阻拦 |
| `4` | `PlayerDie` | 否 | 太吾死亡，进入失败反馈 |
| `5` | `EnemyDie` | 是 | 敌方战死；若允许直接处决则立即调用击杀 helper，否则进入处决页 |

## CharacterLoseGuard

`EventHelper.CharacterLoseGuard(charId, combatType)` 是护卫状态副作用，不是一个可见事件。

已确认实现语义：

- 只有 `combatType` 为 `BeatNormal = 1` 或 `DieNormal = 2` 时，才调用 `DomainManager.Character.LoseGuard(...)`。
- `CharacterDomain.LoseGuard` 把 `charId` 加入 `_unguardedChars` 并刷新状态。
- `CharacterDomain.RecoverGuards` 清空 `_unguardedChars`，原生会在月度推进的状态更新阶段调用。
- 因此它表示“此人物在本月/月结前暂时失去护卫保护/被打散护卫保护”，不是杀死护卫、伤害护卫或修改关系。

原生袭击调用位置：

- `7ffdb41d-1e91-446e-b5b0-08e4de29b9b3`，即「有护卫死斗」战斗结果 router。
- 进入 router 后先读 `CombatResult` 和 `CombatType`。
- 若 `CombatResultType.IsPlayerWin(result)` 为真，先对原目标 `CharacterId` 调用 `CharacterLoseGuard(CharacterId, CombatType)`，再进入后续事件。

注意：无护卫/直接打目标的 router `f84d7117-51d4-4eab-98e7-c8f70d00547f` 不调用 `CharacterLoseGuard`。

## 原生袭击提交入口

原生敌对事件中「出手袭击」提交选项的主要逻辑位于 `TaiwuEvent_4ea2bec5...OnOption3Select`。

提交时先发生这些副作用：

- 选项可用条件要求行动力大于 5，消耗 `OptionConsumeInfo(type: 8, value: 5, auto: true)`。
- `ChangeRoleInfectedValue(RoleTaiwu, 5)`。
- 对目标扣太吾好感 `ChangeFavorabilityOptionalRepeatedEvent(target, RoleTaiwu, -9000)`。
- `ApplyRelationBecomeEnemy(target, RoleTaiwu)`。
- `ChangeAlertnessOnAttack(targetId)`。

之后再进入战斗/伤重分支：

- 若目标有护卫：`PrepareCombatEnemy(targetId, DieNormal, false)`。若队首不是目标，设置 `Partner` 并进入 `9638c0a8` 护卫出面；否则直接用队伍开战，完成事件 `7ffdb41d`。
- 若目标无护卫且 `IsCharacterDirectFallenInCombat(targetId, Die)` 为真：不启动战斗，进入 `f3a9e752` 伤重处置页。
- 若目标无护卫且能战斗：直接与目标开战，完成事件 `f84d7117`。

## 战斗开始后的两个结果 Router

### 无护卫或目标本人战斗：`f84d7117-51d4-4eab-98e7-c8f70d00547f`

事件名：`惩戒-3出手袭击-死斗`。该事件无可见文本，是纯 router。

进入条件：直接 `StartCombat(targetId, DieNormal, f84d...)`，或有护卫队伍但实际主敌仍是目标本人。

路由：

- 若 `CharIdSeizedInCombat == CharacterId` 且有 `ItemKeySeizeCharacterInCombat`：调用 `AddPrisonerToCharacter`，设置 `Prisoner`，进入 `a96209d2` 关押选择页。
- `EnemyWin` 或 `PlayerDie`：进入 `324ada7c` 太吾失败页。
- `PlayerWin`：若目标未被卫起保护，进入 `aee604fc` 目标倒地处置页；否则进入 `95359f82` 卫起保护页。
- `PlayerFlee`：进入 `26d87715` 太吾逃跑页。
- `EnemyFlee`：进入 `c3d48308` 目标逃跑页。
- `EnemyDie`：若 `GetAllowExecute()` 为真，直接关闭并调用 `HandleCombatResultKillEnemy(RoleTaiwu, CharacterId, true)`；否则进入 `52fcb445` 处决选择页。
- 结果缺失：关闭。

### 有护卫/多人战斗：`7ffdb41d-1e91-446e-b5b0-08e4de29b9b3`

事件名：`惩戒-3出手袭击-有护卫死斗`。该事件无可见文本，是纯 router。

进入条件：护卫出面后 `StartCombat(enemyTeam 或 guardId, DieNormal, 7ffd...)`。

通用前置副作用：

- 若 `IsPlayerWin(CombatResult)` 为真，对原目标 `CharacterId` 调用 `CharacterLoseGuard(CharacterId, CombatType)`。
- 若战斗中抓到原目标：调用 `AddPrisonerToCharacter`，设置 `Prisoner`，进入 `a96209d2`。
- 若战斗中抓到的不是原目标：调用 `AddPrisonerToCharacter`，设置 `Prisoner`，让原目标 `CharacterEscapeToNearbyBlock(..., 1, true)`，进入 `11dd56cb` 同道/护卫关押选择页。

若 `MainEnemyId` 对应人物 `CreatingType != 1`（非智能主敌）：

- 太吾胜、敌方逃跑或敌方死亡：让原目标逃走，进入 `bd26f019` “得胜但目标逃走”页。
- 太吾败或太吾死亡：进入 `66c2adc4` 非智能死斗失败页。
- 太吾逃跑：进入 `751cd0ba` 有护卫失败非智能页。

若 `MainEnemyId != CharacterId` 且主敌是智能人物（实际打的是护卫/同道）：

- 先设置 `Partner = MainEnemyId`。
- 若太吾胜，则让原目标逃走。
- `EnemyDie`：若 `GetAllowExecute()` 为真，直接击杀 `Partner`；否则进入 `ce332722` 护卫/同道处决选择页。
- `PlayerWin`：若 `Partner` 未被卫起保护，进入 `ad16ffb4` 护卫/同道倒地处置页；否则进入 `4bc9d2fb` 同道被卫起保护页。
- `EnemyWin` 或 `PlayerDie`：进入 `b059b3eb` 被护卫/同道击败页。
- `PlayerFlee`：进入 `fd1deaf7` 太吾甩脱护卫/同道页。
- `EnemyFlee`：进入 `a2877dd3` 护卫/同道甩脱太吾页。

若 `MainEnemyId == CharacterId`：

- 退化为目标本人战斗，路由与 `f84d7117` 基本一致：`EnemyDie -> 52fcb445/直接击杀`，`PlayerWin -> aee604fc/95359f82`，`PlayerFlee -> 26d87715`，`EnemyFlee -> c3d48308`，`EnemyWin/PlayerDie -> 324ada7c`。

## 玩家可见事件页

### 护卫出面

`9638c0a8-fadf-4f6a-bb22-05f3aed994ed`

- 名称：`惩戒-3出手袭击-0有护卫战前`
- 文本：正欲出手时，护卫横挡身前；提示若要向目标出手须先一战。
- 选项：`（只得如此……）`
- 去向：与护卫/队伍开战，完成事件为 `7ffdb41d`。

### 目标已伤重/无力应战

`f3a9e752-b8f6-41d4-840b-f0b81ba9dcf2`

- 名称：`惩戒-3出手袭击-伤重`
- 文本：目标身负重伤，毫无还手之力。
- 选项：
  - `（使用绳索关押<Character key=CharacterId str=Name/>……）` -> `b93c4b99`
  - `（公开处死……）` -> `5d2a7aa7`
  - `（秘密杀害……）` -> `15b06fc4`
  - `（暂且作罢……）` -> 关闭

`b93c4b99-ed5e-49dc-8a20-9176c31a2bd1`

- 名称：`惩戒-3出手袭击-伤重-选择绳索`
- 文本：空，进入物品选择。
- 进入：设置 `SelectItemFilter.TemplateId = 62`，调用 `TaiwuSelectItem` 选择绳索。
- 选项：
  - `（确认选择……）`：若有绳索且 `CheckRopeHitOutOfCombat(..., 2, rope, true)` 成功，调用 `AddPrisonerToCharacter` 并进入 `a96209d2`；若有绳索但失败，进入 `e9d34ab6`；无绳索则关闭。
  - `（取消选择……）` -> 返回 `f3a9e752`

`e9d34ab6-0036-400a-a420-b7b2451578d2`

- 名称：`惩戒-3出手袭击-伤重-劫持失败`
- 文本：想把目标五花大绑，但目标挣脱逃走。
- 选项：`（无可奈何……）`
- 去向：若 `CheckTaskInProgress(647)`，进入 `9d06a849`；否则关闭。

### 目标倒地后的处置

`aee604fc-c0b8-468e-bf51-8665e2844c00`

- 名称：`惩戒-3出手袭击-死斗胜利`
- 文本：太吾将目标击倒，目标再无还手之力，并提示双方结仇。
- 选项：
  - `（公开处死！）`：若目标未被卫起保护，进入 `5d2a7aa7`；否则进入 `95359f82`。
  - `（秘密杀害……）`：若 `ArgBox` 含 `NotShowKillPrivate` 则隐藏；若目标未被卫起保护，进入 `15b06fc4`；否则进入 `95359f82`。
  - `（任其离开……）` -> `59bc16e6`

`52fcb445-f6a9-4f72-86fd-f8927d05f5cb`

- 名称：`惩戒-3出手袭击-死斗胜利-处决`
- 触发：战斗结果为 `EnemyDie`，但不允许直接 `GetAllowExecute()` 处决。
- 选项：
  - `（公开处死！）` -> `5d2a7aa7`
  - `（秘密杀害……）` -> `15b06fc4`

`5d2a7aa7-4e08-40cc-abae-8a535db84c79`

- 名称：`惩戒-3出手袭击-死斗胜利-1`
- 文本：众目睽睽下处死目标。
- 进入副作用：`HandleCombatResultKillEnemy(RoleTaiwu, CharacterId, true)`。
- 选项：`（如此便好！）`。
- 去向：若目标是 `YouthCharId`、`GirlCharId`、`BigWigCharId`、`ChildCharId` 之一，进入 `806f2eb7-bfbe-43c7-8ba5-f083739b4d01`（`小村特殊-老道驱赶`）；否则若任务 647 进行中进入 `9d06a849`；再否则返回 `FromEventGuid`，没有该参数则关闭。

`15b06fc4-7e2e-4a05-a491-c65758596518`

- 名称：`惩戒-3出手袭击-死斗胜利-2`
- 文本：趁四下无人秘密杀害目标。
- 进入副作用：`HandleCombatResultKillEnemy(RoleTaiwu, CharacterId, false)`。
- 选项：`（如此便好！）`。
- 去向：同公开处死目标页，先检查主线全局人物，再检查任务 647，最后返回 `FromEventGuid`。

`59bc16e6-f828-4b73-bd19-12c6405fdae9`

- 名称：`惩戒-3出手袭击-死斗胜利-3`
- 文本：不再向重伤目标出手，任其离去。
- 进入副作用：`HandleCombatResultReleaseEnemy(RoleTaiwu, CharacterId)`。
- 选项：`（……）`，若任务 647 进行中进入 `9d06a849`，否则返回 `FromEventGuid`，没有该参数则关闭。

### 关押选择

`a96209d2-bd08-4cda-9881-f6301dff22f0`

- 名称：`惩戒-3出手袭击-死斗劫持`
- 文本：太吾将 `Prisoner` 击倒并五花大绑。
- 选项：
  - `（公开关押！）` -> `4aeae75a`
  - `（秘密关押……）` -> `a7848767`
  - `（放其离开……）` -> `c11442fc`

`11dd56cb-239c-4f05-8142-23e2ed2604c7`

- 名称：`惩戒-3出手袭击-死斗劫持同道`
- 文本：太吾绑住 `Prisoner`，原目标已经逃走。
- 选项同 `a96209d2`。

`4aeae75a-19a3-41eb-a5ee-7ad8830f9d20`

- 名称：`惩戒-3出手袭击-死斗劫持-1`
- 文本：公开挟持 `Prisoner`，提示结仇并公开关押。
- 进入副作用：添加公开绑架人生经历，创建公开绑架密闻。
- 选项：`（这便随我走罢！）`。
- 去向：若 `Prisoner` 是 `YouthCharId`、`GirlCharId`、`BigWigCharId`、`ChildCharId` 之一，进入 `806f2eb7-bfbe-43c7-8ba5-f083739b4d01`（`小村特殊-老道驱赶`）；否则若任务 647 进行中进入 `9d06a849`；再否则关闭。

`a7848767-2921-474c-9bb0-b3273eb65ef6`

- 名称：`惩戒-3出手袭击-死斗劫持-2`
- 文本：秘密挟持 `Prisoner`，提示结仇并秘密关押。
- 进入副作用：添加秘密绑架人生经历，创建秘密绑架密闻。
- 选项：`（这便随我走罢！）`
- 去向：同公开关押页，先检查主线全局人物，再检查任务 647，最后关闭。

`c11442fc-29d7-4dfa-82f5-5baf48623acc`

- 名称：`惩戒-3出手袭击-死斗劫持-释放`
- 文本：不再向重伤的 `Prisoner` 出手，任其离去。
- 进入副作用：`RemovePrisonerFromCharacter(Prisoner, RoleTaiwu, false)`，再 `HandleCombatResultReleaseEnemy(RoleTaiwu, Prisoner)`。
- 选项：`（……）`，若任务 647 进行中进入 `9d06a849`，否则关闭。

### 护卫/同道倒地后的处置

`ad16ffb4-63fa-4282-b67c-c784beecdf0c`

- 名称：`惩戒-3出手袭击-有护卫死斗胜利`
- 文本：太吾将 `Partner` 击倒，原目标已经逃走。
- 选项：
  - `（公开处死！）` -> `bd458f6f`，若保护判定触发则进 `95359f82`
  - `（秘密杀害……）` -> `da784c8e`，若保护判定触发则进 `95359f82`
  - `（任其离开……）` -> `5530c4df`

`ce332722-bd28-405f-be7d-c70d779d2f2e`

- 名称：`惩戒-3出手袭击-有护卫死斗胜利处决`
- 触发：`Partner` 战死但不能直接处决。
- 选项：
  - `（公开处死！）` -> `bd458f6f`
  - `（秘密杀害……）` -> `da784c8e`

`bd458f6f-8b94-41c8-84b4-d739183cebc0`

- 名称：`惩戒-3出手袭击-死斗胜利-1同道`
- 文本：公开处死 `Partner`。
- 进入副作用：`HandleCombatResultKillEnemy(RoleTaiwu, Partner, true)`。
- 选项：`（如此便好！）`，若任务 647 进行中进入 `9d06a849`，否则关闭。

`da784c8e-2691-46a3-8ea1-280f3c13cde8`

- 名称：`惩戒-3出手袭击-死斗胜利-2同道`
- 文本：秘密杀害 `Partner`。
- 进入副作用：`HandleCombatResultKillEnemy(RoleTaiwu, Partner, false)`。
- 选项：`（如此便好！）`，若任务 647 进行中进入 `9d06a849`，否则关闭。

`5530c4df-4738-441c-95d0-839ad86f690f`

- 名称：`惩戒-3出手袭击-死斗胜利-3同道`
- 文本：放 `Partner` 离去。
- 进入副作用：`HandleCombatResultReleaseEnemy(RoleTaiwu, Partner)`。
- 选项：`（……）`，若任务 647 进行中进入 `9d06a849`，否则关闭。

## 单页反馈

这些页面没有后续可选处置，主要用于展示结果。

| Guid | 名称 | 触发 | 选项去向 |
| --- | --- | --- | --- |
| `324ada7c-c41c-4cbf-bef8-540631b39d18` | `惩戒-3出手袭击-死斗失败` | 目标本人战斗中太吾败或死亡 | 触发旧 passing event 后关闭 |
| `f75b2836-07ba-4169-a333-92d915853310` | `惩戒-3出手袭击-死斗失败-传剑后续` | 由 `324ada7c` 以 `TriggerLegacyPassingEvent` 触发 | `OnCheckEventCondition` 调用 `ExitMajorEvent` 后返回 false |
| `95359f82-778c-4cbd-82dc-0030ccce0956` | `惩戒-3出手袭击-死斗敌方被卫起保护` | 目标被击倒但被卫起保护 | 任务 647 -> `9d06a849`，否则返回 `FromEventGuid` |
| `26d87715-a994-44a4-9b9d-7f4df64bacee` | `惩戒-3出手袭击-死斗逃跑` | 太吾逃跑 | 关闭 |
| `c3d48308-d491-4157-8049-f2941f2ae0c4` | `惩戒-3出手袭击-死斗敌方逃跑` | 目标逃跑 | 任务 647 -> `9d06a849`，否则关闭 |
| `bd26f019-9e59-4c1d-8481-3800c32042c7` | `惩戒-3出手袭击-有护卫死斗胜利-非智能` | 非智能主敌场景，太吾胜但目标逃走 | 任务 647 -> `9d06a849`，否则关闭 |
| `66c2adc4-9a28-4fa1-85f7-09f7d855131b` | `惩戒-3出手袭击-非智能死斗失败` | 非智能主敌场景，太吾败或死亡 | 关闭 |
| `751cd0ba-4518-4ecd-abb7-716801ef382e` | `惩戒-3出手袭击-有护卫死斗失败-非智能` | 非智能主敌场景，太吾逃跑 | 关闭 |
| `4bc9d2fb-7a06-42d7-84fa-a990de310e93` | `惩戒-3出手袭击-死斗同道被卫起保护` | `Partner` 被击倒但被卫起保护 | 任务 647 -> `9d06a849`，否则关闭 |
| `b059b3eb-a93b-419a-af4b-f8eaec7d3623` | `惩戒-3出手袭击-有护卫死斗失败` | 被 `Partner` 击败或太吾死亡 | 关闭 |
| `fd1deaf7-9dbb-4cc0-836e-4ce44a4eac99` | `惩戒-3出手袭击-有护卫死斗逃跑` | 太吾甩脱 `Partner` | 关闭 |
| `a2877dd3-92f1-44dd-ac3b-62616961c668` | `惩戒-3出手袭击-有护卫死斗敌方逃跑` | `Partner` 甩脱太吾 | 任务 647 -> `9d06a849`，否则关闭 |
| `52672b30-655b-4528-8c37-31e8a0733946` | `惩戒-3出手袭击-非智能战斗结束` | 反编译包内有大量其他敌对事件引用；未见 `f84d7117`/`7ffdb41d` 主袭击 router 直接进入 | `OnCheckEventCondition` 返回 false，`OnEventEnter` 关闭 |

## 主线 647 后续

多个袭击反馈页在按钮选择时检查：

```csharp
if (EventHelper.CheckTaskInProgress(647))
{
    return "9d06a849-184e-4c3c-ae28-7fd4a1a29fc5";
}
```

`9d06a849-184e-4c3c-ae28-7fd4a1a29fc5` 不属于 `CharacterInteraction_Oppose` 文本文件，而在 `Taiwu_EventPackage_NewMainStory_PreEvil_Language_CN.txt` 中，名称为 `欲念侵心-嗔-环节二-01`。这是袭击后的主线钩子，不是通用关闭页。

`ForceEncounter` 若要“像袭击一样”保留完整原生后行动，至少需要明确是否接入这条任务 647 后续；如果不接入，应在设计文档中说明原因，避免误以为原生只是关闭。

## 对 ForceEncounter 的实现含义

当前 `ForceEncounter` 已经复用/仿照了以下袭击路径：

- `DieNormal` 战斗类型。
- 有护卫时先准备敌方队伍，可能先打护卫。
- `CombatResultType.IsPlayerWin` 判定。
- 护卫路由胜利后调用 `CharacterLoseGuard`。
- 目标/太吾逃跑时调用 `CharacterEscapeToNearbyBlock`。
- 目标战死时延后调用击杀 helper。
- 战斗中绳索擒获原目标时调用 `AddPrisonerToCharacter`，先完成强制成功结算，再进入本 Mod 的擒获处置页。
- 本 Mod 的擒获处置页保留“公开关押”“秘密关押”和“放其离开”：公开/秘密关押保留俘虏状态并分别补对应劫持人生经历/密闻；释放调用 `RemovePrisonerFromCharacter` 与 `HandleCombatResultReleaseEnemy`。

仍未完整复刻的袭击后行动：

- 目标倒地后的三选项：公开处死、秘密杀害、任其离开。
- 目标已经无力应战时的四选项：绳索关押、公开处死、秘密杀害、作罢。
- 绳索选择与劫持失败反馈。
- 护卫/同道倒地后的处置树。
- 卫起保护、非智能主敌和任务 647 主线钩子的完整处理。

下一步若扩展 `ForceEncounter`，建议不要把所有结果继续挤进一个 `CombatResultEvent` 文本函数，而是按原生拆为：

- 纯 router：读取 `CombatResult`、`MainEnemyId`、`Prisoner` 等参数，只决定下一个事件。
- 处置选择页：只显示玩家选项，不直接混入后端结算。
- 终端反馈页：进入时调用对应原生 helper 或 `ForceEncounter` 后端，再显示结果。
