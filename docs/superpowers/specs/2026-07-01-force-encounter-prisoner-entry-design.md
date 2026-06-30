# 情难自已：接入关押目标（Prisoner Entry，独立链版）设计

日期：2026-07-01
Mod：ForceEncounter（情难自已）

## 背景与问题

「情难自已」目前只把外层入口注入到普通互动菜单事件 `7c70ce0c`（`CharacterInteraction_Main`，原生「出手袭击」所在的那一页）。已被太吾关押的目标走的是另一条完全独立的路径：

- 俘虏面板「互动」按钮 → `OnInteractKidnappedCharacter` → 触发 `KidnappedCharacterClicked = 12`
- 对应独立事件包 `KidnappedCharacterInteraction`，入口事件 `2e651ccb-3a77-447a-a74f-c9a24a1a32d1`（`TriggerType = 12`，9 项菜单：释放/服食/给予/劝说/转移/处罚/没收/询问/暂无要事）
- 该菜单不含「情难自已」，也不复用 `7c70ce0c`，前端 `CanInteractWithKidnappedCharacter` 还显式要求 `!IsOnNormalInteractEvent`

结果：**对已关押目标，「情难自已」不会出现，因此不生效。**

后端结算 `ExecuteForcedAction`（`MakeLove`/好感惩罚/结仇/密闻/经历）是纯结算逻辑，可直接复用；其余 UI/事件/文案在关押态另起一条独立链。

## 目标

让玩家在俘虏互动菜单里也能对已关押目标使用「情难自已」。关押态拥有**一整套独立的事件链与文案**（新 GUID），与普通路线完全隔离；普通路线的事件与文案一行不动。

## 设计原则：独立复制，而非分支复用

- 关押态复制一整套专用事件（外层入口 / 内层选择 / 强制反馈 / 亲密反馈）和文案，都用新 GUID。
- **后端结算 `ExecuteForcedAction` 共用**（纯结算逻辑，复制只会凭白重复）。
- 普通路线事件（`ForceEncounterEvent` / `ForceEncounterConsentChoiceEvent` / `ForceEncounterCombatResultEvent` / `ForceEncounterAcceptedResultEvent`）与共享文案构造器**不加任何 `已关押` 分支**。
- 隔离带来的简化：关押链天然只服务俘虏，因此**不需要**「目标是太吾俘虏」运行时判定、`构造内层说明` 的 `已关押` 维度、或返回菜单纠正助手——这些在独立链里都是常量行为。

## 已确认的设计决策

1. **交互形态**：关押内层保留二选一（更进一步=强制 / 其他话题），「其他话题」作反悔入口；说明与外层提示为关押情境专写。
2. **亲密判定**：照常运行——配偶/双向恋人/单恋太吾/太吾村民/符合条件的谷中密友仍可走亲密直通，进入关押链自己的亲密反馈页。
3. **入口开关**：总是开启，不新增设置。
4. **行动力成本**：与普通强制路线一致（沿用 `ActionTimeCostDays`，内层提交时消耗）。
5. **外层区分**：外层选项**提示 Desc** 出关押变体；外层**按钮标签**沿用「（情难自已……）」。
6. **复制边界**：事件链 + 文案独立复制（新 GUID）；后端结算共用。
7. **强制路径**：直接结算——俘虏从不开战，关押链跳过战斗启动/护卫/战斗结果路由，「更进一步」后直接走成功结算 → 关押强制反馈页。

## 玩家可见流程（关押目标）

1. 俘虏面板对某关押目标点「互动」→ 进入俘虏互动菜单 `2e651ccb`。
2. 菜单出现「（情难自已……）」，悬浮提示为**关押变体**（无「开战」字眼）。
3. 点击 → 关押外层入口探测（共用后端探测模式）：
   - 亲密判定通过 → 关押内层显示「半推半就」提交 → 提交后进入**关押亲密反馈页**。
   - 不通过 → 关押内层显示「更进一步 / 其他话题」，内层说明为**关押变体**（对方已被缚、无从反抗，无开战/护卫措辞）。
