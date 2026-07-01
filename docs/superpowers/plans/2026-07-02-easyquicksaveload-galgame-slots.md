# EasyQuickSaveLoad Galgame 栏位存档 + 原生 ESC 嵌入 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 EasyQuickSaveLoad 从"时间戳滚动备份 + 自绘悬浮面板"改造成 galgame 式栏位存档系统（1 独立快捷栏 + N 可配普通栏，各栏可存/读/删），入口用克隆的原生 CButton 嵌进 ESC 系统选项，确认走原生 Dialog。

**Architecture:** 后端把 3 个 mod 方法换成 4 个栏位化方法（SaveToSlot/ListSlots/LoadFromSlot/DeleteSlot），存到按栏命名的文件。前端拆成聚焦的小文件：可复用核心 EqslCore、原生确认 NativeDialog、ESC 按钮注入器 SystemOptionButtonInjector、自建档位面板 SlotPanel。前端调后端一律用运行期 ModIdStr。

**Tech Stack:** C# / .NET 8（后端，游戏 GameData 进程）+ C# / Unity Mono net48（前端）；TaiwuModdingLib；游戏反编译源在 `_decompiled/`；实机检视/驱动用 EasyBridge 桥（`_scratch/bridge.ps1` 的 `SB`/`UI`，`/eval` `/static` `/ui` `/inspect` `/action`）。

## Global Constraints

- **无单元测试**：本 mod 代码强耦合游戏运行期类型（`UIElement`/`DomainManager`/`ViewSystemOption`/`CButton`/反射），无法在游戏外单测。每个任务的"测试"是：`dotnet build <csproj> -c Release`（0 error）→ 部署 → 进游戏用 EasyBridge 探针验证。构建命令务必**显式跑 dotnet build 看 0 error**——`deploy.ps1` 在 build 失败时会静默拷旧 DLL。
- **改后端 DLL 必须先关游戏**（运行时 DLL 被锁）；改前端 DLL 也建议关游戏后部署再重启。
- **部署**：`./deploy.ps1 -ModName EasyQuickSaveLoad -NoBuild`（先手动 build 过），核对游戏目录 `Mod/EasyQuickSaveLoad/Plugins/*.dll` 的 LastWriteTime。
- **前端调后端 mod 方法必须用运行期 `ModIdStr`（如 "0_2"）**，不是显示名 "EasyQuickSaveLoad"。后端注册键 = `<ModIdStr>.Method.<name>`。`FrontendPlugin.Initialize` 里注入。
- **config.lua / Settings.Lua 必须是纯字面量**（不能有函数/表达式）。
- 存档文件放当前存档目录 `Common.GetArchiveDataDirectory(archiveId)`；**绝不触碰游戏主档 `local.sav`**。
- 测试用 world_1 存档（太吾鉴忠 id 6818，已过引导）。测试前备份 `SaveGames/world_1`，测后还原、清测试栏文件、校验 `local.sav` 哈希不变。
- 反射目标 `SaveWorldAt`/`LoadWorldAt`/`SetLoadedAllArchiveData`/`SetCurrGameWorldType`/`InitAchievements`/`PackAllCrossArchiveGameData`/`LeaveWorld`/`Common.*` 已在现有代码验证可用，保留其调用序列不动。

## 文件结构

后端（`EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/`）：
- `SlotStore.cs`（新）：栏位文件路径、save/list/load/delete 实现、槽数读取。纯静态。
- `BackendPlugin.cs`（重写）：注册 4 个栏位 mod 方法，方法体薄，转发到 SlotStore + 复用现有加载序列。

前端（`EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/`）：
- `EqslCore.cs`（新）：`CanOperate`、当前栏、`WorldInfo` 摘要格式化、`LoadSlotWorld` 载入协程、四个后端调用的 helper（按 ModId 分发）、ModId 持有。
- `NativeDialog.cs`（新）：`Confirm(title, content, onYes)` 走原生 `DialogCmd` + `UIElement.Dialog`。
- `SystemOptionButtonInjector.cs`（新，MonoBehaviour）：侦测 `ViewSystemOption`、克隆 4 个 CButton + 分隔、生命周期、置灰、点击路由。
- `SlotPanel.cs`（新，MonoBehaviour）：自建档位面板（存/读/删模式，克隆原生元素）。
- `FrontendPlugin.cs`（改）：注入 ModId、创建 injector host。
- `EasyQuickSaveLoadOverlay.cs`（删除）：被上面替换。

---

## Task 1: 后端栏位存储 SlotStore + 4 个 mod 方法

把时间戳环换成栏位模型。快捷栏约定 `slot = -1`，普通栏 `slot = 0..SlotCount-1`。

**Files:**
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/SlotStore.cs`
- Modify (rewrite): `EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/BackendPlugin.cs`

**Interfaces:**
- Produces（前端 Task 2/5/6 依赖，经 mod 方法名 + `SerializableModData` 键约定）：
  - mod 方法 `SaveToSlot`（Action，参数键 `Slot:int`）
  - mod 方法 `LoadFromSlot`（Action，参数键 `Slot:int`）
  - mod 方法 `DeleteSlot`（Action，参数键 `Slot:int`）
  - mod 方法 `ListSlots`（Func 带返回，无参）→ 返回 `SerializableModData`：键 `Ok:bool`、`QuickInfo:ArchiveInfo`（快捷栏，`WorldInfo` 可空）、`SlotInfos:ArchiveInfo`（普通栏聚合：`BackupWorldsInfo` 里第 i 项 timestamp 存槽号 i、worldInfo 为该槽摘要或 null 占位）、`SlotCount:int`。
- 参数键常量前端需一致：`Slot`、`Ok`、`QuickInfo`、`SlotInfos`、`SlotCount`。

- [ ] **Step 1: 写 SlotStore.cs**

```csharp
using System;
using System.IO;
using GameData.ArchiveData;
using GameData.Common;

