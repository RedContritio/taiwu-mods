# EasyBridge 走游戏内置路线创建村职角色 — 设计文档

日期：2026-07-08
状态：已通过设计评审，待写实现计划

## 背景与问题

EasyBridge 的后端状态桥用 `GameOps` 生成/改造 NPC 供测试。创建"太吾村村民"目前走的是捷径：

- `GameOps.Spawn(...)`（`EasyBridge.Backend/GameOps.cs:288`）：`EventHelper.CreateIntelligentCharacter` 建角色；`villager=false` 时 `ChangeOrganization` 转 org 0 散人；`villager=true` 时留在太吾村所在 settlement/org。
- `GameOps.MakeVillager(ctx, id)`（`GameOps.cs:523`）：仅 `DomainManager.Organization.ChangeOrganization(ctx, ch, org16)`。

这条捷径把角色变成太吾村（org 模板 16）成员，但**从不登记村职（VillagerRole）**。而游戏过月时 `TaiwuDomain.UpdateVillagerFixedActions` 会遍历太吾村中**处于村职品阶**的成员并调用 `DomainManager.Extra.GetVillagerRole(charId)`，对返回值的 `.Character` **无空检查**——成员在村职品阶却没登记村职时即空引用崩溃、卡死整个过月流程。这就是 skill 文档里记的 "spawn-villager-crashes-month" 坑。

> 注：同一处 `UpdateVillagerFixedActions` 也是游戏原生 bug（村职持有者死亡留悬空引用会崩）的现场，但那属于游戏自身问题，不在本次范围。本次只修 EasyBridge 自己制造无效村民的问题。

## 目标

1. EasyBridge 创建的太吾村村民**始终登记合法村职**，过月安全（不再触发 `UpdateVillagerFixedActions` 空引用）。
2. 支持创建**指定山门职务**（特定村职）的村民。
3. 用**游戏内置路线**登记村职，而不是给游戏打补丁绕过。
4. 保持 EasyBridge 端点通用、基于游戏机制（见 `[[feedback_easybridge-stays-general]]`）。

## 非目标

- 不修游戏原生 `UpdateVillagerFixedActions` 的空引用（那是游戏 bug，另议/反馈官方）。
- 不改 `villager=false` / `/nonvillager`（散人）路径，保持原样。
- 不做村职容量/威望经济的完整模拟。

## 方案（已选：原生改品阶）

用游戏自己的"晋升村职"内部路线登记角色：

1. `DomainManager.Organization.ChangeOrganization(ctx, ch, org16)` —— 先入太吾村。
2. 原生改品阶到**目标村职的品阶**：`DomainManager.Organization.ChangeGrade(ctx, ch, destGrade, destPrincipal)`（或等效的 `DomainManager.Taiwu.GmCmd_ForceChangeGrade(ctx, charId, grade, principal)`）。`ChangeGrade` 内部对 org-16 成员会调用 `DomainManager.Taiwu.OnTaiwuVillagerGradeChanged(ctx, ch, targetGrade)`，后者据 `GetGradeVillagerRole(targetGrade)` 调 `DomainManager.Extra.RegisterVillagerRole(ctx, id, roleTemplateId)`，创建 `VillagerRoleWrapper` 并初始化——正是 `UpdateVillagerFixedActions` 所需。
3. 返回前断言 `DomainManager.Extra.GetVillagerRole(id) != null`；把登记到的 `roleTemplateId` 放入返回结果。

选此路线的理由：它就是游戏晋升村民时走的同一个 `OnTaiwuVillagerGradeChanged`；相比完整 `SetVillagerRole`，不产生威望消耗/好感变动等会扰乱测试的副作用，也不会因太吾威望不足而失败。

### 村职选择

