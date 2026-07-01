# EasyQuickSaveLoad — Galgame 式栏位存档 + 原生 ESC 嵌入

日期：2026-07-02
分支：dev

## 背景与目标

现状：EasyQuickSaveLoad 在 ESC 系统面板里挂了一个**自绘悬浮面板**（暗色底 + 4 个自绘按钮 + 内嵌摘要/状态文本 + 二次点击确认状态机）。存档采用"时间戳滚动环"模型（保留 N 个手动存档，N=原生回溯数），读档复用原生回溯列表 UI（RevertArchive，改标题字）。

问题：
1. 自绘面板与游戏原生 UI 风格不一致，"看着奇怪"。
2. 存档不能指定栏位（只是按时间滚动），读档走的是"回溯"备份 UI（语义/外观都是备份，不是可寻址存档栏）。

目标：改造成 **galgame 式栏位存档系统**，并把入口**原生嵌入**到 ESC 系统选项对话框：
- 1 个独立**快捷 SL 栏** + N 个可寻址**普通 SL 栏**（N 可在模组设置里配）。
- 每栏可单独存 / 读 / 删，显示 保存时间/太吾/年数/地点。
- ESC 里 4 个**原生克隆按钮**（存档/读档/快速存档/快速读档），存/读档打开自建档位面板，快速存/读档操作快捷栏。
- 确认走游戏**原生 Dialog**。

## 术语

- **栏位 (slot)**：一个独立存档文件。
  - 快捷栏：`local.sav.qsl.quick`，独立，只经"快速存档/快速读档"访问，**不进档位面板**。
  - 普通栏 N：`local.sav.qsl.slot.<N>`，N = 0..(SlotCount-1)。
- 存档目录：与现有一致，`Common.GetArchiveDataDirectory(archiveId)`（当前存档目录）。不再触碰游戏主档 `local.sav`。

## 架构总览

三部分，边界清晰：

1. **后端 · 栏位存档域方法**（`EasyQuickSaveLoad.Backend`）：把现有的时间戳环替换为栏位化的 mod 方法。
2. **前端 · ESC 原生按钮注入器**（`EasyQuickSaveLoad.Frontend`）：把 4 个原生克隆 `CButton` 注入 `ViewSystemOption`。
3. **前端 · 档位面板**（`EasyQuickSaveLoad.Frontend`）：自建、克隆原生元素拼出的存/读/删档位选择面板。

前端调后端一律用运行期 `ModIdStr`（已修复，见 [[project-easyquicksaveload]]）。

---

## ① 后端：栏位存档模型

替换现有 `SaveWorldWithBackup` / `ListManualSaves` / `LoadManualSave` 三个 mod 方法，改为栏位化：

- **`SaveToSlot(param{Slot:int})`**：Slot ≥ 0 → 存到 `slot.<Slot>`；Slot = -1（约定值）→ 快捷栏 `quick`。覆盖该栏文件（先写临时再原子替换，避免写坏）。复用现有 `SaveWorldAt`（`completeNextFrame:true`，不占用原生 .bak）。
- **`ListSlots(param{}) → SerializableModData`**：返回每个普通栏（0..SlotCount-1）+ 快捷栏的占用状态与 `WorldInfo` 摘要（时间/太吾姓名/年数/地点原始字段）。前端负责本地化格式化。
- **`LoadFromSlot(param{Slot:int})`**：读 `slot.<Slot>` 或 `quick`。沿用当前 `LoadManualSave` 的正确加载序列（Pack+LeaveWorld → SetLoadedAllArchiveData → LoadWorldAt → SetArchiveId → InitAchievements → next-frame SetCurrGameWorldType），保持已验证的顺序修复。
- **`DeleteSlot(param{Slot:int})`**：删除该栏文件。

槽位数量：后端从模组设置读取 `SlotCount`（默认 20，见 §配置）。

去除：`GetSavePointRetentionCount` / `RemoveRedundantManualSaves` / `CleanupManualSavesWhenSaveFinished` 等滚动环清理逻辑（栏位模型下每栏独立、覆盖即可，无需清理环）。

## ② 前端：ESC 原生按钮注入器

替换现有 `EasyQuickSaveLoadOverlay` 的自绘面板部分。

- 侦测 `UIElement.SystemOption` 显示且其 `UiBase`（`ViewSystemOption`）就绪。
- 反射取一个原生 `CButton` 作克隆源（`ViewSystemOption` 的私有 `[SerializeField]` 字段，如 `btnReturnToGame`）及其父容器（按钮所在的纵向布局）。
- `Instantiate` 该按钮 4 份，改子级标签文本为 存档/读档/快速存档/快速读档，`CButton.ClearAndAddListener` 挂回调，`SetSiblingIndex` 使其自成一组；组前插入一个**分隔元素**（优先克隆 prefab 内现有分隔/间隔元素，没有则放一个与原生风格匹配的细条/spacer）。
- 每帧按 `CanOperate` 设 `interactable`（不可存/读档时置灰）。
- `ViewSystemOption` 被销毁/重建时清理并重注入（跟随其生命周期）。
- 按钮回调：
  - 存档 → 打开档位面板（存模式）
  - 读档 → 打开档位面板（读模式）
  - 快速存档 → 快捷栏：若配置需确认则弹原生 Dialog（显示快捷栏现有摘要）→ `SaveToSlot(-1)`
  - 快速读档 → 快捷栏：若配置需确认则弹原生 Dialog（快捷栏摘要）→ `LoadFromSlot(-1)`