namespace EasyQuickSaveLoad.Backend
{
    /// <summary>栏位存档文件管理：快捷栏 slot=-1 → local.sav.qsl.quick；普通栏 N → local.sav.qsl.slot.N。</summary>
    internal static class SlotStore
    {
        public const int QuickSlot = -1;
        private const string QuickFileName = "local.sav.qsl.quick";
        private const string SlotFilePrefix = "local.sav.qsl.slot.";
        public const int DefaultSlotCount = 20;

        public static string GetSlotPath(sbyte archiveId, int slot)
        {
            string dir = Common.GetArchiveDataDirectory(archiveId);
            string name = slot == QuickSlot ? QuickFileName : SlotFilePrefix + slot;
            return Path.Combine(dir, name);
        }

        public static void EnsureDir(sbyte archiveId)
        {
            Directory.CreateDirectory(Common.GetArchiveDataDirectory(archiveId));
        }

        /// <summary>读某栏的 WorldInfo 摘要；无文件/读失败返回 null。</summary>
        public static WorldInfo TryReadWorldInfo(sbyte archiveId, int slot)
        {
            string path = GetSlotPath(archiveId, slot);
            try
            {
                if (File.Exists(path) &&
                    new LocalArchiveFile(path).TryGetArchiveInfo(out ArchiveInfo info) &&
                    info != null)
                {
                    return info.WorldInfo;
                }
            }
            catch
            {
                // 读损坏文件不应崩溃
            }
            return null;
        }

        public static bool SlotExists(sbyte archiveId, int slot)
        {
            return File.Exists(GetSlotPath(archiveId, slot));
        }

        public static void DeleteSlotFile(sbyte archiveId, int slot)
        {
            try
            {
                string path = GetSlotPath(archiveId, slot);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 删除失败静默（文件可能被占用）
            }
        }
    }
}
```

- [ ] **Step 2: 重写 BackendPlugin.cs**

完整替换文件内容：

```csharp
using System;
using System.Reflection;
using GameData.Common;
using GameData.ArchiveData;
using GameData.Domains;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using TaiwuModdingLib.Core.Plugin;

namespace EasyQuickSaveLoad.Backend
{
    [PluginConfig("EasyQuickSaveLoad", "RedContritio", "1.0.0.0")]
    public sealed class BackendPlugin : TaiwuRemakePlugin
    {
        private const string SaveToSlotMethod = "SaveToSlot";
        private const string LoadFromSlotMethod = "LoadFromSlot";
        private const string DeleteSlotMethod = "DeleteSlot";
        private const string ListSlotsMethod = "ListSlots";

        private const string SlotKey = "Slot";
        private const string OkKey = "Ok";
        private const string QuickInfoKey = "QuickInfo";
        private const string SlotInfosKey = "SlotInfos";
        private const string SlotCountKey = "SlotCount";
        private const string SlotCountSetting = "SlotCount";
        private const sbyte ArchiveStatusGood = 1;

