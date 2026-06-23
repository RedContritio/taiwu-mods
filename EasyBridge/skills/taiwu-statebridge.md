---
name: taiwu-statebridge
description: Use to construct in-game character/relationship state (generate condition-meeting NPCs), read it back for assertions, and run arbitrary C# via /eval — over the EasyBridge mod's backend pipe (taiwu-testbridge). Pair with the taiwu-ui skill (same EasyBridge mod's frontend pipe taiwu-uibridge) to drive and verify ForceEncounter event paths end-to-end.
---

# EasyBridge State Skill

后端状态桥 `taiwu-testbridge`：生成/改造 NPC、读取角色状态。与前端 `taiwu-uibridge`（UI 驱动）配合，
端到端验证 `ForceEncounter`（情难自已）各分支。

## 关键规则

1. **每个 PowerShell 调用都要带函数定义**（shell 状态不跨调用）。
2. **必须先加载存档并回到地图**，后端 `GlobalDomain` 才会每帧 tick，pump 才会排空。`/ping` 的
   `tickAlive=true` 表示钩子已运行；若一直 false，说明还没进存档或主循环未跑。
3. **生成的 NPC 落在太吾所在格**，因此会出现在 `MapBlockCharList` 里，可被 UiBridge 点击。用返回的
   `name` 在 UI 角色列表里匹配；若同名多个，用 `/spawn` 逐个生成并立刻交互，避免歧义。
4. 写操作有 ~8s 超时；若超时多半是没进存档（tick 不跑）。

## 连接函数（每次调用都要包含）

```powershell
function Invoke-StateEasyBridge {
    param([string]$Path, [hashtable]$Body)
    $req = @{ path = $Path }
    if ($Body) { $req.body = ($Body | ConvertTo-Json -Compress) }
    $json = $req | ConvertTo-Json -Compress
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "taiwu-testbridge", [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(5000)
        $w = New-Object System.IO.StreamWriter($pipe, [System.Text.Encoding]::UTF8, 4096, $true)
        $w.WriteLine($json); $w.Flush(); $w.Dispose()
        $r = New-Object System.IO.StreamReader($pipe, [System.Text.Encoding]::UTF8)
        $r.ReadLine() | ConvertFrom-Json
    } finally { $pipe.Dispose() }
}
```

## 端点

- `Invoke-StateEasyBridge -Path "/ping"` → `{ok, tickAlive}`
- `Invoke-StateEasyBridge -Path "/taiwu"` → `{taiwuId, closeFriendId, areaId, blockId, behaviorType, ...}`
- `Invoke-StateEasyBridge -Path "/whereami"` → 太吾当前格 `{areaId, blockId, blockType, blockTypeName}`（护卫判定看 blockType）
- `Invoke-StateEasyBridge -Path "/char/123"` → 角色快照（含 `hasGuard`）
- `Invoke-StateEasyBridge -Path "/preset" -Body @{ name="spouse" }` → 一键造 NPC，返回 `{id, name, expectedRoute, snapshot}`
- `Invoke-StateEasyBridge -Path "/spawn" -Body @{ gender=0; age=25; grade=4; baseAttraction=500; villager=$false }`
  → 默认生成**非村民散人**（落在太吾格，但会随月度游走/被门派招募）。
  **`villager=$true` 才转入太吾村**（村民文案分支用），但太吾村村民缺村民角色数据，**过月会让游戏
  `TaiwuDomain.UpdateVillagerFixedActions` 空引用崩溃、卡死过月**——验证过月类 mod（如 DreamLover）
  务必用 `villager=$false`，并配合 DreamLover 设 `IgnoreDistance` 以免散人游走出格被 sameLocation 过滤。
- `Invoke-StateEasyBridge -Path "/sects" -Body @{ count=8 }` → 门派据点格位列表 `{orgTemplate, areaId, blockId, blockTypeName}`
- `Invoke-StateEasyBridge -Path "/settlements" -Body @{ civilianOnly=$true; max=80 }` → 城镇/城市据点列表（含 blockType），找弱平民格触发护卫拦截
- `Invoke-StateEasyBridge -Path "/move/taiwu" -Body @{ areaId=..; blockId=.. }` → 瞬移太吾（跨区自动 QuickTravel）
- `Invoke-StateEasyBridge -Path "/relation" -Body @{ a=$taiwu; b=$npc; type="spouse"; both=$true }`
- `Invoke-StateEasyBridge -Path "/favor" -Body @{ from=$taiwu; to=$npc; type=6 }`
- `Invoke-StateEasyBridge -Path "/villager" -Body @{ id=$npc }` / `-Path "/nonvillager"`
- `Invoke-StateEasyBridge -Path "/prisoner" -Body @{ id=$npc }`
- `Invoke-StateEasyBridge -Path "/injure" -Body @{ id=$npc; level=6 }` → 叠伤势，84 标记 → 无力应战；对太吾 6818 削弱可制造战败
- `Invoke-StateEasyBridge -Path "/heal" -Body @{ id=6818 }` → 清空伤势（复原太吾）
- `Invoke-StateEasyBridge -Path "/favor/exact" -Body @{ from=$npc; to=$taiwu; value=16000 }` → 精确好感（绕过缩放，BranchA 用）
- `Invoke-StateEasyBridge -Path "/block/chars"` → 当前格全部角色 `{id,name,orgTemplateId,hasGuard,kidnapperId}`
- `Invoke-StateEasyBridge -Path "/cripple" -Body @{ id=$npc }` → 撤销全部战技 + 内力清零，返回 combatPowerBefore/After（注：根基属性主导，降幅有限）
- `Invoke-StateEasyBridge -Path "/giverope" -Body @{ template=90 }` → 给太吾高级捕绳（开战前调用）
- `Invoke-StateEasyBridge -Path "/throwrope"` → 战斗中向敌扔绳（重试至命中 → 擒获）
- `Invoke-StateEasyBridge -Path "/eval" -Body @{ code="return DomainManager.Taiwu.GetTaiwuCharId();" }` → **动态执行任意 C#**（见下「/eval」节）

