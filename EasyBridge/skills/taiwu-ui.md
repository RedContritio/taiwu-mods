---
name: taiwu-ui
description: Inspect and interact with the running Taiwu game UI via the EasyBridge mod's frontend named pipe (taiwu-uibridge). Use when you need to verify game UI state, find UI elements, or perform actions (click, toggle, set, select) during automated testing.
---

# EasyBridge UI Skill

与运行中的太吾绘卷游戏 UI 交互，用于自动化测试和验证。

## 关键规则

1. **每个 PowerShell 调用都必须以函数定义开头** — shell 状态不跨调用保留
2. **动作后必须重新取快照** — id 在 UI 变化后失效
3. **先 `/ui` 看全貌，再 `/ui/{name}` 下钻** — 不要猜窗口名
4. **用 `-match` 搜索标签** — 标签已去除富文本标签，可直接用中文匹配
5. **不能发 ESC/键盘** — 只有 click/toggle/set/select。事件/交互窗口要靠**窗口内的“继续/确认/关闭”按钮**关闭；
   像 NPC 交互这种**模态窗**没关掉会挡住后续的地图角色点击（点新 NPC 无效，误触旧窗）。换目标前先 `/ui` 确认它已关。

## 函数定义（每次调用必须包含）

```powershell
function Invoke-UiBridge {
    param([string]$Path, [hashtable]$Query, [string]$Body)
    $req = @{ path = $Path }
    if ($Query) { $req.query = $Query }
    if ($Body) { $req.body = $Body }
    $json = $req | ConvertTo-Json -Compress
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "taiwu-uibridge", [System.IO.Pipes.PipeDirection]::InOut)
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
| `/ui/{name}` | GET | 单窗口语义树（控件 + 文本，按区域分组） |
| `/find?q=关键词` | GET | 跨窗口文本搜索 |
| `/elements` | GET | 已知窗口目录 |
| `/action` | POST | 动作注入（click/toggle/set/select） |
| `/wait?element=Name&timeout=60` | GET | 阻塞等待指定窗口出现 |
| `/quit` | POST | 退出游戏（不保存） |

### `/ui/{name}` 返回结构

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

query 参数：`detail=full` 全量模式，`max=200` 条目上限。

### `/action` 请求体

```json
{"element": "窗口名", "id": "控件id路径", "action": "click|toggle|set|select", "value": "可选值"}
```

- `click`：按钮触发 onClick；toggle 模拟真实点击（正确处理 ToggleGroup）
- `toggle`：设布尔值（`value: true/false`）或翻转
- `set`：input 设文本、slider 设数值
- `select`：dropdown 按索引选择

## 启用新部署的本地 mod（重要，否则后端插件不加载）

新拷到 `<游戏>\Mod\` 的本地 mod **不会自动加载**，必须在主菜单的「模组管理」里启用并重启：

```powershell
# 主菜单 → 模组管理（Mod 窗口）
Invoke-UiBridge -Path "/action" -Body '{"element":"MainMenu","id":"<模组管理按钮id>","action":"click"}'
# Mod 窗口里有搜索框，输入 mod 名过滤；找到该行的开关 toggle（id 形如 .../<行>@N/CellContainerSwitch@3/Content@0/SwitchToggleBig@0）
$m = Invoke-UiBridge -Path "/ui/Mod" -Query @{ detail="full"; max="300" }
# set 搜索框 → click 该行的 SwitchToggleBig 打开 → click「保存配置」(BtnSave) → Dialog 点「确认」
# 关闭 Mod 窗口(Background/ButtonCloseView) 时弹「确认更改并重启游戏」→ 点「确认」→ 游戏自动重启
```

要点：**后端插件只在游戏启动时加载**，改了后端 DLL 必须重启游戏才生效；启用状态更改也要重启（弹窗会提示）。
直接改 `Save\ModSettings.Lua` 会被安全策略拦截，用此 UI 流程。重启后按下面流程重新加载存档。

## 驱动战斗

`敌对→袭击/情难自已(强制)` 会进 `CombatBegin`（点 `StartCombatBtn` 开战）。战斗是手动的，但游戏**内置 AutoFight 默认开**
（Combat 窗 `Top.../AutoFight` toggle=True），弱目标会自动打完，~20-95s 后出 `CombatResult`（点 `ConfirmButton` 确认）。
用 `/wait?element=CombatResult&timeout=120` 阻塞等待即可，无需手动操作技能。

## 常用模式

### 模式 1：找按钮并点击

```powershell
# 取窗口控件，找匹配标签的按钮，点击
$ui = Invoke-UiBridge -Path "/ui/WindowName" -Query @{ detail = "full"; max = "200" }
$btn = $ui.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "目标文字" } | Select-Object -First 1
if ($btn) {
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"WindowName`",`"id`":`"$($btn.id)`",`"action`":`"click`"}"
}
```

### 模式 2：切换标签页（ToggleGroup）

```powershell
$ui = Invoke-UiBridge -Path "/ui/WindowName" -Query @{ detail = "full"; max = "200" }
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
    $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; max = "200" }
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
$mm = Invoke-UiBridge -Path "/ui/MainMenu" -Query @{ detail = "full"; max = "100" }
$btn = $mm.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.label -match "自由模式" } | Select-Object -First 1
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MainMenu`",`"id`":`"$($btn.id)`",`"action`":`"click`"}"
```

### 步骤 3：存档选择 → 加载第一个存档

预期窗口：`RecordSelect`。找 label 包含 "展开绘卷" 的按钮。

```powershell
Start-Sleep 2
$rs = Invoke-UiBridge -Path "/ui/RecordSelect" -Query @{ detail = "full"; max = "100" }
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
        $mn = Invoke-UiBridge -Path "/ui/MonthNotify" -Query @{ detail = "full"; max = "50" }
        $close = $mn.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.id -match "Close" } | Select-Object -First 1
        if ($close) { Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MonthNotify`",`"id`":`"$($close.id)`",`"action`":`"click`"}" | Out-Null }
        Start-Sleep 1; continue
    }
    if ($names -contains "EventWindow") {
        # 事件窗口 — 点第一个可用选项（加载时的事件通常只有一个选项）
        $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; max = "200" }
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
    $all = Invoke-UiBridge -Path "/elements" -Query @{ onlyActive = "false" }
    $mapShowing = $all.elements | Where-Object { $_.name -eq "MapBlockCharList" -and $_.showing }
    if ($mapShowing) { Write-Output "On map."; break }
    Start-Sleep 1
}
```

### 步骤 5：找 NPC 并打开交互

预期元素：`MapBlockCharList`（showing=true，但不在 UI 栈中）。

```powershell
$cl = Invoke-UiBridge -Path "/ui/MapBlockCharList" -Query @{ detail = "full"; max = "50" }
# NPC 在 Viewport section 中，类型为 button
$npc = $cl.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "button" -and $_.id -match "Viewport.*Content" } | Select-Object -First 1
Write-Output "Target: $($npc.label)"
Invoke-UiBridge -Path "/action" -Body "{`"element`":`"MapBlockCharList`",`"id`":`"$($npc.id)`",`"action`":`"click`"}"
```

### 步骤 6：NPC 交互 → 切到"敌对"标签 → 选"袭击"

预期窗口：`EventWindow`。标签页是 toggle 控件。

```powershell
Start-Sleep 2
$ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; max = "200" }
# 切到敌对标签
$hostile = $ew.sections | ForEach-Object { $_.controls } | ForEach-Object { $_ } | Where-Object { $_.type -eq "toggle" -and $_.label -eq "敌对" } | Select-Object -First 1
if ($hostile -and -not $hostile.value) {
    Invoke-UiBridge -Path "/action" -Body "{`"element`":`"EventWindow`",`"id`":`"$($hostile.id)`",`"action`":`"click`"}" | Out-Null
    Start-Sleep 1
}
# 重新取选项（切标签后选项列表会变）
$ew2 = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; max = "300" }
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
$cb = Invoke-UiBridge -Path "/ui/CombatBegin" -Query @{ detail = "full"; max = "50" }
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
$cr = Invoke-UiBridge -Path "/ui/CombatResult" -Query @{ detail = "full"; max = "100" }
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
    $ew = Invoke-UiBridge -Path "/ui/EventWindow" -Query @{ detail = "full"; max = "200" }
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