        public override void Initialize()
        {
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, SaveToSlotMethod, SaveToSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, LoadFromSlotMethod, LoadFromSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, DeleteSlotMethod, DeleteSlot));
            TryRegister(() => DomainManager.Mod.AddModMethod(ModIdStr, ListSlotsMethod, ListSlots));
        }

        public override void Dispose()
        {
        }

        private static void TryRegister(Action register)
        {
            try { register(); }
            catch { /* 开发热重载时可能已注册 */ }
        }

        private int GetSlotCount()
        {
            try
            {
                int n = GetSetting.Int(ModIdStr, SlotCountSetting);
                return n > 0 ? n : SlotStore.DefaultSlotCount;
            }
            catch
            {
                return SlotStore.DefaultSlotCount;
            }
        }

        // --- Save ---

        private void SaveToSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            sbyte archiveId = Common.GetCurrArchiveId();
            SlotStore.EnsureDir(archiveId);
            string path = SlotStore.GetSlotPath(archiveId, slot);
            // completeNextFrame:true → 存独立文件、不占用原生 .bak
            InvokeGlobalPrivate("SaveWorldAt",
                new[] { typeof(DataContext), typeof(string), typeof(bool) },
                context, path, true);
        }

        // --- List ---

        private SerializableModData ListSlots(DataContext context, SerializableModData parameter)
        {
            sbyte archiveId = Common.GetCurrArchiveId();
            int slotCount = GetSlotCount();

            var quick = new ArchiveInfo { Status = ArchiveStatusGood, WorldInfo = SlotStore.TryReadWorldInfo(archiveId, SlotStore.QuickSlot) };

            var slots = new ArchiveInfo
            {
                Status = ArchiveStatusGood,
                BackupWorldsInfo = new System.Collections.Generic.List<(long timestamp, WorldInfo worldInfo)>()
            };
            for (int i = 0; i < slotCount; i++)
            {
                // timestamp 字段复用为槽号 i；worldInfo 为该槽摘要或 null（空栏）
                slots.BackupWorldsInfo.Add((i, SlotStore.TryReadWorldInfo(archiveId, i)));
            }

            var result = new SerializableModData();
            result.Set(OkKey, true);
            result.Set(SlotCountKey, slotCount);
            result.Set(QuickInfoKey, quick);
            result.Set(SlotInfosKey, slots);
            return result;
        }

        // --- Delete ---

        private void DeleteSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            SlotStore.DeleteSlotFile(Common.GetCurrArchiveId(), slot);
        }

        // --- Load ---

        private void LoadFromSlot(DataContext context, SerializableModData parameter)
        {
            int slot = ReadSlot(parameter);
            sbyte archiveId = Common.GetCurrArchiveId();
            string path = SlotStore.GetSlotPath(archiveId, slot);
            if (!System.IO.File.Exists(path))
            {
                throw new System.IO.FileNotFoundException("Slot save file not found.", path);
            }

            // 与已验证的加载序列一致（顺序修复保留）：
            if (Common.IsInWorld())
            {
                DomainManager.Global.PackAllCrossArchiveGameData();
                DomainManager.Global.LeaveWorld();
            }
            InvokeGlobalPrivate("SetLoadedAllArchiveData", new[] { typeof(bool), typeof(DataContext) }, false, null);
            InvokeGlobalPrivate("LoadWorldAt", new[] { typeof(DataContext), typeof(string) }, context, path);
            Common.SetArchiveId(archiveId);
            AddInitAchievementsHandler();
            GameData.GameDataBridge.GameDataBridge.StartNextFrame(
                () => InvokeGlobalPrivate("SetCurrGameWorldType", new[] { typeof(sbyte), typeof(DataContext) }, (sbyte)1, null));
        }

        // --- helpers ---

        private static int ReadSlot(SerializableModData parameter)
        {
            if (parameter == null || !parameter.Get(SlotKey, out int slot))
            {
                throw new ArgumentException("Missing slot.");
            }
            return slot;
        }

        private static void AddInitAchievementsHandler()
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                "InitAchievements", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, "InitAchievements");
            }
            DataModificationHandler handler = (DataModificationHandler)Delegate.CreateDelegate(
                typeof(DataModificationHandler), DomainManager.Global, method);
            DomainManager.Global.AddPostModificationHandler(new DataUid(0, 1, ulong.MaxValue), "InitAchievements", handler);
        }

        private static void InvokeGlobalPrivate(string methodName, Type[] parameterTypes, params object[] args)
        {
            MethodInfo method = DomainManager.Global.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(DomainManager.Global.GetType().FullName, methodName);
            }
            try { method.Invoke(DomainManager.Global, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
    }
}
```

- [ ] **Step 3: 确认读设置的 API 名（`GetSetting.Int`）**

`GetSetting.Int(ModIdStr, "SlotCount")` 是占位——**实现期先确认真实 API**。查参考：ForceEncounter 用的是 `TaiwuModSettings.GetBool(modId, name, default)`（见 `_decompiled` 或 ForceEncounter 源）。

Run（grep 反编译定位读设置 API）：
```
grep -rnE "TaiwuModSettings|GetModSetting|class .*ModSetting" /d/TaiwuMods/_decompiled/GameData*/*.decompiled.cs | head
```
Expected：找到一个"按 modId + 名字取 int/bool 设置"的静态方法。把 `GetSlotCount()` 里 `GetSetting.Int(...)` 换成真实调用（带默认值 `SlotStore.DefaultSlotCount`）。若后端拿不到设置（跨进程），退化为 `DefaultSlotCount` 并在 Task 3 用 config 的 `DefaultSettings` 兜底。

- [ ] **Step 4: 构建后端**

Run:
```
cd /d/TaiwuMods && dotnet build EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/EasyQuickSaveLoad.Backend.csproj -c Release -v:minimal
```
Expected: `0 个错误`。若报 `GetSetting`/`SerializableModData.Get` 等 API 不符，按反编译改正再构建。

- [ ] **Step 5: 部署 + 实机验证（EasyBridge /eval）**

关游戏 → build（上一步）→ 部署：
```
./deploy.ps1 -ModName EasyQuickSaveLoad -NoBuild
```
备份 world_1（`Copy-Item SaveGames/world_1 <backup>`），启动游戏，载入 world_1（太吾鉴忠），关掉月报弹窗。

用 `/eval` 反射直调后端方法验证（构造 `SerializableModData` 传 Slot；参考 [[project-easyquicksaveload]] 的 `/eval` 手法）：
1. 设好感哨兵 `DirectlySetFavorabilities(ctx, taiwu, someNpc, 7777, 7777)`。
2. 反射调 `BackendPlugin.SaveToSlot(ctx, param{Slot:3})` → 断言文件 `SaveGames/world_1/local.sav.qsl.slot.3` 出现。
3. 改好感为 1111。
4. 反射调 `BackendPlugin.LoadFromSlot(ctx, param{Slot:3})`（会离世界+载入）→ 等 `IsInWorld()==True` → 断言好感回到 7777。
5. 反射调 `ListSlots` → 断言 `SlotCount` 正确、slot 3 有 WorldInfo、其余为 null、`QuickInfo.WorldInfo` 为 null。
6. 反射调 `SaveToSlot(Slot:-1)`（快捷栏）→ 断言 `local.sav.qsl.quick` 出现。
7. 反射调 `DeleteSlot(Slot:3)` → 断言 slot.3 文件消失。

Expected: 全部通过、0 `Unable to call method` warn、不卡 Loading。

- [ ] **Step 6: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/SlotStore.cs EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/BackendPlugin.cs
git commit -m "EasyQuickSaveLoad: backend slot-based save model (SaveToSlot/ListSlots/LoadFromSlot/DeleteSlot)"
```

---

## Task 2: 配置项（SlotCount / QuickSaveConfirm / QuickLoadConfirm）

**Files:**
- Modify: `EasyQuickSaveLoad/config.lua`
- Modify: `EasyQuickSaveLoad/Settings.Lua`

**Interfaces:**
- Produces：模组设置项 `SlotCount:int(默认20)`、`QuickSaveConfirm:bool(默认true)`、`QuickLoadConfirm:bool(默认true)`。前端 Task 5/6 读；后端 Task 1 读 `SlotCount`。

- [ ] **Step 1: 确认设置项 Lua 结构**

查一个已有带设置项的 mod（如 DreamLover 或 ForceEncounter）的 `config.lua` `DefaultSettings` + `Settings.Lua` 格式（纯字面量）：
```
sed -n '1,80p' /d/TaiwuMods/DreamLover/config.lua
sed -n '1,80p' /d/TaiwuMods/DreamLover/Settings.Lua
```
Expected：看清 int/bool 设置项字段（Key/Name/Desc/Type/Default/Min/Max 等）的确切写法，照抄结构。

- [ ] **Step 2: 写 config.lua 的 DefaultSettings + Settings.Lua**

按上一步确认的结构，在 `config.lua` 的 `DefaultSettings` 加三项，`Settings.Lua` 加对应 UI 定义。**纯字面量**。示例（字段名以 Step 1 实测为准）：

`config.lua` 的 `DefaultSettings`：
```lua
    DefaultSettings = {
        SlotCount = 20,
        QuickSaveConfirm = true,
        QuickLoadConfirm = true,
    },
```

`Settings.Lua`（结构照 Step 1 抄；含滑条/开关定义），例如：
```lua
return {
    { Key = "SlotCount", Name = "普通存档栏位数", Type = "int", Default = 20, Min = 1, Max = 99 },
    { Key = "QuickSaveConfirm", Name = "快速存档需要确认", Type = "bool", Default = true },
    { Key = "QuickLoadConfirm", Name = "快速读档需要确认", Type = "bool", Default = true },
}
```

- [ ] **Step 3: 部署 + 验证设置出现在模组管理**

部署（无需 build，Lua 是数据）：`./deploy.ps1 -ModName EasyQuickSaveLoad -NoBuild`。启动游戏 → 主菜单 → 模组管理 → 点 EasyQuickSaveLoad 的设置面板 → 三项出现、默认值正确。用 EasyBridge UI 桥 `/ui/Mod` 检视或直接看。

Expected：三个设置项渲染出来。

- [ ] **Step 4: Commit**

```
git add EasyQuickSaveLoad/config.lua EasyQuickSaveLoad/Settings.Lua
git commit -m "EasyQuickSaveLoad: add SlotCount/QuickSaveConfirm/QuickLoadConfirm settings"
```

---

## Task 3: 前端可复用核心 EqslCore + NativeDialog

从旧 overlay 抽出要保留的逻辑到聚焦文件，先让工程编译通过（旧 overlay 仍在，暂不删）。

**Files:**
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EqslCore.cs`
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/NativeDialog.cs`
- Reference (不改，抄逻辑): `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoadOverlay.cs`

**Interfaces:**
- Produces（Task 4/5/6 依赖）：
  - `EqslCore.ModId`（string，静态；由 FrontendPlugin 注入）
  - `EqslCore.CanOperate(out string reason)`（bool）
  - `EqslCore.FormatWorldInfoBrief(WorldInfo)`（string，多行：时间/太吾/年数/地点）
  - `EqslCore.CallSave(int slot)` / `CallLoad(int slot)` / `CallDelete(int slot)`（void；按 ModId 发 mod 方法）
  - `EqslCore.RequestSlots(Action<SerializableModData> onReady)`（异步取 ListSlots）
  - `EqslCore.LoadSlotWorld(int slot)`（static；完整换档载入协程，等价旧 `LoadManualWorld`，只是传 slot 且不再传 timestamp）
  - `NativeDialog.Confirm(string title, string content, Action onYes)`（void）

- [ ] **Step 1: 写 NativeDialog.cs**

参照 `_decompiled` 里 `ViewSystemOption.OnReturnToMainMenu` 的 `DialogCmd` 用法（`Title`/`Content`/`Type`/`Yes`）：

```csharp
using System;
using FrameWork;

namespace EasyQuickSaveLoad.Frontend
{
    internal static class NativeDialog
    {
        public static void Confirm(string title, string content, Action onYes)
        {
            try
            {
                var cmd = new DialogCmd
                {
                    Title = title,
                    Content = content,
                    Type = 1,
                    Yes = () => { try { onYes?.Invoke(); } catch (Exception ex) { UnityEngine.Debug.LogError("[EasyQuickSaveLoad] dialog action failed: " + ex); } }
                };
                UIElement.Dialog.SetOnInitArgs(EasyPool.Get<ArgumentBox>().SetObject("Cmd", cmd));
                UIManager.Instance.MaskUI(UIElement.Dialog);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] Confirm dialog failed: " + ex);
            }
        }
    }
}
```

`DialogCmd` 的确切命名空间/字段用反编译确认：`grep -nE "class DialogCmd" /d/TaiwuMods/_decompiled/Assembly-CSharp/Assembly-CSharp.decompiled.cs` 并读其字段，按需修正 `Type`/字段名。

- [ ] **Step 2: 写 EqslCore.cs（迁移旧 overlay 逻辑）**

把旧 `EasyQuickSaveLoadOverlay.cs` 里以下方法**原样搬**进 `EqslCore`（改为该类的 static 成员），去掉与自绘面板/二次确认相关的部分：
- `CanOperate(out string reason, out sbyte slot)` → 简化成 `CanOperate(out string reason)`（内部仍取当前 slot 判断，只是不外传）。整段判断（未进游戏/读取中/无档位/数据未就绪/保存中/月结/载入/战斗/事件/指令/界面）照搬。
- `FormatSaveTime` / `FormatTaiwuName` / `FormatYear` / `FormatLocation` / `FormatWorldInfoBrief` 照搬。
- `LoadManualWorld(sbyte slot, long ts)` → 改名 `LoadSlotWorld(int slot)`，删掉 timestamp，`CallLoadManualSave` 内部改为发新 mod 方法 `LoadFromSlot`（参数只有 `Slot`）。`AddLoadedAllArchiveDataMonitor` 照搬。
- 新增后端调用 helper：

```csharp
public static string ModId = "EasyQuickSaveLoad";

public static void CallSave(int slot)
{
    var p = new GameData.Serializer.SerializableModData();
    p.Set("Slot", slot);
    ModDomainMethod.Call.CallModMethodWithParam(ModId, "SaveToSlot", p);
}

public static void CallDelete(int slot)
{
    var p = new GameData.Serializer.SerializableModData();
    p.Set("Slot", slot);
    ModDomainMethod.Call.CallModMethodWithParam(ModId, "DeleteSlot", p);
}

public static void RequestSlots(Action<GameData.Serializer.SerializableModData> onReady)
{
    // 抄旧 RequestManualSaves 的 AsyncCall.CallModMethodWithParamAndRet 结构，
    // 但方法名 "ListSlots"、参数为空 SerializableModData、反序列化回 SerializableModData 后回调。
}
```

`CallLoad(int)` 由 `LoadSlotWorld` 内部的 `CallLoadFromSlot` 承担（发 `LoadFromSlot`，见下）。`LoadSlotWorld` 内 `CallLoadManualSave` 等价物：设 `GlobalOperations.LoadedAllArchiveData=false` → 发 `LoadFromSlot`(Slot) → `AddLoadedAllArchiveDataMonitor()`。

> DRY 提示：`CanOperate`/格式化/载入协程直接从旧文件复制，逐字保留（含 try/catch 与本地化回退），**不要**重写。

- [ ] **Step 3: 构建前端**

Run:
```
cd /d/TaiwuMods && dotnet build EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoad.Frontend.csproj -c Release -v:minimal
```
Expected: `0 个错误`（旧 overlay 仍在、EqslCore 并存，只要签名不冲突即可）。

- [ ] **Step 4: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EqslCore.cs EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/NativeDialog.cs
git commit -m "EasyQuickSaveLoad: extract EqslCore + NativeDialog (slot-aware load, native confirm)"
```

---

## Task 4: ESC 原生按钮注入器 SystemOptionButtonInjector

克隆原生 CButton 注入 ViewSystemOption，先把点击接到临时桩（打日志），验证"原生外观 + 分组 + 置灰 + 生命周期"。

**Files:**
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs`
- Modify: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/FrontendPlugin.cs`

**Interfaces:**
- Consumes: `EqslCore.CanOperate`、`EqslCore.ModId`。
- Produces: `SystemOptionButtonInjector.Create()` / `Destroy()`（MonoBehaviour host，`DontDestroyOnLoad`）。四个点击入口方法 `OnSaveClick/OnLoadClick/OnQuickSaveClick/OnQuickLoadClick`（Task 5/6 填内容，本任务先打日志）。

- [ ] **Step 1: 实机检视 ViewSystemOption 结构（EasyBridge）**

启动游戏、进 world_1、开 ESC（用 EasyBridge 无法发 ESC 键——改用 `/static` 反射或让用户开；或用 `/inspect`）。取 `UIElement.SystemOption.UiBase` 的类型与按钮字段、父容器路径：

用 `/eval`（前端? 不，SystemOption 是前端 UI——用 `/static` 或 `/inspect`）。实操：ESC 打开后 `UI /ui/SystemOption detail=full` 列出所有 CButton 的 id 与父路径；`/inspect element=SystemOption id=<btnReturnToGame 的路径>` 看组件。记录：
- 克隆源按钮 id（原生"继续游戏" btnReturnToGame 的节点路径）
- 其父容器路径（按钮列表的 LayoutGroup）
- 标签文本子节点的组件类型（`Text` 还是 `TextMeshProUGUI`）与相对路径
- 是否有现成分隔元素可克隆

把这些写成 `SystemOptionButtonInjector` 里的常量/反射字段名。

> 反射拿字段：`viewSystemOption.GetType().GetField("btnReturnToGame", NonPublic|Instance)`（字段名来自反编译 `ViewSystemOption`，已知：btnReturnToGame/btnSystemSetting/btnManageMod/...）。用它拿 `CButton` 实例 → `.gameObject` 克隆源、`.transform.parent` 容器。

- [ ] **Step 2: 写 SystemOptionButtonInjector.cs**

MonoBehaviour，`Update` 里侦测 SystemOption 焦点、注入一次、每帧更新置灰、SystemOption 消失时清理。核心逻辑（字段名/路径用 Step 1 实测值）：

```csharp
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FrameWork;

namespace EasyQuickSaveLoad.Frontend
{
    public sealed class SystemOptionButtonInjector : MonoBehaviour
    {
        private static SystemOptionButtonInjector _instance;
        private readonly System.Collections.Generic.List<GameObject> _injected = new System.Collections.Generic.List<GameObject>();
        private CButton _save, _load, _quickSave, _quickLoad;
        private object _boundUiBase;   // 当前已注入的 ViewSystemOption 实例

        public static SystemOptionButtonInjector Create()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("EasyQuickSaveLoad.Injector");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SystemOptionButtonInjector>();
            return _instance;
        }

        public static void Destroy(SystemOptionButtonInjector i)
        {
            if (i == null) return;
            if (_instance == i) _instance = null;
            UnityEngine.Object.Destroy(i.gameObject);
        }

        private void Update()
        {
            object uiBase = GetSystemOptionUiBaseIfFocused();
            if (uiBase == null)
            {
                if (_boundUiBase != null) Cleanup();
                return;
            }
            if (!ReferenceEquals(uiBase, _boundUiBase))
            {
                Cleanup();
                try { Inject(uiBase); _boundUiBase = uiBase; }
                catch (Exception ex) { Debug.LogWarning("[EasyQuickSaveLoad] inject failed: " + ex); }
            }
            UpdateInteractable();
        }

        private static object GetSystemOptionUiBaseIfFocused()
        {
            try
            {
                if (GameApp.Instance == null || GameApp.Instance.GetCurrentGameStateName() != EGameState.InGame) return null;
                if (UIManager.Instance == null || !UIElement.SystemOption.Exist || UIElement.SystemOption.UiBase == null) return null;
                if (!UIManager.Instance.IsFocusElement(UIElement.SystemOption)) return null;
                return UIElement.SystemOption.UiBase;
            }
            catch { return null; }
        }

        private void Inject(object viewSystemOption)
        {
            var t = viewSystemOption.GetType();
            var srcField = t.GetField("btnReturnToGame", BindingFlags.Instance | BindingFlags.NonPublic);
            var srcButton = srcField?.GetValue(viewSystemOption) as CButton;
            if (srcButton == null) throw new Exception("clone source btnReturnToGame not found");
            var parent = srcButton.transform.parent;

            // 分隔（可选，Step 1 若找到分隔元素则克隆之，否则跳过）
            _save = CloneButton(srcButton, parent, "存档", OnSaveClick);
            _load = CloneButton(srcButton, parent, "读档", OnLoadClick);
            _quickSave = CloneButton(srcButton, parent, "快速存档", OnQuickSaveClick);
            _quickLoad = CloneButton(srcButton, parent, "快速读档", OnQuickLoadClick);
            // 摆到一组：紧跟克隆源之后（自成一组）。SetSiblingIndex 依 Step 1 决定组位置。
            int baseIndex = srcButton.transform.GetSiblingIndex();
            _save.transform.SetSiblingIndex(baseIndex + 1);
            _load.transform.SetSiblingIndex(baseIndex + 2);
            _quickSave.transform.SetSiblingIndex(baseIndex + 3);
            _quickLoad.transform.SetSiblingIndex(baseIndex + 4);
        }

        private CButton CloneButton(CButton src, Transform parent, string label, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
            go.name = "EQSL_" + label;
            _injected.Add(go);
            // 设标签：找子级文本组件（Step 1 确定是 Text 还是 TMP）
            var txt = go.GetComponentInChildren<Text>(true);
            if (txt != null) txt.text = label;
            var btn = go.GetComponent<CButton>();
            btn.ClearAndAddListener(onClick);
            return btn;
        }

        private void UpdateInteractable()
        {
            bool ok = EqslCore.CanOperate(out _);
            if (_save != null) _save.interactable = ok;
            if (_load != null) _load.interactable = ok;
            if (_quickSave != null) _quickSave.interactable = ok;
            if (_quickLoad != null) _quickLoad.interactable = ok;
        }

        private void Cleanup()
        {
            foreach (var go in _injected) { if (go != null) UnityEngine.Object.Destroy(go); }
            _injected.Clear();
            _save = _load = _quickSave = _quickLoad = null;
            _boundUiBase = null;
        }

        // 点击入口——Task 5/6 填实现，本任务先打日志：
        private void OnSaveClick() { Debug.Log("[EasyQuickSaveLoad] Save clicked"); }
        private void OnLoadClick() { Debug.Log("[EasyQuickSaveLoad] Load clicked"); }
        private void OnQuickSaveClick() { Debug.Log("[EasyQuickSaveLoad] QuickSave clicked"); }
        private void OnQuickLoadClick() { Debug.Log("[EasyQuickSaveLoad] QuickLoad clicked"); }
    }
}
```

- [ ] **Step 3: 改 FrontendPlugin.cs 用 injector 替换旧 overlay**

`Initialize` 里：`EqslCore.ModId = ModIdStr;`（替换旧 `EasyQuickSaveLoadOverlay.SetModId`），`_injector = SystemOptionButtonInjector.Create();`（替换 `EasyQuickSaveLoadOverlay.Create()`）。`Dispose` 里 `SystemOptionButtonInjector.Destroy(_injector)`。旧 overlay 类此时可从工程移除引用（文件 Task 7 删）。为避免两套 UI 并存，本步就把 `EasyQuickSaveLoadOverlay.Create()` 调用去掉。

- [ ] **Step 4: 构建**

Run: `dotnet build EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoad.Frontend.csproj -c Release -v:minimal`
Expected: `0 个错误`。

- [ ] **Step 5: 部署 + 实机验证**

关游戏→build→部署→启动→进 world_1→开 ESC。EasyBridge `/ui/SystemOption detail=full` 验证：
- 出现 4 个新按钮，label 为 存档/读档/快速存档/快速读档，节点是克隆的原生 CButton（同 prefab 结构）。
- 位置成组、紧邻。
- 点击任一按钮（`/action` click），Player.log 出现对应 `clicked` 日志。
- 制造不可操作态（如载入中）时按钮 `interactable=false`（`/reflect` 读 `interactable`）。

Expected：外观原生、点击有日志、置灰生效。若克隆源字段/文本组件类型不符，回 Step 1 修正常量。

- [ ] **Step 6: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/FrontendPlugin.cs
git commit -m "EasyQuickSaveLoad: inject native cloned CButtons into ESC SystemOption"
```

---

## Task 5: 快速存档/读档接入（快捷栏 + 可配确认）

**Files:**
- Modify: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs`
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EqslSettings.cs`

**Interfaces:**
- Consumes: `EqslCore.CallSave(-1)`、`EqslCore.LoadSlotWorld(-1)`、`NativeDialog.Confirm`、`EqslCore.FormatWorldInfoBrief`、`EqslCore.RequestSlots`。
- Produces: `EqslSettings.QuickSaveConfirm` / `QuickLoadConfirm`（bool，读模组设置）。

- [ ] **Step 1: 写 EqslSettings.cs（前端读设置）**

前端读模组设置的 API 以实测为准（Step：`grep -nE "GetModSetting|ModSetting|Settings" 前端可用类`）。骨架：

```csharp
namespace EasyQuickSaveLoad.Frontend
{
    internal static class EqslSettings
    {
        public static bool QuickSaveConfirm => GetBool("QuickSaveConfirm", true);
        public static bool QuickLoadConfirm => GetBool("QuickLoadConfirm", true);
        public static int SlotCount => GetInt("SlotCount", 20);

        private static bool GetBool(string key, bool def)
        {
            try { /* 真实读设置 API，按 EqslCore.ModId + key */ return def; }
            catch { return def; }
        }
        private static int GetInt(string key, int def)
        {
            try { /* 真实读设置 API */ return def; }
            catch { return def; }
        }
    }
}
```
实现期把 `/* 真实读设置 API */` 换成实测调用（前端设置读取；若前端读不到，从 config 默认值或后端 ListSlots 返回的 SlotCount 拿——ListSlots 已回传 SlotCount）。

- [ ] **Step 2: 填 OnQuickSaveClick / OnQuickLoadClick**

```csharp
private void OnQuickSaveClick()
{
    if (!EqslCore.CanOperate(out string reason)) return;
    Action doSave = () => EqslCore.CallSave(SlotStoreQuick);
    if (EqslSettings.QuickSaveConfirm)
    {
        EqslCore.RequestSlots(res =>
        {
            string summary = BuildQuickSummary(res);   // 快捷栏现有摘要，无则"（空）"
            NativeDialog.Confirm("确认快速存档", "将覆盖快捷存档\n" + summary, doSave);
        });
    }
    else { doSave(); }
}