> 自 EasyBridge 合并版起，前端 UI 桥 + 后端状态桥同属**一个 mod「EasyBridge」**（管道名不变：`taiwu-uibridge` / `taiwu-testbridge`）。

## /eval：动态执行任意 C#（无需为每个新操作重编译重启）

后端内置 Roslyn。`/eval` 把传入的 C# 代码在**后端主线程**上、用主线程写上下文 `ctx` 运行，可直接调用
`DomainManager.*`、`EventHelper.*`、以及本桥的 `GameOps.*`（这些命名空间已 `using`）。脚本用 `return ...;`
返回值，结果序列化进 `result`。预置全局：`ctx`（DataContext）、`taiwuId`（int）。编译结果按代码串缓存；
编译/运行错误以 `{ok:false, error}` 返回。

```powershell
. D:\TaiwuMods\_scratch\bridge.ps1
SB -Path "/eval" -Body @{ code = "return DomainManager.Taiwu.GetTaiwuCharId();" }
SB -Path "/eval" -Body @{ code = "DomainManager.Character.AddRelation(ctx, taiwuId, 7570, 1024); return DomainManager.Character.GetAliveSpouse(7570);" }
SB -Path "/eval" -Body @{ code = "return GameOps.Spawn(ctx, (sbyte)0, (short)20, (short)-1, (sbyte)2, (short)300);" }
```
用 `/eval` 做一次性/临时状态操作（不必再为每个新操作加端点重启）；常用、文档化的流程仍走上面的命名端点。

> 下文配方用 `SB`/`UI`/`FE-Open`/`Drive-Combat` 等简写助手——它们在 `_scratch/bridge.ps1`，每次调用前先
> `. D:\TaiwuMods\_scratch\bridge.ps1` 点进来（`SB`=本桥、`UI`=UiBridge）。`SB -Path P -Obj @{...}` 等价于
> 上面的 `Invoke-StateEasyBridge -Path P -Body (...|ConvertTo-Json)`。

## 移动到其他格

地图角色列表只显示太吾**当前格**的 NPC。要操作别的格，先瞬移：`/sects`/`/settlements` 找目标格 → `/move/taiwu`。
瞬移用 `EventHelper.TeleportMoveTaiwuToLocation`，**不消耗行动力**，且瞬移后 UI 角色列表会自动刷新到新格。
`hasGuard=true` 的前提：org≠16（非太吾村）、有 settlement 且 areaId<45、且**身处非 Wild/Bad 格**；自己生成的散人/村民通常无护卫。

## 触发护卫拦截（护卫出面）——关键：用城镇/城市平民，别用门派 NPC

`GetTaiwuCombatEnemyTeam`(GameData:490264) 把 [护卫…+目标] 整列按 `Character.GetCombatPower()` 降序排，
`ForceEncounterCombatStarter` 要求 `enemyTeam[0]!=target` 才进护卫出面。门派散兵/弟子是**武者**、战力**高于**自己按
品级生成的护卫（日志恒 `firstEnemy=target`）→ 直接开战、不触发；而**城镇/城市平民**(org≠16、非武者、战力低)的护卫
**反而更强** → `firstEnemy=守卫` → 护卫出面。`/cripple`（撤 15 战技+清内力）对门派武者只降 ~11% 战力（根基属性主导、
无 GM setter），不够——所以走“找弱平民”，不要试图削武者。配方：

```powershell
. D:\TaiwuMods\_scratch\bridge.ps1
$dest = (SB -Path "/settlements" -Obj @{civilianOnly=$true;max=80}).settlements | ? { $_.blockTypeName -in @("City","Town") } | Select -First 1
SB -Path "/move/taiwu" -Obj @{ areaId=$dest.areaId; blockId=$dest.blockId } | Out-Null
$g = (SB -Path "/block/chars").chars | ? hasGuard | Select -First 1     # 城镇平民, hasGuard=True
$o = FE-Open -Name $g.name                                              # 敌对 → 情难自已
FE-ForceAndConfirm                                                      # 更进一步 + 重要选择确认
if (FE-OptionId -Key "ForceEncounter.GuardInterceptContinue") { FE-GuardContinue; Drive-Combat }  # 护卫出面 → 护卫继续 → 与护卫战
```
已实测：城市 org21 平民「俞盼文」→ 日志 `firstEnemy=守卫≠目标` → 护卫出面 → 战斗反馈“护卫拦截” reason=GuardIntercepted、
非村民 relType=32768 结仇。排查看后端日志 `[ForceEncounter] Guard preparation ... firstEnemy=` / `Guard intercept active`。