**具体克隆源字段名、父容器路径、分隔元素**在实现期用 EasyBridge 检视 live `ViewSystemOption` prefab 确定并写入常量。

## ③ 前端：档位面板

自建面板，尽量**克隆原生元素**（列表行/格子/文本/按钮）拼装，避免自绘风格漂移。

- 可滚动网格，渲染 N 个普通栏（**不含快捷栏**）。每格：
  - 占用：显示 保存时间 / 太吾姓名 / 年数 / 地点；一个小"删除"按钮。
  - 空：显示"空"。
- 两种模式（由 ESC 的存档/读档按钮决定）：
  - **存模式**：点空栏 → 直接 `SaveToSlot(slot)`；点占用栏 → 原生 Dialog（该栏现有摘要，"确认覆盖"）→ `SaveToSlot(slot)`。
  - **读模式**：点占用栏 → 原生 Dialog（该栏摘要）→ `LoadFromSlot(slot)`（走 `LoadManualWorld` 载入协程，隐藏 SystemOption）；空栏不可点。
- 每格"删除"→ 原生 Dialog 确认 → `DeleteSlot(slot)` → 刷新面板。
- 面板数据来源：`ListSlots`（异步 mod 方法），打开面板/存删后刷新。

## ④ 确认（原生 Dialog）

统一用游戏原生 `DialogCmd`（Title + Content=摘要 + Yes=执行 + 取消），与"回到主菜单/退出游戏"同机制。适用：覆盖占用栏、读档、删档、快速存/读档（受配置开关）。

## ⑤ 配置项（原生模组设置）

写入 `config.lua` 的 `DefaultSettings` + `Settings.Lua`（纯字面量，见 [[project-dreamlover]] config.lua-must-be-pure-literal 坑）：
- `SlotCount`：普通栏数量，默认 20。
- `QuickSaveConfirm`：快速存档是否弹确认，默认 true。
- `QuickLoadConfirm`：快速读档是否弹确认，默认 true。

## ⑥ 健壮性

- 反射拿不到 `ViewSystemOption` / 克隆源 `CButton` / 父容器 → 记 `Debug.LogWarning`，**不注入任何按钮**（不崩溃、不回退旧自绘面板）。
- 档位面板克隆源找不到 → 同样降级：记日志，按钮点击弹一个原生 Dialog 提示"UI 不可用"，不崩。
- 后端方法参数缺失/文件不存在 → 抛明确异常（现有风格），前端捕获并提示。

## 复用（不动）

- `CanOperate`（存/读档可用性 + 按钮置灰）。
- 档案信息读取、`WorldInfo` 摘要格式化（`FormatSaveTime/TaiwuName/Year/Location`）。
- `LoadManualWorld` 载入协程（含 `AddLoadedAllArchiveDataMonitor`、监视 `LoadedAllArchiveData`）。
- 运行期 `ModIdStr` 注入（`FrontendPlugin` → overlay）。
- 后端 `SaveWorldAt`（`completeNextFrame:true`）保存机制。

## 删除

- 自绘悬浮面板（`BuildUi` / `CreateButton` / `CreateText` / panel Image / status/archive 文本）。
- Attach/Detach/`ApplyEscPanelLayout` 到 SystemOption 的逻辑。
- 二次点击确认状态机（`PendingConfirmAction` / `SetConfirm` / `ResetConfirm` / confirm 计时/冷却/内嵌摘要文本）。
- 时间戳滚动环模型（`CreateManualSaveTimestamp` 语义、retention/回溯数复用、冗余清理）。
- 原生回溯列表复用（`OpenLoadPoint` 走 RevertArchive + `ApplyManualRevertArchiveLabel` 改标题 hack）。

## 测试 / 验收（EasyBridge 实机）

在 world_1（太吾鉴忠 6818，已过引导）验证：
1. ESC 里出现 4 个原生风格按钮，自成一组带分隔线；不可操作态置灰。
2. 存档面板：存到空栏 / 覆盖占用栏（确认）/ 删档，`ListSlots` 摘要正确。
3. 读档面板：读某栏 → 世界回到该栏快照（用好感哨兵验证回归，见 [[project-easyquicksaveload]] 手法），不卡 Loading、0 warn。
4. 快速存档→快捷栏；快速读档→回快捷栏快照；确认开关按配置生效。
5. 关游戏后校验测试栏文件清理、主档 `local.sav` 未被触碰（哈希对照备份）。

## 未决 / 实现期确定

- `ViewSystemOption` 克隆源字段名、按钮父容器路径、分隔元素来源。
- 档位面板克隆的原生行/格 prefab 来源（候选：RevertArchive 列表行、RecordSelect 卡片、或通用 CButton 拼格）。
- 面板超出一屏时：滚动（默认）还是翻页——默认滚动，按需再议。