private void OnQuickLoadClick()
{
    if (!EqslCore.CanOperate(out string reason)) return;
    EqslCore.RequestSlots(res =>
    {
        var quickInfo = ExtractQuickInfo(res);          // QuickInfo.WorldInfo
        if (quickInfo == null) return;                  // 快捷栏为空，不可读
        Action doLoad = () => { HideSystemOption(); EqslCore.LoadSlotWorld(SlotStoreQuick); };
        if (EqslSettings.QuickLoadConfirm)
            NativeDialog.Confirm("确认快速读档", EqslCore.FormatWorldInfoBrief(quickInfo), doLoad);
        else doLoad();
    });
}
```
`SlotStoreQuick = -1` 常量；`BuildQuickSummary`/`ExtractQuickInfo`/`HideSystemOption` 小 helper（`HideSystemOption` 抄旧 overlay 的 `HideSystemOption`）。`ExtractQuickInfo` 从 ListSlots 结果取 `QuickInfo` 的 ArchiveInfo.WorldInfo。

- [ ] **Step 3: 构建 + 实机验证**

关游戏→build→部署→启动→world_1→ESC。设好感哨兵→点"快速存档"→（确认开时）弹原生 Dialog→确认→`local.sav.qsl.quick` 出现。改好感→点"快速读档"→确认→好感回哨兵值、回地图、0 warn。到模组设置关掉"快速读档需要确认"→重启→快速读档不弹框直接读。

Expected：快捷栏存/读正确、确认开关生效。

- [ ] **Step 4: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EqslSettings.cs
git commit -m "EasyQuickSaveLoad: wire quick save/load to quick slot with configurable confirm"
```

