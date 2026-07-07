---
name: taiwu-ui
description: Inspect and interact with the running Taiwu game UI via the EasyBridge mod's frontend named pipe (easybridge-ui). Use when you need to verify game UI state, find UI elements, or perform actions (click, toggle, set, select) during automated testing.
---

# EasyBridge UI Skill

与运行中的太吾绘卷游戏 UI 交互，用于自动化测试和验证。

## 关键规则

1. **每个 PowerShell 调用都必须以函数定义开头** — shell 状态不跨调用保留
2. **动作后必须重新取快照** — id 在 UI 变化后失效
3. **先 `/ui` 看全貌，再 `/ui/{name}` 摘要下钻** — 不要猜窗口名；默认只读标题/分区计数，需要控件时显式 `detail=full&fields=controls`
4. **用 `-match` 搜索标签** — 标签已去除富文本标签，可直接用中文匹配
5. **不能发 ESC/键盘** — 只有 click/toggle/set/select。事件/交互窗口要靠**窗口内的“继续/确认/关闭”按钮**关闭；
   像 NPC 交互这种**模态窗**没关掉会挡住后续的地图角色点击（点新 NPC 无效，误触旧窗）。换目标前先 `/ui` 确认它已关。
6. **每次调用都带 `-Note "<一句中文说明>"`** — 说明这步在做什么。启用 monitor 后它会显示在游戏内「EasyBridge」浮层（F9 展开/收起、F8 暂停），
   方便用户实时看到 agent 的每个动作。需要浮层时先 `POST /config {"enableMonitor":true}`；回看完整历史用 `GET /log?since=&max=`。

## 游戏品级约定（重要：读 grade 数据前必看）

太吾**显示品级是「1 品最高、9 品最低」**（一品最强、九品垫底）。但代码/数据里的内部 `grade`/`Level` 多为 **0~8（0 最低、8 最高）**，与显示**正好相反**：

- **显示品 = `9 − 内部grade`**（内部 0→9品，内部 8→1品，内部 3→6品）。
- 颜色 `Colors.Instance.GradeColors[0..8]`：index 0=最低(灰)、8=最高(红)，与游戏里名字的品级色一致。
- 蛐蛐：`(colorId,partId).CalcCricketGrade()` 返回**内部 0~8**；`ItemDisplayData.CricketColorId/PartId` 是真实 color/part，**不被「是否鉴定」遮挡**（鉴定只改名字显示，不改圈色/品级）。斗蛐蛐对手蛐蛐按对手 org 品级**现生成**（`CricketGenerator`：三只 = `[wagerGrade, orgGrade, orgGrade-3+绝学/150]`），故弱对手三只可能全是 0 品呆物、看起来一色。

读到一个 grade 数字，**先确认它是「内部 0~8」还是「显示 1~9」**，别把内部 index 当显示品级念反。

## 函数定义（每次调用必须包含）

```powershell
function Invoke-UiBridge {
    param([string]$Path, [hashtable]$Query, [string]$Body, [string]$Note)
    $req = @{ path = $Path }
    if ($Query) { $req.query = $Query }
    if ($Body) { $req.body = $Body }
    if ($Note) { $req.note = $Note }   # 一句中文说明，显示在游戏内「EasyBridge」浮层上
    $json = $req | ConvertTo-Json -Compress
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "easybridge-ui", [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(5000)
        $writer = New-Object System.IO.StreamWriter($pipe, [System.Text.Encoding]::UTF8, 4096, $true)
        $writer.WriteLine($json); $writer.Flush(); $writer.Dispose()
        $reader = New-Object System.IO.StreamReader($pipe, [System.Text.Encoding]::UTF8)
        $response = $reader.ReadLine()
        $response | ConvertFrom-Json
    } finally { $pipe.Dispose() }
}
```

## API 速查

