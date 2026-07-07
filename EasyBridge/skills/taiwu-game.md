---
name: taiwu-game
description: 太吾绘卷（The Scroll of Taiwu）游戏本身的内部性质与约定 —— 架构、品级规则、UI 系统、本地化、角色数据语义，以及改 mod / 自动化时反复踩到的坑。任何 Taiwu mod 开发或用 EasyBridge 操控游戏前先读这份；具体 bridge API 见 taiwu-ui / taiwu-statebridge。
---

# 太吾绘卷 游戏内部性质（modding 通用）

> 下面的类名/字段名来自 `_decompiled/Assembly-CSharp`（前端）与 GameData（后端）。**游戏更新后类名/行号可能漂移**，断言前先对当前反编译核实。Steam AppID **838350**，游戏目录 `D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu`。
>
> **本文件只放高频要点 + 索引**；深入 / 情境化的细节放在 `references/` 子目录、**按需加载**（如做斗蛐蛐时才读 `references/cricket-combat.md`）。新增细节请同样拆进 `references/`，别堆进主文件。

## 1. 架构：前后端双进程

- **前端**（Unity **Mono / net48**）：`The Scroll of Taiwu_Data\Managed\`，主程序集 `Assembly-CSharp.dll`，UI 用 **uGUI + TextMeshPro**。负责一切**界面与输入**。
- **后端**（**.NET 8.0**）：`Backend\`，`GameData.*.dll`。负责**游戏数据/世界逻辑**。
- 两个 `TaiwuModdingLib.dll`（各运行时一份）。Harmony = `0Harmony.dll`。
- **两进程不共享内存/静态**：前端 mod 改不到后端对象，反之亦然。EasyBridge 因此是两条独立管道（`easybridge-ui` 前端 / `easybridge-state` 后端）。
- **Mod 结构**（Lua 清单，无 BepInEx）：`config.lua`（Title/Author/Version/**FrontendPlugins**/**BackendPlugins**/设置 UI 声明）+ `Settings.Lua` + `Plugins/*.dll`。插件继承 `TaiwuModdingLib...TaiwuRemakePlugin`，`Initialize()`/`Dispose()`/`OnModSettingUpdate()`。
- 存档：`Save/world_1`（+ backup）。

## 2. 启动 & 部署（实测可靠法）

- **可靠启动**：Steam 在跑的前提下 `$env:SteamAppId="838350"; $env:SteamGameId="838350"; Start-Process "<game>\The Scroll of Taiwu.exe" -WorkingDirectory "<game>"`。
  - **坑**：`TaiwuLauncher.exe` 跑完即退、**不拉起游戏**；`steam://rungameid/838350` 会被 Steam 的**虚假「无法同步」云存档框**挡住（此游戏没云存档）→ 进程根本不起。
- **部署 mod**：改前端 C# 后**必须先退游戏**（DLL 被占）→ deploy → 重启。用 `bridge /ping` 轮询就绪（`/ping` 不计入监视面板）。

## 3. 品级约定（读 grade 前**必看**，最容易念反）

太吾**显示品级 = 1 品最高、9 品最低**（一品最强、九品垫底）。但代码/数据里的**内部 `grade`/`Level` 多为 0~8（0 最低、8 最高）**，与显示**正好相反**：

- **显示品 = `9 − 内部grade`**（内部 0→9品，内部 8→1品，内部 3→6品）。
- 颜色 `Colors.Instance.GradeColors[0..8]`：index 0=最低(灰)、8=最高(红)，与名字品级色一致。
- **读到一个 grade 数字，先确认它是「内部 0~8」还是「显示 1~9」**，别把内部 index 当显示品级念。

## 4. UI 系统（前端）

- **只有一个 OS 窗口**。游戏里那些「弹窗」（对话框、事件窗、语言选择）都是**同一个窗口内部的 uGUI 面板**，不是独立系统窗口。Unity 标准运行时**没有再开可移动桌面窗的 API**（`Display.displays` 是多显示器全屏、非可拖动窗）。
- **`UIManager`**（单例 `UIManager.Instance`）：`_curElements`（当前栈顶元素们）、`IsFocusElement(UIElement)`、`BlockHotKey`、`CheckQuickHide()`（每帧、`_curElements.Count>0` 时跑）、`UiCamera`。
- **元素体系**：`UIElement`（枚举键）/ `UIBase`（视图基类，有 `Element`）/ `UIGroup`。`UiElementCatalog`（EasyBridge）反射枚举。
- **热键**：`HotKeyCommand.Check(...)` + `CommandKitBase`（`_customKeyGroup` 持自定义键位；`GetDisable()` 全局禁用位）。`EventWindowCommandKit`/`CommonCommandKit` 等是具体命令集。
- **缩放**：UI 用 `CanvasScaler.ScaleMode.ScaleWithScreenSize`（按分辨率自动缩放，**不是**用户设置）。文本用 **TextMeshPro**。
- **唯一用户字号设置**：事件窗正文字号 `EventWindow.s_savedContentFontSize`（命名空间 `Game.Components.EventWindow`，**18~36 / 默认 24**，session 级、由事件窗滑块改、不持久）。想「跟随游戏字号」就读它（EasyBridge 监视窗即按 `gameFont/24` 比例缩放）。

## 5. 本地化

- `LocalStringManager`（`CurLanguageType`、`GetFormat(LanguageKey, ...)`），`LanguageType`（**CN / EN**），`LanguageKey`（枚举）。中文/英文同字号常不同（代码里多见 `CN?24:22` 之类）。
- EasyBridge 取到的 label 已 `StripRichText` 去富文本标签，可直接用中文 `-match`。

## 6. 角色 / 数据语义（后端，常用）

- `CreatingType`：**0=固定剧情NPC / 1=凡人(随机生成) / 2=奇遇 / 3=敌对**。排除剧情人物常用 `CreatingType==1` 之类判断（见 DreamLover）。
- 关键 id 经后端 `/taiwu`：`TaiwuCharId`（太吾本人）、`closeFriendId`、`villageSettlementId` 等。
- 蛐蛐：`(colorId,partId).CalcCricketGrade()` 返回**内部 0~8**；斗蛐蛐对手按对手 org 品级**现生成**（`CricketGenerator`）。蛐蛐**品名与品级反直觉**：`八败`=templateId 21=内部 Level 8=**1品(最强)**；`呆物`=templateId 0=Level 0=**9品(最弱)**。品名只是品种,不是品级。

## 7. 斗蛐蛐（搭场景 / 卡事件断点）

斗蛐蛐是**预模拟 + 事件队列回放**（非实时）：开局 `Simulate()` 把整局算成事件队列 `Board._matchContext.Logs`，再逐条出队回放。**要搭场景或"停在某事件"（如八败首次攻击）就读事件队列、别读 Spine 动画；用现有桥反射即可精确卡断点、无需编译。**

> 完整配方（搭场景的 `/eval` 一套 + 反射读事件队列卡断点）见 **[references/cricket-combat.md](references/cricket-combat.md)** —— 做斗蛐蛐时再加载。

## 8. 改 mod 的坑（都很贵、实机踩出来的）

1. **runtime uGUI Canvas 在本游戏内不合成！** mod 新加一个 `ScreenSpaceOverlay` Canvas+Image+Text，GameObject active、无异常、Update 在跑——但**屏上什么都不画**。做屏幕浮层**用 IMGUI `OnGUI`**（立即模式，永远画在相机之上），别赌 runtime Canvas。
2. **别禁用游戏 EventSystem！** 为挡 IMGUI 点击穿透而 `EventSystem.current.enabled=false`，会让**游戏自己的每帧热键检查 `HotKeyCommand.Check`（经 `UIManager.CheckQuickHide`/`UI_PermanentTips.Update`）空指针刷屏**（`EventSystem.current` 失效，游戏 UI 代码没料到）。要挡穿透用**透明 uGUI 射线拦截图**，别动整个 EventSystem。读代码看不出的因果，用**对照实验**（有/无某段各跑一次数 NRE）定性。
3. **诊断游戏侧报错**：grep 前端 `Player.log`（`%USERPROFILE%\AppData\LocalLow\ConchShip\The Scroll Of Taiwu\Player.log`）的 `Exception`/`NullReference` 看调用栈；后端日志另在 `Backend` 侧。
4. **截游戏内浮层**：`PrintWindow(h,hdc,2)` 抓得到游戏自身 UI 但**抓不到 mod 的 IMGUI/overlay 层**；用 `Graphics.CopyFromScreen`（真实合成帧）+ 把游戏提到最前（`keybd_event` 点 ALT 解锁 `SetForegroundWindow` + `SetWindowPos` TOPMOST）。**且必须 `SetProcessDPIAware()`**：4K(150%)下 DPI-unaware 只拿到降采样 2560×1440，深色半透明面板几乎看不见、会误判「没渲染」。`System.Drawing.Bitmap` 用 **Windows PowerShell 5.1**（pwsh7 缺 System.Drawing.Common）。

## 9. 不想手写反射？用 EasyBridge

游戏内任意类/对象的检视与改写，经 EasyBridge 通用反射端点（写/调类**默认关闭**，需要时先 `POST /config {"enableInvoke":true}`）：`GET /reflect`、`GET /inspect`、`POST /static`（静态方法/读静态字段，`$ref` 可解析静态成员）、`POST /reflect/invoke`、`POST /reflect/set`。前端见 **taiwu-ui**，后端见 **taiwu-statebridge**。例：读 `EventWindow.s_savedContentFontSize`、调 `GameObject.Find(...)` 都能 `/static` 搞定。