---

## Task 6: 自建档位面板 SlotPanel（存/读/删）

**Files:**
- Create: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SlotPanel.cs`
- Modify: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs`（OnSaveClick/OnLoadClick 打开面板）

**Interfaces:**
- Consumes: `EqslCore.RequestSlots/CallSave/CallDelete/LoadSlotWorld/FormatWorldInfoBrief`、`NativeDialog.Confirm`、`EqslSettings.SlotCount`。
- Produces: `SlotPanel.Open(bool saveMode)`（静态入口）。

- [ ] **Step 1: 实机检视可克隆的格/行 prefab**

用 EasyBridge 检视候选克隆源，选一个做档位格：
- RevertArchive 列表行：`/ui/RevertArchive detail=full`（进入需先有回溯数据；或看反编译 prefab）。
- 或用 Task 4 的克隆 CButton + 附 Text 自己拼格。
记录选定源的节点路径与文本子节点，写成常量。**倾向**：用一个 ScrollRect + 克隆的行（每行=一个可点按钮 + 摘要文本 + 删除小按钮）。若找不到理想行 prefab，用克隆 CButton 拼最小可用格（不追求华丽，但用原生按钮底以保持风格）。

- [ ] **Step 2: 写 SlotPanel.cs**

MonoBehaviour 面板：`Open(bool saveMode)` → 建一个遮罩容器 + ScrollRect + 每栏一行；`RequestSlots` 回来后按 `SlotCount` 渲染行。行点击行为按 saveMode：
- saveMode 且空栏 → `EqslCore.CallSave(i)` → 关面板/刷新。
- saveMode 且占用 → `NativeDialog.Confirm("确认覆盖", 摘要, () => CallSave(i))`。
- 读 mode 且占用 → `NativeDialog.Confirm("确认读档", 摘要, () => { Close(); HideSystemOption(); EqslCore.LoadSlotWorld(i); })`。
- 读 mode 且空 → 不可点。
- 每行删除按钮 → `NativeDialog.Confirm("确认删除", 摘要, () => { CallDelete(i); Refresh(); })`。

