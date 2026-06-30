# 情难自已：接入关押目标（Prisoner Entry）设计

日期：2026-07-01
Mod：ForceEncounter（情难自已）

## 背景与问题

「情难自已」目前只把外层入口注入到普通互动菜单事件 `7c70ce0c`（`CharacterInteraction_Main`，原生「出手袭击」所在的那一页）。已被太吾关押的目标走的是另一条完全独立的路径：

- 俘虏面板「互动」按钮 → `OnInteractKidnappedCharacter` → 触发 `KidnappedCharacterClicked = 12`
- 对应独立事件包 `KidnappedCharacterInteraction`，入口事件 `2e651ccb-3a77-447a-a74f-c9a24a1a32d1`（`TriggerType = 12`，9 项菜单：释放/服食/给予/劝说/转移/处罚/没收/询问/暂无要事）
- 该菜单不含「情难自已」，也不复用 `7c70ce0c`，前端 `CanInteractWithKidnappedCharacter` 还显式要求 `!IsOnNormalInteractEvent`

结果：**对已关押目标，「情难自已」不会出现，因此不生效。**

后端其实已为此预留：`ForceEncounterCombatStarter.StartForcedCombat` 中的 `TargetIsAlreadyPrisonerOfActor`（`target.GetKidnapperId() == actor`）会跳过死战、直接按「已制服」判定强制成功；`ForceEncounterEventText.BuildCombatResultContent` 也已有 `TargetAlreadyPrisoner` 分支与「成年/未成年 已关押成功(村民)」四条反馈文案。缺的只是**外层入口**与**关押态专用说明文案**。

## 目标

让玩家在俘虏互动菜单里也能对已关押目标使用「情难自已」，且外层（选项提示）与内层（动作说明）文案都按「关押 vs 普通」区分；后端结算、反馈文案、成本、各类门槛全部沿用现成实现。

## 非目标

- 不改后端结算逻辑（成功/失败、好感惩罚、结仇、密闻、`MakeLove`）。
- 不改普通路线的任何文案或行为。
- 不为关押入口新增设置项（入口与普通入口一样总是随 Mod 启用而开启）。
- 不复刻原生俘虏菜单的其它处置（处罚/没收等）。

## 已确认的设计决策

1. **交互形态**：复用既有内层二选一页（强制关系 / 其他话题），保留「其他话题」作为反悔入口；但内层说明与外层提示为关押情境另写文案。
2. **亲密判定**：照常运行——配偶/双向恋人/单恋太吾/太吾村民/符合条件的谷中密友仍可走亲密直通，进入既有 `亲密反馈`（非战斗框架，文案已合适）。
3. **入口开关**：总是开启，不新增设置。
4. **行动力成本**：与普通强制路线完全一致（沿用 `ActionTimeCostDays`，内层提交时消耗）。
5. **外层区分**：外层选项**提示 Desc** 出关押变体；外层**按钮标签**沿用「（情难自已……）」。

## 玩家可见流程（关押目标）

1. 打开俘虏面板，对某关押目标点「互动」→ 进入俘虏互动菜单 `2e651ccb`。
2. 菜单中出现新选项「（情难自已……）」，悬浮提示为**关押变体**文案（无「开战」字眼）。
3. 点击 → 外层入口探测：
   - 亲密判定通过 → 内层显示「半推半就」提交 → 提交后进入既有 `亲密反馈`。
   - 不通过 → 内层显示「更进一步（强制）/ 其他话题」，内层说明为**关押变体**（对方已被缚、无从反抗，无开战/护卫措辞）。
4. 选「更进一步」→ `StartForcedCombat` 命中 `TargetIsAlreadyPrisonerOfActor` → 跳过死战，直接判定成功 → `战斗反馈` 用 `TargetAlreadyPrisoner` 分支显示「已关押成功」文案，保留原有关押状态。
5. 选「其他话题」→ 返回**俘虏菜单 `2e651ccb`**（而非普通菜单 `7c70ce0c`）。

## 改动清单

### A. 常量（`ForceEncounter.Shared/ForceEncounterConstants.cs`）

- `EventGuids.NativeKidnappedInteraction = "2e651ccb-3a77-447a-a74f-c9a24a1a32d1"`
- 关押态外层选项：`Options.ExecutePrisonerKey`（新 key）+ `Options.ExecutePrisonerGuid`（新生成 GUID）

### B. 外层入口选项（`ForceEncounter.Events/ForceEncounterEvent.cs`）

在外层入口事件中新增第二个 execute 选项「情难自已·关押」：

- `OptionKey/OptionGuid` 用上面新增的关押常量
- `Behavior = BehaviorEgoistic`，预览成本同现有 execute 选项
- `OnOptionVisibleCheck = IsVisible`、`OnOptionAvailableCheck = CanExecute`、`OnOptionSelect = Execute`（**复用同一处理器**，行为与普通入口一致）
- 按钮标签沿用 `按钮.情难自已`（「（情难自已……）」）

注：两个 execute 选项分别供两个菜单注入；同一 `Execute` 处理器自然适配关押/普通（差异只在文案，由下游判定）。

### C. 外层提示配置（`ForceEncounter/Config/` 下新增一份 lua）