| 端点 | 方法 | 用途 |
|------|------|------|
| `/ping` | GET | 健康检查 |
| `/ui` | GET | 全局快照：当前活动窗口列表 |
| `/ui/{name}` | GET | 单窗口渐进读取；默认摘要，`detail=title` 只读标题，`detail=full` 才返回控件/文本 |
| `/find?q=关键词` | GET | 跨窗口文本搜索 |
| `/elements` | GET | showing/exist 窗口目录；默认只回当前相关窗口，全目录用 `all=true` |
| `/inspect?element=Name&id=Path` | GET | 通用 UI 对象检查：组件、RectTransform、屏幕坐标、UI camera |
| `/pointer?x=900&y=995&origin=top-left` | GET | 只读指针诊断：当前鼠标坐标和 UI raycast 命中栈；可验证 Computer Use 坐标 |
| `/reflect?element=Name&component=Type&member=path` | GET | 通用只读反射：读取组件字段，支持私有字段和点号路径 |
| `/action` | POST | 动作注入（click/toggle/set/select） |
| `/wait/actions?timeout=30` | POST | 等待指定窗口出现后，在同一主线程调度中执行一组动作 |
| `/config` | POST | 临时开关：`enableInvoke`、`enableMonitor`、`mainThreadTimeoutMs` |
| `/reflect/invoke` | POST | 反射调用实例方法（**默认关闭**；`POST /config {"enableInvoke":true}` 开启）|
| `/wait?element=Name&timeout=60` | GET | 阻塞等待指定窗口出现 |
| `/quit` | POST | 退出游戏（不保存，需先开启 `enableInvoke`） |

## EasyBridge浮层 + 每次操作带说明（note）

前端 monitor 默认关闭。需要实时说明时先 `POST /config {"enableMonitor":true}`，插件会创建一个**原生 IMGUI 浮层「EasyBridge」**（`MonitorOverlay.cs`，`DontDestroyOnLoad`，**随游戏关闭自动消失**），显示 bridge 处理的请求摘要——前后端两条管道（`easybridge-ui` / `easybridge-state`，后端经 `/monitor/push` 转发汇聚）合并到一处，标 `[ui]`/`[state]`。**可拖动 + 可缩放窗口**：拖标题移动、拖右下角 ↘ 抓手改大小、内容滚动；**F9 显示/隐藏、F8 暂停**。字号**自动跟随游戏「正文字号」设置**。每条两层：

- 顶行（端点）：`[ui] GET /ui  ✓ 200  · 306ms`
- 内层（说明）：`▸ 查看当前打开的界面窗口`；失败再加内层 `↳ 错误`

> 用 IMGUI 画（非 runtime uGUI Canvas——后者在本游戏内不合成）。点击**可能穿透**到窗口下面的游戏：刻意**不禁用游戏 EventSystem**（禁用会让游戏自己的每帧热键检查 NRE）。这些游戏侧坑见 `taiwu-game` skill。

**约定：每次调用都带一句中文说明 `-Note`**，让用户在浮层开启时看懂 agent 正在做什么。说明写**操作的实际目的**（如「点击确认关闭月报」「生成 4 品 NPC 测试关系分支」），**别写调试/流程性内容**（如「心跳」「重启验证」「对照实验」）。`/ping`、`/` 等连通性检查是基础设施、**不计入面板**。`bridge.ps1` 的 `UI`/`SB`/`Invoke-Pipe`（及 `taiwu-statebridge` 的 helper）都支持 `-Note`。例：

```powershell
UI -Path "/action" -Body $clickJson -Note "点击「确认」关闭月报弹窗"
SB -Path "/spawn"  -Body @{grade=4}  -Note "生成一个 4 品 NPC 用于测试关系分支"
```

回看历史用 `GET /log?since=<seq>&max=<n>`（默认/推荐 `max<=20`；普通请求硬限 20，确要更大需 `allowLarge=true`。浮层滚动显最近若干条、最新在上；服务端缓冲 500 条。`/ping`、`/`、`mon=1` 等基础设施请求**不入日志**）。表头不显累计计数，仅在有「无说明」时红字提醒。

### `/ui/{name}` 渐进读取

默认是摘要，不返回控件路径，适合先判断窗口标题和大致区域：

```json
{
  "name": "Mod",
  "showing": true,
  "detail": "summary",
  "title": "模组管理",
  "counts": {"controls": 98, "texts": 1},
  "sections": [{"section": "SubPages", "controls": 80, "texts": 0}],
  "truncated": false
}
```

只关注菜单标题时：