行摘要用 `EqslCore.FormatWorldInfoBrief(slotWorldInfo)`；空栏显示"空"。数据从 ListSlots 的 `SlotInfos.BackupWorldsInfo`（第 i 项 = 槽 i 的 WorldInfo 或 null）。面板关闭：点空白/关闭按钮 Destroy 容器。

> 面板尽量克隆原生元素拼（Step 1 选定源）；布局用 `VerticalLayoutGroup`/`ScrollRect`（Unity 内置，风格来自克隆的行）。

- [ ] **Step 3: 接 OnSaveClick / OnLoadClick**

```csharp
private void OnSaveClick() { if (EqslCore.CanOperate(out _)) SlotPanel.Open(saveMode: true); }
private void OnLoadClick() { if (EqslCore.CanOperate(out _)) SlotPanel.Open(saveMode: false); }
```

- [ ] **Step 4: 构建 + 实机验证（完整回归）**

关游戏→build→部署→启动→world_1→ESC：
1. 点"存档"→面板出现，`SlotCount` 个栏、空栏显"空"。设好感哨兵→存到栏 3→栏 3 显摘要。
2. 覆盖占用栏 3→弹确认→覆盖成功。
3. 点"读档"→面板→读栏 3→好感回哨兵、回地图、0 warn、不卡 Loading。
4. 删栏 3→确认→栏 3 变"空"。
5. 快速存/读档仍正常（Task 5 回归）。