新增一份 `EventOptionTipsInfo` 配置（与现有 `Config/EventOptionTipsInfo.lua` 同加载机制），绑定到关押选项新 GUID：

- `Title = "情难自已"`
- `Desc`：关押变体，去掉「开战」字眼。示例：「对已被你擒下、无力反抗之人为所欲为。若情投意合则两厢情愿；否则径直得手。作罢不消耗行动力。」
- `Guid = { <ExecutePrisonerGuid> }`

实现时确认 `Config/*.lua` 的加载/登记方式：现有 `InteractionEventOption.lua`、`EventOptionTipsInfo.lua` 已生效，沿用同机制；若加载需显式登记，则一并补登记。关押入口**不需要**新增 `InteractionEventOption`（TemplateId 1099，那是地图块「敌对」子菜单的自定义按钮；俘虏入口走 `AddOptionToEvent` 直接注入）。

### D. 入口注入（`ForceEncounter.Backend/BackendPlugin.cs`，`EnsureHostileMenuOption()`）

- 保留：`AddOptionToEvent(7c70ce0c, Entry, ExecuteKey)`
- 新增：`AddOptionToEvent(NativeKidnappedInteraction[2e651ccb], Entry, ExecutePrisonerKey)`

`2e651ccb` 的 `MainRoleKey = RoleTaiwu / TargetRoleKey = CharacterId` 与现有流程一致，无需改参数。注入在 `OnEnterNewWorld` / `OnLoadedArchiveData` 两处都会执行（沿用现状）。

### E. 内层说明关押变体（`ForceEncounter.Events/ForceEncounterEventText.cs`）

- 新增 `入口说明.成年已关押` / `入口说明.未成年已关押`：俘虏情境措辞（对方已被缚、无从反抗），不含开战/护卫。
- `构造内层说明` 增加 `已关押` 维度：当 `已关押 && !亲密通过` 时返回上述文案，并忽略 `有护卫` 分支（俘虏无护卫拦截）。亲密通过分支不变（沿用 `成年/未成年亲密`）。

### F. 关押态判定 + 返回菜单纠正（`ForceEncounter.Events/ForceEncounterConsentChoiceEvent.cs` 与 `ForceEncounterEventRuntime`）

- `ForceEncounterEventRuntime` 新增助手 `目标是太吾俘虏(argBox)`：复用 `target.GetKidnapperId() == actorId`（与 `ForceEncounterCombatStarter.TargetIsAlreadyPrisonerOfActor` 同口径）。
- `ConsentChoiceEvent.GetReplacedContentString`：取「目标是太吾俘虏」传入 `构造内层说明`。
- 返回菜单纠正：`Abandon()`（其他话题）是唯一显式导航回菜单的出口，封一个 `决定返回菜单(argBox)` 助手——目标是太吾俘虏时返回 `NativeKidnappedInteraction[2e651ccb]`，否则沿用现有逻辑（`MainInteractionHeadEvent ?? 7c70ce0c`）。（探测失败、反馈页「离开/继续」等出口均为 `ToEvent("")` 关闭，无需纠正。）

### G. 不改动

- 后端 `ExecuteForcedAction` / 结算 / `TargetAlreadyPrisoner` 短路：已就绪。
- 战斗反馈「已关押成功」四条文案：已存在。
- 亲密反馈、成本、未成年/婴儿门槛、好感惩罚、结仇、密闻：沿用。

### H. 版本与文档

- 按构建规则 bump 第四段版本（`autoIncrementBuildVersion`），同步 `config.lua` 与后端 `[PluginConfig]`。
- 更新 `README.md`、`docs/development-notes.md`：补「关押目标入口」机制、`2e651ccb` 路径、外层/内层关押文案区分。

## 边界与一致性

- **菜单隔离**：普通选项只注入 `7c70ce0c`，关押选项只注入 `2e651ccb`；两菜单上下文天然互斥，不会同时出现。可选加固：关押选项可见性额外要求「目标是太吾俘虏」。
- **护卫/戒心**：关押态在 `StartForcedCombat` 命中俘虏短路前不触发护卫准备与开战戒心；内层说明也不显示护卫预警。
- **未成年**：沿用 `允许未成年` 设置与既有未成年说明/反馈文案（含已关押未成年变体）。

## 测试计划

构建部署后入档，准备一名已被太吾关押的非婴儿目标：

1. 俘虏面板「互动」菜单出现「（情难自已……）」，提示为关押变体（无「开战」字眼）。
2. 普通成年俘虏：点击 → 内层显示关押变体说明 + 「更进一步 / 其他话题」。
3. 选「其他话题」→ 回到**俘虏菜单**，不开战、不写失败记录、不结仇。
4. 选「更进一步」→ 不进入战斗，直接「已关押成功」反馈；检查经历/好感/结仇/密闻与普通强制成功一致，关押状态保留。
5. 亲密直通目标（如配偶被关押）：点击 → 亲密提交 → `亲密反馈`，不开战、不强制结仇。
6. 未成年俘虏：内层说明显示未成年 + 关押变体；反馈走未成年已关押成功文案。
7. 普通（未关押）目标在 `7c70ce0c` 的行为与文案完全不变（回归）。