4. 选「更进一步」→ **直接调用后端强制结算**（`battleSucceeded=true`，无战斗）→ **关押强制反馈页**显示「已关押成功」文案，保留关押状态。
5. 选「其他话题」→ 直接返回俘虏菜单 `2e651ccb`（关押链内硬路由，无需判定）。

## 关押事件链（全部新 GUID）

复制并裁剪自普通链，注册到事件包（`ForceEncounterEventPackage`）。`MainRoleKey=RoleTaiwu / TargetRoleKey=CharacterId`，与 `2e651ccb` 一致。

### P1. 关押外层入口（copy of `ForceEncounterEvent`）

- 含「情难自已·关押」execute 选项：`Behavior=BehaviorEgoistic`，预览成本（沿用 `ForceEncounterEventCosts` 预览，AutoConsume=false），`OnOptionVisibleCheck/OnOptionAvailableCheck` 复用既有可见性/可执行校验（存活、非婴儿、未成年设置）。
- 按钮标签沿用「（情难自已……）」。
- 选中：设置外层 wait-confirm 预览键 + 存交互参数 → 共用后端**探测模式**（`ResolutionMode=Probe`）→ 路由到 P2。
- 可选加固：可见性额外要求 `target.GetKidnapperId()==Taiwu`。

### P2. 关押内层选择（copy of `ForceEncounterConsentChoiceEvent`）

- 选项：`半推半就`(亲密提交，可见条件=探测为亲密通过) / `更进一步`(强制，可见条件=探测为需要战斗选择) / `其他话题`。
- 内层提交成本 `AutoConsume=true`，消耗 `ActionTimeCostDays`，并确认外层 wait；「其他话题」不确认、不消耗。
- 说明文案：关押专写（亲密通过用关押亲密说明；否则用关押强制说明，无开战/护卫）。
- `半推半就` → 共用后端**亲密提交**（`ResolutionMode=AcceptedCommit`）→ P4。
- `更进一步` → 确认外层 wait → 共用后端**强制结算**（`ResolutionMode=Combat`，`battleSucceeded=true`）→ P3。（不经 `StartForcedCombat`。）
- `其他话题` → 返回 `2e651ccb`。

### P3. 关押强制反馈（copy of `ForceEncounterCombatResultEvent`，大幅裁剪）

- 仅成功态（俘虏强制必成）。读取后端返回的 `targetIsTaiwuVillager`，按 成年/未成年 × 村民 选择「已关押成功」文案。
- 单「离开」按钮 → `ToEvent("")` 关闭。无失败/逃走/护卫/擒获分支，无战斗结果路由。

### P4. 关押亲密反馈（copy of `ForceEncounterAcceptedResultEvent`）

- 显示关押亲密成功文案（成年/未成年）；异常兜底沿用结构。单「离开」按钮关闭。

## 改动清单

### A. 常量（`ForceEncounter.Shared/ForceEncounterConstants.cs`）

- `EventGuids.NativeKidnappedInteraction = "2e651ccb-3a77-447a-a74f-c9a24a1a32d1"`
- 关押链事件 GUID：`PrisonerEntry` / `PrisonerConsentChoice` / `PrisonerForcedResult` / `PrisonerAcceptedResult`（各新生成）
- 关押链选项 key/guid：外层 `ExecutePrisoner*`、内层 `半推半就/更进一步/其他话题`、反馈 `离开/继续` 各一套新值（避免与普通链选项 GUID 冲突）

### B. 关押事件实现（`ForceEncounter.Events/` 新增 4 个事件类）

`ForceEncounterPrisonerEntryEvent` / `ForceEncounterPrisonerConsentChoiceEvent` / `ForceEncounterPrisonerForcedResultEvent` / `ForceEncounterPrisonerAcceptedResultEvent`，按 P1–P4。可复用 `ForceEncounterEventRuntime`、`ForceEncounterEventCosts` 这类无状态工具（它们不是「事件」本身）。在 `ForceEncounterEventPackage` 注册这 4 个事件。

### C. 关押文案（`ForceEncounter.Events/` 新增 `ForceEncounterPrisonerText` 或独立 section）