Expected：面板存/读/删全通、外观用原生底不突兀。

- [ ] **Step 5: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SlotPanel.cs EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/SystemOptionButtonInjector.cs
git commit -m "EasyQuickSaveLoad: self-built slot panel (save/load/delete) via native-cloned elements"
```

---

## Task 7: 删除旧 overlay + 清理 + 更新文档

**Files:**
- Delete: `EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoadOverlay.cs`
- Modify: `EasyQuickSaveLoad/README.md`
- Modify: `EasyQuickSaveLoad/config.lua`（Version bump）

**Interfaces:** 无新增。

- [ ] **Step 1: 删除旧 overlay 文件**

确认 `EasyQuickSaveLoadOverlay` 已无引用（FrontendPlugin 已换成 injector；所有复用逻辑已搬进 EqslCore）：
```
grep -rn "EasyQuickSaveLoadOverlay" /d/TaiwuMods/EasyQuickSaveLoad/
```
Expected：除文件本身无其他引用。删除该 .cs。

- [ ] **Step 2: 更新 README.md + config Version**

README 改写为 galgame 栏位模型描述（快捷栏 + N 普通栏、原生 ESC 按钮、原生确认、设置项）。`config.lua` `Version` 升到 `1.0.1.0`。

- [ ] **Step 3: 构建全量**

```
dotnet build EasyQuickSaveLoad/EasyQuickSaveLoad.Backend/EasyQuickSaveLoad.Backend.csproj -c Release -v:minimal
dotnet build EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoad.Frontend.csproj -c Release -v:minimal
```
Expected：两个 `0 个错误`。

- [ ] **Step 4: Commit**

```
git add EasyQuickSaveLoad/EasyQuickSaveLoad.Frontend/EasyQuickSaveLoadOverlay.cs EasyQuickSaveLoad/README.md EasyQuickSaveLoad/config.lua
git commit -m "EasyQuickSaveLoad: remove legacy overlay; update README/version"
```

---

## Task 8: 端到端验收 + 存档还原

**Files:** 无（仅验证 + 收尾）。

- [ ] **Step 1: 全流程实机验收（world_1）**

按 spec §测试/验收全跑一遍（EasyBridge），逐条勾：ESC 原生按钮成组+分隔+置灰；存/读/删普通栏；快速存/读快捷栏；确认开关；载入不卡 Loading、0 `Unable to call method` warn。用好感哨兵验证每次读档回归。

- [ ] **Step 2: 关游戏 + 还原存档 + 校验**

关游戏 → 删 `SaveGames/world_1` 里测试栏文件（`local.sav.qsl.quick` / `local.sav.qsl.slot.*`）→ 从备份还原 `local.sav`（若测试期未 quicksave 主档则本就未变）→ `Get-FileHash` 对照备份，断言 `local.sav` 不变。

- [ ] **Step 3: 记忆更新**

更新 [[project-easyquicksaveload]]：栏位模型、原生 ESC 嵌入、克隆源字段/路径、面板克隆源、实测的读设置 API、任何新踩坑。

---

## Self-Review

- **Spec coverage**：① 后端栏位 → Task 1；② ESC 原生按钮 → Task 4；③ 档位面板 → Task 6；④ 原生 Dialog → Task 3（NativeDialog）+ Task 5/6 用；⑤ 配置项 → Task 2 + Task 5(读)；⑥ 健壮性 → Task 4(反射降级)贯穿；快捷栏独立不进面板 → Task 5(独立按钮) + Task 6(面板只渲染普通栏)；删除旧东西 → Task 7；复用 → Task 3。全覆盖。
- **Placeholder 说明**：前端 UI 任务（4/6）含"实机检视"步骤产出确切字段名/路径/克隆源——这是本逆向 mod **必需的可执行发现步骤**，非占位。后端/config/核心逻辑均给完整代码。读设置 API（Task 1 Step3 / Task 5 Step1）标注为"实测确认"，因跨前后端设置读取 API 名需以反编译为准。
- **Type 一致性**：参数键 `Slot`/`Ok`/`QuickInfo`/`SlotInfos`/`SlotCount` 前后端一致；快捷栏 `-1` 常量 `SlotStore.QuickSlot`/前端 `SlotStoreQuick` 同值；方法名 `SaveToSlot`/`LoadFromSlot`/`DeleteSlot`/`ListSlots` 全文件一致。