- **不传 `roleTemplateId`（默认）**：取一个有效的、低/中品阶且非掌门（避开品阶 8 的 principal 人数上限）的村职作默认。运行时从 `Config.VillagerRole.Instance` 解析（如按 `OrganizationMember.Instance[item.OrganizationMember].Grade` 升序取最低的非 principal 村职）。默认**总登记一个村职**，故村民恒过月安全。
- **传 `roleTemplateId`**：品阶 = `OrganizationMember.Instance[Config.VillagerRole.Instance[roleTemplateId].OrganizationMember].Grade`，改到该品阶 → 登记该特定村职。无效 `roleTemplateId` 回退默认并在结果里标注。

`destPrincipal` 与游戏 `SetVillagerRole` 一致取 true；默认村职品阶须 < 8 以规避掌门人数上限。

## 接口改动（最小、向后兼容）

- `GameOps.MakeVillager(ctx, id, short roleTemplateId = -1)`：入村 + 原生改品阶 + 校验登记；返回含 `role`（登记到的 templateId）与 `roleName`（若易得）。
- `GameOps.Spawn(..., bool villager, short roleTemplateId = -1)`：`villager=true` 改为走新的 `MakeVillager` 路线（带默认/指定村职）。`villager=false` 不变。
- `Router`：`/spawn` 与 `/villager` 解析可选字段 `role`（= roleTemplateId）。缺省 -1。
- `Presets`：`villager` / `plain-villager`（`Presets.cs:83/105`，及 `SpawnVillager` 帮助器 `:175`）经由新路线产出有效、过月安全的村职村民；预设语义（村民文案分支/好感比例）不变（仍是 org 16 成员）。

## 涉及文件

- `EasyBridge/EasyBridge.Backend/GameOps.cs` —— `Spawn`、`MakeVillager`（`MakeNonVillager` 不动）。
- `EasyBridge/EasyBridge.Backend/Router.cs` —— `/spawn`、`/villager` 增 `role` 字段解析。
- `EasyBridge/EasyBridge.Backend/Presets.cs` —— 村民相关预设走新路线。
- `EasyBridge/skills/taiwu-statebridge.md` —— 更新 `/spawn`、`/villager` 文档；删除/改写"villager=true 过月会崩"的警告为"已过月安全，可指定 role"。
- `GameOps.Spawn` 内的中文注释（`GameOps.cs:307-310`）更新为不再崩溃。

## 错误处理

- 角色不存在 / 太吾不存在：沿用 `GameOps.Err(...)`。
- `roleTemplateId` 无效：回退默认村职，结果标注 `roleFallback=true`。
- 登记后 `GetVillagerRole(id) == null`：返回 `Err`（说明改品阶未触发登记），便于快速定位。

## 验证计划

需游戏运行 + EasyBridge。用直接 exe + `steam_appid.txt` 启动，载存档回地图（`SB /ping` tickAlive=true）。

1. **默认村职**：`SB /spawn {villager:true}` → `SB /eval` 读 `GetVillagerRole(id)` 非空、`GetVillagerRoleTemplateId(id)` 为默认村职、org=16。
2. **指定村职**：`SB /spawn {villager:true, role:<某 templateId>}` → 校验登记的正是该村职、品阶匹配。
3. **过月安全**：保持这两个村民在场，`UI /static GMFunc.AdvanceManyMonths(1,0)` 推进一月；读后端 `Logs/GameData_*.log`，确认无 `UpdateVillagerFixedActions` 空引用、`AdvanceMonth: begin→end` 干净。
4. **回归**：`villager=false` 仍为 org 0 散人；`/nonvillager` 不变。
5. 测后从备份还原存档，删除临时 `steam_appid.txt`。

## 部署与善后

- `dotnet build` EasyBridge.Backend → 拷 DLL 到游戏 `Mod/EasyBridge/Plugins/`（后端 DLL 改动需重启游戏生效）。
- 修复后更新记忆：`[[project_testbridge]]` 与 `[[project_mod-publish-version-workflow]]` 中 "spawn-villager-crashes-month" 的表述改为"EasyBridge 生成村民已走原生村职登记、过月安全"。