```powershell
Invoke-UiBridge -Path "/ui/Mod" -Query @{ detail = "title" }
```

要点击按钮时再取控件项；默认上限偏小，巨大菜单按 `section=` 或更精确的 `fields=` 分步读：

```json
{
  "name": "EventWindow",
  "showing": true,
  "sections": [
    {
      "section": "SectionName",
      "controls": [{"id": "path@0/to@1/btn@2", "type": "button", "label": "按钮文本"}],
      "texts": [{"id": "path@0/to@1/text@0", "text": "显示文本"}]
    }
  ]
}
```

query 参数：

- `detail=summary|title|full`：默认 `summary`，只有 `full` 返回控件/文本项。
- `fields=controls|texts|controls,texts`：`full` 模式下选择返回字段；找按钮优先 `controls`。
- `section=SectionName`：只返回某个分区的项。
- `max` / `scan`：条目上限和扫描预算；普通请求会被小上限夹住。
- `allowLarge=true`：显式允许更大 `max/scan`，只在确实需要完整大树时使用。

### `/inspect` 与 `/reflect`

```powershell
Invoke-UiBridge -Path "/inspect" -Query @{ element="Combat"; id=""; max="40" }
Invoke-UiBridge -Path "/pointer" -Query @{ x="900"; y="995"; origin="top-left"; max="20" }
Invoke-UiBridge -Path "/reflect" -Query @{
    element="Combat"
    component="ViewCombat"
    member="targetDistanceBar._interactType"
    depth="1"
    max="40"
}
```

`/inspect` 和 `/reflect` 是通用诊断接口，不绑定具体 mod。`id` 使用 `/ui/{name}` 返回的 UI path；留空表示窗口根对象。
`component` 可传组件短名或全名；`member` 是字段点号路径。返回快照深度和成员数受 EasyBridge 设置
`MaxReflectDepth` / `MaxReflectMembers` 硬限制。

`/reflect/invoke`（及 `/reflect/set`/`/static`/`/pin`）**默认关闭**；需要时 `POST /config {"enableInvoke":true}` 开启。常规验证优先使用只读
`/inspect`/`/reflect` 加 `/action` 或真实鼠标输入。

### `/action` 请求体

```json
{"element": "窗口名", "id": "控件id路径", "action": "click|toggle|set|select", "value": "可选值"}
```

- `click`：按钮触发 onClick；toggle 模拟真实点击（正确处理 ToggleGroup）
- `toggle`：设布尔值（`value: true/false`）或翻转
- `set`：input 设文本、slider 设数值
- `select`：dropdown 按索引选择

### `/wait/actions` 请求体

```json
{
  "waitElement": "Combat",
  "actions": [
    {"id": "Top@21/CombatTimeRoot@0/PauseToggle@0", "action": "click"},
    {"id": "Top@21/CombatTimeRoot@0/AutoFight@1", "action": "toggle", "value": false}
  ]
}
```

用于实时界面：先发起这个等待请求，再由另一个管道请求触发界面变化；目标窗口出现后，动作序列会在 Unity
主线程调度中立即执行，避免靠外部轮询速度抢 UI。实时战斗节奏本身不要靠这里抢暂停；使用后端
`easybridge-state` 的 `/combat/watch` 和 `/combat/resume` 在游戏进程内步进。普通节奏控制用 `mode=frames`/`anyReady`；
如果需要停在技能、普攻、道具或其他动作的 Prepare 状态提交前，用 `mode=beforeCommit`。

## 启用新部署的本地 mod（重要，否则后端插件不加载）