## 战斗驱动要点

- 战斗为手动但游戏内置 **AutoFight 默认开**，弱目标 ~20-95s 自动打完出 `CombatResult`。用 `Drive-Combat`（见 `_scratch/bridge.ps1`）。
- **无力应战（直接成功）**：`/injure {id,level:6}` → 84≥36 标记 → 更进一步直接成功，不开战。
- **强制失败（太吾战败）**：`/injure {id:6818,level:4}` 削弱太吾 → 对无护卫目标更进一步开战 → AutoFight 战败 → 失败结算；
  测后 `/heal {id:6818}` 复原。
- **战斗擒获 → 擒获处置**（hack 战斗过程扔绳子；RNG 无妨，多扔几次）：
  1. `/giverope {template:90}` 给太吾高级捕绳（**必须在开战前**，战斗会快照背包 GetValidItems）；
  2. spawn 无护卫目标 → `/injure {id,level:2}`（~28 标记<36，开战且绳命中率高）→ 更进一步开战 → start；
  3. 开战中循环 `/throwrope`（`CombatDomain.UseItem` 向敌扔绳，需 CanAcceptCommand，AutoFight 间隙才生效，故多次重试）；
  4. 命中后战斗写 `CharIdSeizedInCombat` → 战斗反馈 reason=TargetCapturedInCombat → 战斗反馈继续 → **擒获处置**（公开/秘密关押·放其离开）。
  后端日志可见 `preliminaryReason=TargetCapturedInCombat`。验证过：模板 90 绳 + 预标记 28 的目标，2 次投掷即命中。
- **交互窗口收尾**：UiBridge 不能发 ESC。务必用各路“继续/念头通达”选项关闭事件窗回地图；`其他话题`只回到敌对子菜单、
  会留下**模态 EventWindow** 挡住后续 NPC 点击（导致 FE-Open 误触旧目标）。换目标前确保 `ActiveWindows` 为空。

预设名（`/preset {name}`，详见 `../README.md`）：
- 亲密直通：`spouse`, `mutual-lover`, `adore`, `deepvalley-unattached`, `villager`, `deepvalley-attached-high`
- 强制：`plain`(非村民结仇), `plain-villager`(村民不结仇), `minor`, `prisoner`, `directfallen`(无力应战), `deepvalley-attached-low`

护卫拦截、战斗擒获不是预设——按上文“触发护卫拦截”/下文“战斗擒获”的配方手动构造。

## 端到端验证一条分支（与 UiBridge 配合）

```
1. 确认游戏在地图：Invoke-StateEasyBridge -Path "/ping"  → tickAlive=true
2. 造 NPC：       $p = Invoke-StateEasyBridge -Path "/preset" -Body @{ name="spouse" }
                  $id = $p.id; $name = $p.snapshot.name; $expect = $p.expectedRoute
3. 记录初值：     $before = Invoke-StateEasyBridge -Path "/char/$id"
4. UiBridge：重读 MapBlockCharList → 按 $name 找到该 NPC → 点击 → EventWindow
5. UiBridge：切“敌对”标签 → 点“（情难自已……）”
6. 内层：读出所有选项文本与说明，按 $expect 选择（语义判断，不要机械选位置）：
   - accepted 路线 → 选“（半推半就……）”→ 进入“亲密反馈”，读文案确认成功/未成年/村民
   - forced 路线   → 选“（更进一步……）”→ 走战斗/护卫/直接结算；或选“其他话题”验证放弃无副作用
7. 断言：$after = Invoke-StateEasyBridge -Path "/char/$id"
   - 比较 favorability/favorabilityType、关系位（hasSpouse/hasAdored）、kidnapperId、组织等
   - 配合 UI 反馈文案，核对与 README 预期一致
```

## 校验要点（对照 ForceEncounter 设计）

- **亲密直通**（spouse/lover）：不结仇、好感 delta 0；UI 进入“亲密反馈”而非战斗。
- **单恋太吾 / 谷中密友有他属高好感**：折扣好感惩罚（村民比例，默认约 −9000）。
- **太吾村民**：强制路线不结仇、好感按村民比例打折；亲密净需求档位为 4。
- **未成年**：选项内层说明出现未成年警告，但机制与成年一致。
- **已是太吾俘虏**：强制不再开战，直接按已制服成功结算。
- **放弃（其他话题）**：不消耗行动力、不开战、不结仇、不写记录。

> 事件选项的选择交给当前 LLM 阅读上下文语义判断，不要硬编码“总选第一个/最后一个”。
