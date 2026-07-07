# EasyBridge 村职原生登记 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** EasyBridge 创建太吾村村民时走游戏内置村职登记路线，使村民过月安全并可指定山门职务。

**Architecture:** 入太吾村(org16)后设目标村职品阶，调用游戏 `OnTaiwuVillagerGradeChanged` 触发 `RegisterVillagerRole`，创建 `UpdateVillagerFixedActions` 所需的 `VillagerRoleWrapper`。修 `GameOps.MakeVillager`/`Spawn` 与 `Router`；预设经 `Spawn(villager:true)` 自动继承。

**Tech Stack:** C# / .NET 8（EasyBridge.Backend），太吾 GameData 域 API，Harmony。无单元测试框架——验证 = `dotnet build` + 实机（经 EasyBridge 管道）。

## Global Constraints

- 目标框架 net8.0；游戏引用 `<Private>false</Private>` 不拷贝。
- 后端 DLL 改动需**重启游戏**才生效。
- 端点保持通用、基于游戏机制（不做测试专用特化）。
- `villager=false` / `/nonvillager`（org 0 散人）路径**不改**。
- 太吾村 org 模板 id = 16（代码常量 `TaiwuVillageOrgTemplateId`）。
- 默认村职品阶须 < 8（规避掌门 principal 人数上限）。
- 构建：`dotnet build D:\TaiwuMods\EasyBridge\EasyBridge.Backend\EasyBridge.Backend.csproj -c Debug`；产物 `EasyBridge\Plugins\EasyBridge.Backend.dll`；部署复制到 `D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu\Mod\EasyBridge\Plugins\`。

---

### Task 1: 实机 spike——验证原生登记调用（不改 DLL）

先用运行中的游戏 /eval 验证"入 org16 + `OnTaiwuVillagerGradeChanged` → `GetVillagerRole` 非空"这条路线真的可行、`OnTaiwuVillagerGradeChanged` 可直接调用，避免带着错误假设重编译。

**Files:** 无（纯实机验证）。

- [ ] **Step 1: 启动游戏并回到地图**

创建 `steam_appid.txt`（游戏根 + Backend，内容 `838350`），`SteamAppId=838350` 直启 exe；经 UI 桥载存档回地图；`SB /ping` 得 `tickAlive=true`。先备份存档 `SaveGames/world_1` + `Save/world_1` + `global.sav`。

- [ ] **Step 2: spike——组织化调用原生登记**

`SB /config {enableEval:true}` 后运行（把 8xxx 换成新 spawn 的 id）：
```
$n = SB -Path "/spawn" -Body @{ villager=$false }   # 先要一个普通角色
# 解析默认村职 + 其品阶，入村，登记
SB -Path "/eval" -Body @{ code = @"
var ch = DomainManager.Character.GetElement_Objects(ID);
short role=-1; sbyte g=127;
foreach (Config.EventConfig.VillagerRoleItem it in (System.Collections.Generic.IEnumerable<Config.EventConfig.VillagerRoleItem>)Config.VillagerRole.Instance){ var gg=Config.OrganizationMember.Instance[it.OrganizationMember].Grade; if(gg>0 && gg<8 && gg<g){g=gg; role=it.TemplateId;} }
var settle = DomainManager.Taiwu.GetTaiwuVillageSettlementId();
DomainManager.Organization.ChangeOrganization(ctx, ch, new GameData.Domains.Organization.OrganizationInfo(16, g, true, settle));
DomainManager.Taiwu.OnTaiwuVillagerGradeChanged(ctx, ch, g);
return "role="+role+" grade="+g+" registered="+(DomainManager.Extra.GetVillagerRole(ID)!=null)+" tid="+DomainManager.Extra.GetVillagerRoleTemplateId(ID);
"@ }
```
Expected: `registered=True`，`tid` = 选中的 role。

- [ ] **Step 3: spike——过月安全**

`UI /static {type:"GMFunc",method:"AdvanceManyMonths",args:[1,0]}` → 等协程 → 读后端 `Logs/GameData_*.log`：`AdvanceMonth: begin→end` 干净、无 `UpdateVillagerFixedActions` / `NullReferenceException`。

- [ ] **Step 4: 判定**

若 Step2 `registered=True` 且 Step3 无异常 → approach A 成立，进 Task 2。
若 `OnTaiwuVillagerGradeChanged` 不可直接调用（编译/运行错）→ 改用 `DomainManager.Organization.ChangeGrade(ctx, ch, g, true)`（内部触发同一回调；需保证 g≠入村时品阶，故入村用品阶 0、g 取 >0 的默认村职）。记录采用的调用，Task 2 据此写 DLL。

- [ ] **Step 5: 还原存档**（spike 会自动存档），删 `steam_appid.txt`；退出游戏。

---

### Task 2: GameOps——`MakeVillager` 原生登记 + 默认村职解析

**Files:**
- Modify: `D:\TaiwuMods\EasyBridge\EasyBridge.Backend\GameOps.cs`（`MakeVillager` 约 522-533；新增私有 `DefaultVillagerRole`/`ResolveVillagerRoleGrade`）

**Interfaces:**
- Produces: `GameOps.MakeVillager(DataContext ctx, int id, short roleTemplateId = -1) -> Dictionary<string,object>`（结果含 `role`:int、可能 `roleFallback`:bool）
- Consumes（游戏 API，按 Task 1 定稿）：`Config.VillagerRole.Instance`(IEnumerable<VillagerRoleItem>)、`Config.VillagerRole.Instance.GetItem(short)`、`Config.OrganizationMember.Instance[sbyte].Grade`、`DomainManager.Taiwu.GetTaiwuVillageSettlementId()`、`DomainManager.Organization.ChangeOrganization(ctx,ch,OrganizationInfo)`、`DomainManager.Taiwu.OnTaiwuVillagerGradeChanged(ctx,ch,sbyte)`、`DomainManager.Extra.GetVillagerRole(int)`、`DomainManager.Extra.GetVillagerRoleTemplateId(int)`

- [ ] **Step 1: 加默认村职解析 helper**（放在 `MakeVillager` 上方）

```csharp
// 取最低的非掌门(品阶<8)、且品阶>0 的村职做默认；返回 (roleTemplateId, grade)。
private static (short role, sbyte grade) DefaultVillagerRole()
{
    short best = -1; sbyte bestGrade = 127;
    foreach (Config.EventConfig.VillagerRoleItem item in
             (System.Collections.Generic.IEnumerable<Config.EventConfig.VillagerRoleItem>)Config.VillagerRole.Instance)
    {
        sbyte g = Config.OrganizationMember.Instance[item.OrganizationMember].Grade;
        if (g > 0 && g < 8 && g < bestGrade) { bestGrade = g; best = item.TemplateId; }
    }
    return (best, bestGrade);
}
```
> 注：命名空间 `Config.EventConfig.VillagerRoleItem` / `Config.OrganizationMember` 以 Task 1 spike 实测为准；若 spike 用了别的限定名，改这里。

- [ ] **Step 2: 重写 `MakeVillager`**

```csharp
/// <summary>把角色以「太吾村村职」身份登记（走游戏内置 OnTaiwuVillagerGradeChanged），过月安全。
/// roleTemplateId<0 时用默认村职；无效则回退默认并标 roleFallback。</summary>
public static Dictionary<string, object> MakeVillager(DataContext ctx, int id, short roleTemplateId = -1)
{
    if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
        return Err("character not found: " + id);

    short role; sbyte grade; bool fallback = false;
    var cfg = roleTemplateId >= 0 ? Config.VillagerRole.Instance.GetItem(roleTemplateId) : null;
    if (cfg != null)
    {
        role = roleTemplateId;
        grade = Config.OrganizationMember.Instance[cfg.OrganizationMember].Grade;
    }
    else
    {
        if (roleTemplateId >= 0) fallback = true;
        (role, grade) = DefaultVillagerRole();
        if (role < 0) return Err("no valid villager role found in config");
    }

    short villageSettlement = DomainManager.Taiwu.GetTaiwuVillageSettlementId();
    DomainManager.Organization.ChangeOrganization(ctx, ch,
        new OrganizationInfo(TaiwuVillageOrgTemplateId, grade, true, villageSettlement));
    DomainManager.Taiwu.OnTaiwuVillagerGradeChanged(ctx, ch, grade);

    if (DomainManager.Extra.GetVillagerRole(id) == null)
        return Err("villager role registration failed (role=" + role + ", grade=" + grade + ")");

    var res = Snapshot(id);
    res["madeVillager"] = true;
    res["role"] = (int)DomainManager.Extra.GetVillagerRoleTemplateId(id);
    if (fallback) res["roleFallback"] = true;
    return res;
}
```
> 若 Task 1 定稿为 `ChangeGrade`：把 `ChangeOrganization(...grade...)` 改为入村品阶 0（`new OrganizationInfo(16,0,true,settle)`）+ `DomainManager.Organization.ChangeGrade(ctx, ch, grade, true)`（grade 必 >0，默认解析已保证）。

- [ ] **Step 3: 构建**

Run: `dotnet build D:\TaiwuMods\EasyBridge\EasyBridge.Backend\EasyBridge.Backend.csproj -c Debug`
Expected: Build succeeded, 0 Error。若报未知类型名（VillagerRoleItem/OrganizationMember 限定名），据编译错误修正限定名后重编。

- [ ] **Step 4: 提交**

```bash
git add EasyBridge/EasyBridge.Backend/GameOps.cs
git commit -m "EasyBridge: register villager role via native OnTaiwuVillagerGradeChanged in MakeVillager"
```

---

### Task 3: Spawn(villager=true) 走新路线 + Router `role` 参数

**Files:**
- Modify: `GameOps.cs`（`Spawn` 约 288-322，含 307-310 注释）
- Modify: `Router.cs`（`/spawn` 113-122、`/villager` 149-153）

**Interfaces:**
- Produces: `GameOps.Spawn(DataContext ctx, sbyte gender, short age, short settlementId, sbyte grade, short baseAttraction, bool villager, short roleTemplateId = -1)`
- Consumes: `GameOps.MakeVillager(ctx, id, roleTemplateId)`（Task 2）

- [ ] **Step 1: `Spawn` 增参 + villager 分支走 MakeVillager**

`Spawn` 签名末尾加 `, short roleTemplateId = -1`。把 `if (!villager) { ...org0... }` 之后（即 else 分支）改为显式：`villager=true` 时调 `MakeVillager(ctx, id, roleTemplateId)` 并保持落位太吾格。替换 307-316 段为：
```csharp
            // 落位到太吾所在格，确保出现在同一张地图角色列表里供 UI 交互。
            ch.SetLocation(loc, ctx);

            if (villager)
            {
                // 走游戏内置村职登记：入太吾村 + 原生改品阶回调，过月安全（见 MakeVillager）。
                var mv = MakeVillager(ctx, id, roleTemplateId);
                if (!IsOk(mv)) return mv;
                ch.SetLocation(loc, ctx);
            }
            else
            {
                var outsider = new OrganizationInfo(0, 0, true, DomainManager.Organization.GetSettlementIdByOrgTemplateId(0));
                DomainManager.Organization.ChangeOrganization(ctx, outsider is null ? ch : ch, outsider); // keep as-is
                DomainManager.Organization.ChangeOrganization(ctx, ch, outsider);
                ch.SetLocation(loc, ctx);
            }
```
> 修正：上面 else 保持原逻辑，实际只写这一行 `DomainManager.Organization.ChangeOrganization(ctx, ch, outsider); ch.SetLocation(loc, ctx);`（删掉示例里那行占位）。最终 else：
```csharp
            else
            {
                var outsider = new OrganizationInfo(0, 0, true, DomainManager.Organization.GetSettlementIdByOrgTemplateId(0));
                DomainManager.Organization.ChangeOrganization(ctx, ch, outsider);
                ch.SetLocation(loc, ctx);
            }
```
若无 `IsOk` 帮助器，用内联：`if (mv != null && mv.TryGetValue("ok", out var okv) && okv is bool okb && !okb) return mv;`

- [ ] **Step 2: 更新崩溃警告注释**

把 307-310 原"太吾村村民缺少村民角色固定行为数据，过月时会崩…"注释改为：
```csharp
            // villager=true 走 MakeVillager 的游戏内置村职登记（OnTaiwuVillagerGradeChanged→RegisterVillagerRole），
            // 已过月安全；可传 roleTemplateId 指定山门职务，缺省用默认低品村职。
```

- [ ] **Step 3: Router `/spawn` 与 `/villager` 解析 `role`**

`/spawn`（113-122）加 `short roleTemplateId = (short)Json.GetInt(body, "role", -1);` 并传入：
```csharp
                return Pump(ctx => GameOps.Spawn(ctx, gender, age, settlementId, grade, baseAttraction, villager, roleTemplateId));
```
`/villager`（149-153）：
```csharp
            if (path == "/villager")
            {
                int id = Json.GetInt(body, "id", -1);
                short roleTemplateId = (short)Json.GetInt(body, "role", -1);
                return Pump(ctx => GameOps.MakeVillager(ctx, id, roleTemplateId));
            }
```

- [ ] **Step 4: 构建**

Run: `dotnet build D:\TaiwuMods\EasyBridge\EasyBridge.Backend\EasyBridge.Backend.csproj -c Debug`
Expected: Build succeeded, 0 Error。

- [ ] **Step 5: 提交**

```bash
git add EasyBridge/EasyBridge.Backend/GameOps.cs EasyBridge/EasyBridge.Backend/Router.cs
git commit -m "EasyBridge: route Spawn(villager) through native role path; /spawn,/villager accept role"
```

---

### Task 4: 部署 + 实机验证 + 文档 + 记忆

**Files:**
- Modify: `EasyBridge/skills/taiwu-statebridge.md`（`/spawn`、`/villager` 段与"villager 过月会崩"警告）
- Modify: 记忆 `project_testbridge.md`、`project_mod-publish-version-workflow.md`（改写 spawn-villager-crashes-month 表述）

- [ ] **Step 1: 部署新 DLL**

复制 `EasyBridge\Plugins\EasyBridge.Backend.dll` → `…\Mod\EasyBridge\Plugins\EasyBridge.Backend.dll`。

- [ ] **Step 2: 启动游戏 + 备份存档 + 回地图**（同 Task 1 Step1；确认 `[EasyBridge]` 后端已加载）

- [ ] **Step 3: 验证默认村职**

`SB /spawn {villager:true}` → 返回 `role`>=0；`SB /eval` 确认 `GetVillagerRole(id)!=null`、org=16。

- [ ] **Step 4: 验证指定村职**

`SB /spawn {villager:true, role:<Task1 得到的某有效 templateId>}` → 返回 `role` 等于该值、`GetVillagerRole` 对应；传一个无效 role（如 30000）确认 `roleFallback=true` 且仍登记。

- [ ] **Step 5: 验证过月安全**

保持上述村民在场，`UI /static GMFunc.AdvanceManyMonths(1,0)` → 后端日志 `AdvanceMonth begin→end` 干净、无 `UpdateVillagerFixedActions`/NRE。

- [ ] **Step 6: 回归**

`SB /spawn {villager:false}` 仍 org 0 散人；`SB /villager {id:...}` 登记村职成功。

- [ ] **Step 7: 更新 skill 文档**

`taiwu-statebridge.md`：`/spawn` 与 `/villager` 增加 `role`（roleTemplateId，缺省默认村职）说明；把"`villager=true`…过月会让 `UpdateVillagerFixedActions` 空引用崩溃…务必用 `villager=false`"改写为"`villager=true` 现走游戏内置村职登记、**过月安全**；可传 `role` 指定山门职务"。

- [ ] **Step 8: 还原存档 + 关游戏 + 删 steam_appid.txt**

- [ ] **Step 9: 更新记忆 + 提交**

改 `project_testbridge.md` / `project_mod-publish-version-workflow.md` 中 spawn-villager 崩溃表述为"已修复：走原生村职登记"。
```bash
git add EasyBridge/skills/taiwu-statebridge.md
git commit -m "EasyBridge: docs — villager spawn now registers a native role, month-safe"
```

## Self-Review

- **Spec coverage**：目标1(过月安全)=Task2+4；目标2(指定村职)=Task2/3+4；目标3(内置路线)=Task1/2 用 OnTaiwuVillagerGradeChanged；目标4(通用)=保持端点通用、只加 role 可选字段。非目标(不改 villager=false/nonvillager/原生 bug)均遵守。
- **Placeholder**：Task3 Step1 含一处示范占位，已在同步骤给出"最终 else"更正版；实现时以更正版为准。
- **Type consistency**：`MakeVillager(ctx,id,roleTemplateId=-1)`、`Spawn(...,roleTemplateId=-1)`、Router `role`→roleTemplateId 一致；`DefaultVillagerRole()->(short,sbyte)` 与调用点一致。