新拷到 `<游戏>\Mod\` 的本地 mod **不会自动加载**，必须在主菜单的「模组管理」里启用并重启：

```powershell
# 主菜单 → 模组管理（Mod 窗口）
Invoke-UiBridge -Path "/action" -Body '{"element":"MainMenu","id":"<模组管理按钮id>","action":"click"}'
# Mod 窗口里有搜索框，输入 mod 名过滤；找到该行的开关 toggle（id 形如 .../<行>@N/CellContainerSwitch@3/Content@0/SwitchToggleBig@0）
$m = Invoke-UiBridge -Path "/ui/Mod" -Query @{ detail="full"; fields="controls"; max="120"; allowLarge="true" }
# set 搜索框 → click 该行的 SwitchToggleBig 打开 → click「保存配置」(BtnSave) → Dialog 点「确认」
# 关闭 Mod 窗口(Background/ButtonCloseView) 时弹「确认更改并重启游戏」→ 点「确认」→ 游戏自动重启
```

要点：**后端插件只在游戏启动时加载**，改了后端 DLL 必须重启游戏才生效；启用状态更改也要重启（弹窗会提示）。
直接改 `Save\ModSettings.Lua` 会被安全策略拦截，用此 UI 流程。重启后按下面流程重新加载存档。

## 驱动战斗

**边界**：进入战斗只能走这条**前端 UI 路径**（后端桥没有 `/combat/start`/`/combat/end`），结算只能靠游戏**内置 AutoFight** 打完后等 `CombatResult`，后端无强制开战/判胜负的手段。完整的端到端自包含配方见 `taiwu-statebridge.md`「调试战斗：端到端自包含配方」。

`敌对→袭击/情难自已(强制)` 会进 `CombatBegin`（点 `StartCombatBtn` 开战）。若只需要完整结算，战斗可交给游戏
内置 AutoFight，完成后等 `CombatResult`。若需要验证战斗中的 UI 交互或距离条，必须先用后端 `/combat/watch`
预先 arm，并用 `/combat/resume` 步进；Computer Use 的点击/拖拽只在 `timeScale=0` 且 `/combat.frame` 连续不变后执行。

## 常用模式

### 模式 1：找按钮并点击

```powershell
# 取窗口控件，找匹配标签的按钮，点击
$ui = Invoke-UiBridge -Path "/ui/WindowName" -Query @{ detail = "full"; fields = "controls"; max = "40" }
$btn = $ui.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "目标文字" } | Select-Object -First 1
if ($btn) {
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"WindowName`",`"id`":`"$($btn.id)`",`"action`":`"click`"}"
}
```

### 模式 2：切换标签页（ToggleGroup）

```powershell
$ui = Invoke-UiBridge -Path "/ui/WindowName" -Query @{ detail = "full"; fields = "controls"; max = "40" }
$tab = $ui.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "toggle" -and $_.label -eq "标签名" } | Select-Object -First 1
if ($tab -and -not $tab.value) {
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"WindowName`",`"id`":`"$($tab.id)`",`"action`":`"click`"}"
}
```

### 模式 3：等待窗口出现

```powershell
# 阻塞等待，最多 60 秒
Invoke-UiBridge -Path "/wait" -Query @{ element = "CombatResult"; timeout = "60" }
```

### 模式 4：处理连续事件（读取选项 → 判断 → 选择）

事件窗口经常有多个选项。**你应该读取所有选项文本和事件上下文，根据语义自主判断应该选哪个。**
仅在上下文完全不足以判断时，选最无害的选项（不限定位置）。

```powershell
for ($i = 0; $i -lt 20; $i++) {
    $snap = Invoke-UiBridge -Path "/ui"
    if (-not ($snap.elements | Where-Object { $_.name -eq "EventWindow" })) { break }
    $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; fields = "controls,texts"; max = "80"; allowLarge = "true" }
    $opts = @($ew.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "^\[" -and $_.interactable -ne $false })
    if ($opts.Count -eq 0) { break }
    # 输出所有选项和事件文本，供你阅读理解后自行选择
    foreach ($sec in $ew.sections) {
        if ($sec.texts) { foreach ($t in $sec.texts) { if ($t.text) { Write-Output "  TEXT: $($t.text)" } } }
    }
    foreach ($o in $opts) { Write-Output "  OPTION: $($o.label)" }
    # 根据选项含义选择最合适的那个
    $chosen = $opts[...]  # 由你判断
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($chosen.id)`",`"action`":`"click`"}"
    Start-Sleep 1
}
```

## 游戏流程参考：袭击 NPC

以下是完整的 "加载存档 → 袭击 NPC → 确认结果 → 退出" 流程。每一步都是独立的 PowerShell 调用（必须重新定义函数）。

### 步骤 1：启动游戏并等待

```powershell
Start-Process "游戏路径\The Scroll Of Taiwu.exe"
# 轮询等待管道可用（最多 60 秒）
for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep 5
    try { $r = Invoke-UiBridge -Path "/ping"; if ($r.ok) { break } } catch {}
}
```

### 步骤 2：主菜单 → 点击"自由模式"

预期窗口：`MainMenu`。找 label 包含 "自由模式" 的按钮。

```powershell
$mm = Invoke-UiBridge -Path "/ui/MainMenu" -Query @{ detail = "full"; fields = "controls"; max = "40" }
$btn = $mm.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "自由模式" } | Select-Object -First 1
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MainMenu`",`"id`":`"$($btn.id)`",`"action`":`"click`"}"
```

### 步骤 3：存档选择 → 加载第一个存档

预期窗口：`RecordSelect`。找 label 包含 "展开绘卷" 的按钮。

```powershell
Start-Sleep 2
$rs = Invoke-UiBridge -Path "/ui/RecordSelect" -Query @{ detail = "full"; fields = "controls"; max = "40" }
$btn = $rs.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "展开绘卷" } | Select-Object -First 1
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"RecordSelect`",`"id`":`"$($btn.id)`",`"action`":`"click`"}"
```

### 步骤 4：处理加载后弹窗

加载存档后可能出现：`MonthNotify`（月报）、`EventWindow`（事件）、`Dialog`（对话框）。需要逐个处理直到回到地图。

```powershell
Start-Sleep 10  # 存档加载需要时间
# 循环处理弹窗
for ($round = 0; $round -lt 30; $round++) {
    $snap = Invoke-UiBridge -Path "/ui"
    $names = @($snap.elements | ForEach-Object { $_.name })
    if ($names -contains "MonthNotify") {
        # 关闭月报 — 点 Close 按钮
        $mn = Invoke-UiBridge -Path "/ui/MonthNotify" -Query @{ detail = "full"; fields = "controls"; max = "40" }
        $close = $mn.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.id -match "Close" } | Select-Object -First 1
        if ($close) { Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MonthNotify`",`"id`":`"$($close.id)`",`"action`":`"click`"}" | Out-Null }
        Start-Sleep 1; continue
    }
    if ($names -contains "EventWindow") {
        # 事件窗口 — 点第一个可用选项（加载时的事件通常只有一个选项）
        $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; fields = "controls,texts"; max = "80"; allowLarge = "true" }
        $opts = @($ew.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "^\[" -and $_.interactable -ne $false })
        if ($opts.Count -gt 0) { Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($opts[0].id)`",`"action`":`"click`"}" | Out-Null }
        Start-Sleep 1; continue
    }
    if ($names -contains "Dialog") {
        # 对话框 — 点确认
        $dlg = Invoke-UiBridge -Path "/ui/Dialog" -Query @{ detail = "full"; max = "30" }
        $yes = $dlg.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.label -match "确认|确定" } | Select-Object -First 1
        if ($yes) { Invoke-UiBridge -Path "/action" -Body "{`"element`":`"Dialog`",`"id`":`"$($yes.id)`",`"action`":`"click`"}" | Out-Null }
        Start-Sleep 1; continue
    }
    # 没有弹窗了，检查是否在地图上
    $all = Invoke-UiBridge -Path "/elements"
    $mapShowing = $all.elements | Where-Object { $_.name -eq "MapBlockCharList" -and $_.showing }
    if ($mapShowing) { Write-Output "On map."; break }
    Start-Sleep 1
}
```

### 步骤 5：找 NPC 并打开交互

预期元素：`MapBlockCharList`（showing=true，但不在 UI 栈中）。

```powershell
$cl = Invoke-UiBridge -Path "/ui/MapBlockCharList" -Query @{ detail = "full"; fields = "controls"; max = "40" }
# NPC 在 Viewport section 中，类型为 button
$npc = $cl.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.id -match "Viewport.*Content" } | Select-Object -First 1
Write-Output "Target: $($npc.label)"
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MapBlockCharList`",`"id`":`"$($npc.id)`",`"action`":`"click`"}"
```

### 步骤 6：NPC 交互 → 切到"敌对"标签 → 选"袭击"

预期窗口：`EventWindow`。标签页是 toggle 控件。

```powershell
Start-Sleep 2
$ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; fields = "controls"; max = "40" }
# 切到敌对标签
$hostile = $ew.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "toggle" -and $_.label -eq "敌对" } | Select-Object -First 1
if ($hostile -and -not $hostile.value) {
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($hostile.id)`",`"action`":`"click`"}" | Out-Null
    Start-Sleep 1
}
# 重新取选项（切标签后选项列表会变）
$ew2 = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; fields = "controls"; max = "80"; allowLarge = "true" }
$attack = $ew2.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "袭击" } | Select-Object -First 1
Write-Output "Attack option: $($attack.label)"
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($attack.id)`",`"action`":`"click`"}"
```

### 步骤 7：确认对话框

袭击触发 "重要选择" 确认框。

```powershell
Start-Sleep 1
$snap = Invoke-UiBridge -Path "/ui"
if ($snap.elements | Where-Object { $_.name -eq "Dialog" }) {
    $dlg = Invoke-UiBridge -Path "/ui/Dialog" -Query @{ detail = "full"; max = "30" }
    $yes = $dlg.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.label -match "确认" } | Select-Object -First 1
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"Dialog`",`"id`":`"$($yes.id)`",`"action`":`"click`"}"
}
```

### 步骤 8：开始战斗

预期窗口：`CombatBegin`。

```powershell
Start-Sleep 2
$cb = Invoke-UiBridge -Path "/ui/CombatBegin" -Query @{ detail = "full"; fields = "controls"; max = "40" }
$start = $cb.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.id -match "StartCombatBtn" } | Select-Object -First 1
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"CombatBegin`",`"id`":`"$($start.id)`",`"action`":`"click`"}"
```

### 步骤 9：等待自动战斗完成

战斗期间 activeCount=0。完成后出现 `CombatResult`。

```powershell
$r = Invoke-UiBridge -Path "/wait" -Query @{ element = "CombatResult"; timeout = "120" }
Write-Output "Combat done: $($r.ok)"
```

### 步骤 10：查看战斗结果并确认

```powershell
$cr = Invoke-UiBridge -Path "/ui/CombatResult" -Query @{ detail = "full"; fields = "controls,texts"; max = "80"; allowLarge = "true" }
$cr.sections | ForEach-Object { $_.texts } | ForEach-Object { $_ } | Where-Object { $_.text } | ForEach-Object { Write-Output $_.text }
$confirm = $cr.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.id -match "ConfirmButton" } | Select-Object -First 1
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"CombatResult`",`"id`":`"$($confirm.id)`",`"action`":`"click`"}"
```

### 步骤 11：处理战后事件

战后可能出现事件选择（如处置战败 NPC）。**读取事件文本和所有选项，根据语义自主判断选哪个。**
仅在无法判断时选最无害的选项。不要机械地选第一个或最后一个。

```powershell
Start-Sleep 2
for ($i = 0; $i -lt 10; $i++) {
    $snap = Invoke-UiBridge -Path "/ui"
    if (-not ($snap.elements | Where-Object { $_.name -eq "EventWindow" })) { break }
    $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; fields = "controls,texts"; max = "80"; allowLarge = "true" }
    # 输出事件文本（帮助理解上下文）
    foreach ($sec in $ew.sections) {
        if ($sec.texts) { foreach ($t in $sec.texts) { if ($t.text) { Write-Output "  TEXT: $($t.text)" } } }
    }
    $opts = @($ew.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "^\[" -and $_.interactable -ne $false })
    if ($opts.Count -eq 0) { break }
    foreach ($o in $opts) { Write-Output "  OPTION: $($o.label)" }
    # 根据选项含义判断，选最合适的（如释放、离开等无害选项）
    $chosen = $opts[...]  # 由你判断
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($chosen.id)`",`"action`":`"click`"}" | Out-Null
    Start-Sleep 1
}
```

### 步骤 12：退出游戏（不保存）

```powershell
Invoke-UiBridge -Path "/quit" -Body '{}'
Start-Sleep 5
# 确认游戏已退出
$p = Get-Process -Name "The Scroll Of Taiwu" -ErrorAction SilentlyContinue
if ($p) { Stop-Process -Id $p.Id -Force }
```