- 关押外层提示 Desc（见 D）。
- 关押内层说明：成年亲密 / 未成年亲密 / 成年强制 / 未成年强制（强制版无开战、无护卫）。
- 关押强制反馈：成年/未成年 × 村民「已关押成功」（可沿用现有 `强制反馈.*已关押成功*` 文案的文本，迁入关押文案区）。
- 关押亲密反馈：成年/未成年成功 + 异常兜底。
- 普通链的 `ForceEncounterEventText` 共享构造器**不动**。

### D. 外层提示配置（`ForceEncounter/Config/` 新增一份 lua）

新增 `EventOptionTipsInfo` 配置（与现有 `Config/EventOptionTipsInfo.lua` 同加载机制），绑定关押外层选项新 GUID：

- `Title = "情难自已"`，按钮标签沿用「（情难自已……）」。
- `Desc`：关押变体，例如「对已被你擒下、无力反抗之人为所欲为。若情投意合则两厢情愿；否则径直得手。作罢不消耗行动力。」
- 实现时确认 `Config/*.lua` 加载/登记方式：现有两份 Config lua 已生效，沿用同机制；若需显式登记则一并补。关押入口**不需要** `InteractionEventOption`（那是地图块「敌对」自定义按钮；俘虏入口走 `AddOptionToEvent` 直接注入）。

### E. 入口注入（`ForceEncounter.Backend/BackendPlugin.cs`，`EnsureHostileMenuOption()`）

- 保留：`AddOptionToEvent(7c70ce0c, Entry普通, ExecuteKey普通)`
- 新增：`AddOptionToEvent(NativeKidnappedInteraction[2e651ccb], PrisonerEntry, ExecutePrisonerKey)`
- 两处注入都在 `OnEnterNewWorld` / `OnLoadedArchiveData` 执行（沿用现状）。

### F. 后端（不改）

- `ExecuteForcedAction` 共用：探测（`Probe`）、亲密提交（`AcceptedCommit`）、强制结算（`Combat` + `battleSucceeded=true`）均走现有路径；强制成功的好感惩罚/结仇/密闻/经历与普通强制一致。
- 关押链直接以 `battleSucceeded=true` 调强制结算，不依赖 `ForceEncounterCombatStarter` 的俘虏短路（该短路仍保留给普通链的边角情形，无需删除）。

### G. 版本与文档

- 按构建规则 bump 第四段版本（`autoIncrementBuildVersion`），同步 `config.lua` 与后端 `[PluginConfig]`。
- 更新 `README.md`、`docs/development-notes.md`：补「关押目标独立入口链」机制、`2e651ccb` 路径、关押外层/内层/反馈文案独立。

## 边界与一致性

- **菜单隔离**：普通选项只注入 `7c70ce0c`，关押选项只注入 `2e651ccb`；两菜单上下文互斥。
- **无战斗副作用**：关押强制不触发护卫准备、开战戒心、战斗结果路由；内层不显示护卫预警。
- **未成年/婴儿**：沿用 `允许未成年` 设置与可见性校验；关押文案含未成年变体。
- **普通链回归**：普通（未关押）目标在 `7c70ce0c` 的行为与文案完全不变。

## 测试计划

构建部署后入档，准备一名已被太吾关押的非婴儿目标：

1. 俘虏面板「互动」菜单出现「（情难自已……）」，提示为关押变体（无「开战」字眼）。
2. 普通成年俘虏：点击 → 关押内层显示关押强制说明 + 「更进一步 / 其他话题」。
3. 选「其他话题」→ 回到俘虏菜单 `2e651ccb`，不开战、不写失败、不结仇、不消耗行动力。
4. 选「更进一步」→ 不进入战斗，直接「已关押成功」反馈；经历/好感/结仇/密闻与普通强制成功一致，关押状态保留。
5. 亲密直通目标（如配偶被关押）：点击 → 半推半就提交 → 关押亲密反馈，不开战、不强制结仇。
6. 未成年俘虏：关押内层说明显示未成年变体；反馈走未成年已关押成功文案。
7. 普通（未关押）目标在 `7c70ce0c` 的行为与文案完全不变（回归）。
8. 行动力成本：内层「更进一步 / 半推半就」提交按 `ActionTimeCostDays` 消耗；「其他话题」不消耗。
