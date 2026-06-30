# 观音识蛐蛐 高品盲盒 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 CricketSingGradeColor（观音识蛐蛐）加「高品盲盒」：声音品级 ≥ 门槛时，仅在捕蛐蛐小游戏里用盲盒色替换真实品级色，保留抓捕悬念。

**Architecture:** 新增 Unity-free 纯逻辑 `BlindBox`（可单测）决定「展示品级 / 还原白色」；`BlindBoxConfig` 持有运行期配置 + 场次盐；`SessionSeedPatch` 在 `ViewCatchCricket.InitCatchPlace` Postfix 里把本场 21 个草丛蛐蛐散列成 `SessionSeed`；`RippleColorizer.ColorizeForCatch` 走盲盒路径（卡片视图共享的 `Colorize` 不变）。配置走游戏原生分组下拉/开关。

**Tech Stack:** C# net48（前端 Harmony 补丁，引用 Unity / Assembly-CSharp）；net8 测试工程以 `<Compile Link>` 链接纯逻辑源码做执行级单测 + 源码文本契约断言；HarmonyLib；Lua 配置。

## Global Constraints

- 盲盒伪装**仅作用于捕蛐蛐小游戏**（`ViewCatchCricket`）；卡片视图（`CricketView`/`CricketViewNew`，斗蛐蛐/收藏罐/图鉴）显示真实计算色，零改动。
- 品级 index：`0=九品(最低) … 8=一品(最高)`，与游戏调色板下标一致。
- 下拉与默认值用 **0-based index**（与现有 `BlendMode`：`config.lua` `DefaultValue` 与代码 index 直接对应）。
- 新增配置默认值：`EnableBlindBox=false`、`BlindBoxThreshold=7`(二品)、`BlindBoxMode=2`(固定一品)。
- 版本号：`config.lua` `Version` 与 `FrontendPlugin` `[PluginConfig(...)]` 必须一致，本次 `1.0.0.0` → `1.1.0.0`。
- 纯逻辑文件 `BlindBox.cs` **不得 `using UnityEngine`**（要链接进 net8 测试工程编译执行）。
- 新增 `[HarmonyPatch]` 必须用 `[HarmonyPatch(typeof(X), "Method")]` 形式（无 `Type[]` 实参），否则契约测试的清单正则匹配不到；并在 `ModBuild/harmony-targets.json` 同步声明。
- 设置变更即时生效，无需重启（`NeedRestartWhenSettingChanged = false` 保持）。
- 测试命令：`dotnet run --project tests/TaiwuMods.Tests`（期望末行 `All Taiwu mod contract tests passed.`）。

---

### Task 1: BlindBox 纯逻辑（含执行级单测）

**Files:**
- Create: `CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBox.cs`
- Modify: `tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj`（加一行 `<Compile Link>`）
- Modify: `tests/TaiwuMods.Tests/Program.cs`（`PureModRules` 增加 `CricketBlindBoxRules()`）

**Interfaces:**
- Produces:
  - `enum CricketSingGradeColor.Frontend.EBlindBoxMode { RandomLow=0, RandomHigh=1, FixedTop=2, FixedBottom=3, FixedThreshold=4, VanillaWhite=5 }`
  - `static int BlindBox.ResolveDisplayGrade(int trueGrade, int singPitch, int singSize, bool enabled, int threshold, EBlindBoxMode mode, int sessionSeed, int gradeCount)` — 返回展示品级 index；`-1` 表示「还原原版白色」。
  - `static int BlindBox.ComputeSessionSeed(int[] colorIds, int[] partsIds)` — 场次盐散列。

- [ ] **Step 1: 写失败的单测**

在 `tests/TaiwuMods.Tests/Program.cs` 顶部、现有 `using` 行之后（top-level 语句之前）加一个类型别名：

```csharp
using M = CricketSingGradeColor.Frontend.EBlindBoxMode;
```

> 别名必须在文件作用域（不能写在方法体内）。`Program.cs` 顶部已有 `using System.Text.Json;` 等，把这行加在它们后面、`var repo = FindRepoRoot();` 之前。

在 `PureModRules()` 方法体末尾，加一行调用：

```csharp
        DreamLoverRules();
        CricketBlindBoxRules();
```

并在 `DreamLoverRules()` 方法之后（同为 `private static void`）新增方法：

```csharp
    private static void CricketBlindBoxRules()
    {
        int Resolve(int trueGrade, int threshold, M mode, bool enabled = true, int seed = 12345, int gc = 9, int pitch = 3, int size = 40)
            => CricketSingGradeColor.Frontend.BlindBox.ResolveDisplayGrade(trueGrade, pitch, size, enabled, threshold, mode, seed, gc);

        // 未启用 → 显示真实品级
        Assert(Resolve(8, 7, M.FixedTop, enabled: false) == 8, "BlindBox disabled should show the true grade");
        // 真实品级低于门槛 → 显示真实品级
        Assert(Resolve(5, 7, M.FixedTop) == 5, "BlindBox below threshold should show the true grade");
        // 固定一品 / 九品 / 门槛 / 白色
        Assert(Resolve(7, 7, M.FixedTop) == 8, "FixedTop should display the top grade");
        Assert(Resolve(8, 7, M.FixedBottom) == 0, "FixedBottom should display the bottom grade");
        Assert(Resolve(8, 7, M.FixedThreshold) == 7, "FixedThreshold should display the threshold grade");
        Assert(Resolve(8, 7, M.VanillaWhite) == -1, "VanillaWhite should signal a reset to vanilla white");
        // 随机高在 [门槛, 8]
        int hi = Resolve(8, 7, M.RandomHigh);
        Assert(hi >= 7 && hi <= 8, "RandomHigh should pick within the high partition");
        // 随机低在 [0, 门槛-1]
        int lo = Resolve(8, 7, M.RandomLow);
        Assert(lo >= 0 && lo <= 6, "RandomLow should pick within the low partition");
        // 确定性：相同输入相同输出
        Assert(Resolve(8, 7, M.RandomLow, seed: 999) == Resolve(8, 7, M.RandomLow, seed: 999), "Random pick should be deterministic for the same seed");
        // 盐不同结果可变（低区跨度大，至少两种取值）
        var seen = new HashSet<int>();
        for (int s = 0; s < 16; s++) seen.Add(Resolve(8, 7, M.RandomLow, seed: s));
        Assert(seen.Count > 1, "Random pick should vary across session seeds");
        // 空低区（门槛=九品）回退 grade 0
        Assert(Resolve(8, 0, M.RandomLow) == 0, "RandomLow with empty low partition should fall back to grade 0");
        // 场次盐：相同输入相同、不同输入不同
        int seedA = CricketSingGradeColor.Frontend.BlindBox.ComputeSessionSeed(new[] { 1, 2, 3 }, new[] { 0, 0, 5 });
        int seedB = CricketSingGradeColor.Frontend.BlindBox.ComputeSessionSeed(new[] { 1, 2, 3 }, new[] { 0, 0, 5 });
        int seedC = CricketSingGradeColor.Frontend.BlindBox.ComputeSessionSeed(new[] { 1, 2, 4 }, new[] { 0, 0, 5 });
        Assert(seedA == seedB, "ComputeSessionSeed should be deterministic for identical compositions");
        Assert(seedA != seedC, "ComputeSessionSeed should differ for different compositions");
    }
```

> 注：`using M = ...;` 别名语句在方法体内合法（C# 12 / LangVersion latest）。`HashSet<int>` 在 `ImplicitUsings` 下已可用。

- [ ] **Step 2: 链接源码进测试工程**

在 `tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj` 的 `<ItemGroup>`（现有链接列表）末尾加一行：

```xml
    <Compile Include="..\..\CricketSingGradeColor\CricketSingGradeColor.Frontend\BlindBox.cs" Link="Rules\BlindBox.cs" />
```

- [ ] **Step 3: 运行测试确认失败**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: 编译失败（`BlindBox` 类型不存在）或断言异常 —— 因为 `BlindBox.cs` 尚未创建。

- [ ] **Step 4: 写最小实现**

创建 `CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBox.cs`：

```csharp
using System;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 高品盲盒形式。index 与 config.lua 的 BlindBoxMode 下拉项一一对应（0-based）。
    /// </summary>
    internal enum EBlindBoxMode
    {
        RandomLow = 0,       // 随机展示为低品级
        RandomHigh = 1,      // 随机展示为高品级
        FixedTop = 2,        // 固定一品
        FixedBottom = 3,     // 固定九品
        FixedThreshold = 4,  // 固定门槛品级
        VanillaWhite = 5,    // 固定默认白色
    }

    /// <summary>
    /// 高品盲盒纯逻辑：给定真实声音品级与配置，算出「展示用品级」或「还原原版白色」。
    /// 不依赖 Unity，便于被测试工程以源码链接方式直接执行。
    /// 品级 index：0=九品(最低) .. gradeCount-1=一品(最高)。
    /// </summary>
    internal static class BlindBox
    {
        /// <summary>展示用品级 index；返回 -1 表示「还原原版白色」。</summary>
        public static int ResolveDisplayGrade(
            int trueGrade, int singPitch, int singSize,
            bool enabled, int threshold, EBlindBoxMode mode,
            int sessionSeed, int gradeCount)
        {
            if (gradeCount <= 0)
            {
                return trueGrade;
            }

            int maxGrade = gradeCount - 1;
            threshold = Clamp(threshold, 0, maxGrade);

            // 未启用 或 真实品级低于门槛 → 显示真实品级（现有行为）
            if (!enabled || trueGrade < threshold)
            {
                return Clamp(trueGrade, 0, maxGrade);
            }

            switch (mode)
            {
                case EBlindBoxMode.VanillaWhite:
                    return -1;
                case EBlindBoxMode.FixedTop:
                    return maxGrade;
                case EBlindBoxMode.FixedBottom:
                    return 0;
                case EBlindBoxMode.FixedThreshold:
                    return threshold;
                case EBlindBoxMode.RandomLow:
                    return PickInRange(0, threshold - 1, Seed(singPitch, singSize, sessionSeed));
                case EBlindBoxMode.RandomHigh:
                    return PickInRange(threshold, maxGrade, Seed(singPitch, singSize, sessionSeed));
                default:
                    return Clamp(trueGrade, 0, maxGrade);
            }
        }

        /// <summary>把本场全部草丛蛐蛐(颜色/部件 id，按位置顺序)散列成场次盐。</summary>
        public static int ComputeSessionSeed(int[] colorIds, int[] partsIds)
        {
            unchecked
            {
                int h = 17;
                int n = (colorIds != null && partsIds != null)
                    ? Math.Min(colorIds.Length, partsIds.Length)
                    : 0;
                for (int i = 0; i < n; i++)
                {
                    h = h * 31 + colorIds[i];
                    h = h * 31 + partsIds[i];
                }

                return h;
            }
        }

        private static int Seed(int a, int b, int c)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                h = h * 31 + c;
                return h;
            }
        }

        /// <summary>[lo,hi] 内由 seed 确定性取一个；区间为空(hi&lt;lo)回退 lo(不小于 0)。</summary>
        private static int PickInRange(int lo, int hi, int seed)
        {
            if (hi < lo)
            {
                return lo < 0 ? 0 : lo;
            }

            int range = hi - lo + 1;
            int m = ((seed % range) + range) % range;
            return lo + m;
        }

        private static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : (v > hi ? hi : v);
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS，末行 `All Taiwu mod contract tests passed.`

- [ ] **Step 6: 提交**

```bash
git add CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBox.cs tests/TaiwuMods.Tests/TaiwuMods.Tests.csproj tests/TaiwuMods.Tests/Program.cs
git commit -m "CricketSingGradeColor: add unit-tested blind-box pure logic"
```

---

### Task 2: BlindBoxConfig + SoundGradeCalculator 拆分

**Files:**
- Create: `CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBoxConfig.cs`
- Modify: `CricketSingGradeColor/CricketSingGradeColor.Frontend/SoundGradeCalculator.cs`

**Interfaces:**
- Consumes: `EBlindBoxMode`（Task 1）。
- Produces:
  - `static class BlindBoxConfig { static bool Enabled; static int ThresholdGrade; static EBlindBoxMode Mode; static int SessionSeed; }`
  - `static bool SoundGradeCalculator.TryGetGrade(int singPitch, int singSize, out int grade)`
  - `static bool SoundGradeCalculator.TryGetColorForGrade(int grade, out Color color)`
  - `static int SoundGradeCalculator.GradeCount { get; }`
  - `TryGetTierColor` 行为不变（卡片视图继续用）。

- [ ] **Step 1: 创建 BlindBoxConfig**

创建 `CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBoxConfig.cs`：

```csharp
namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 高品盲盒运行期配置 + 场次盐。由 FrontendPlugin 从游戏设置写入，由 RippleColorizer/SessionSeedPatch 读取。
    /// 默认值对应 config.lua：未启用 / 门槛二品(7) / 固定一品。
    /// </summary>
    internal static class BlindBoxConfig
    {
        public static bool Enabled = false;
        public static int ThresholdGrade = 7;            // 0=九品 .. 8=一品；7=二品
        public static EBlindBoxMode Mode = EBlindBoxMode.FixedTop;
        public static int SessionSeed = 0;               // 本场草丛蛐蛐组成的散列盐
    }
}
```

- [ ] **Step 2: 拆分 SoundGradeCalculator 的 grade/color 取值**

在 `SoundGradeCalculator.cs` 中，把现有方法

```csharp
        public static bool TryGetTierColor(int singPitch, int singSize, out Color color)
        {
            EnsureCalibrated();
            color = Color.white;
            if (!_calibrated)
            {
                return false;
            }

            int grade = NearestGrade(Score(singPitch, singSize));
            color = _palette[Mathf.Clamp(grade, 0, _palette.Length - 1)];
            return true;
        }
```

替换为：

```csharp
        public static bool TryGetTierColor(int singPitch, int singSize, out Color color)
        {
            color = Color.white;
            if (!TryGetGrade(singPitch, singSize, out int grade))
            {
                return false;
            }

            return TryGetColorForGrade(grade, out color);
        }

        /// <summary>声音 (音高,圈大小) 映射到的「声音品级」index（品级锚定，最近锚点）。</summary>
        public static bool TryGetGrade(int singPitch, int singSize, out int grade)
        {
            EnsureCalibrated();
            grade = 0;
            if (!_calibrated)
            {
                return false;
            }

            grade = NearestGrade(Score(singPitch, singSize));
            return true;
        }

        /// <summary>品级 index 取调色板颜色（clamp 到合法范围）。</summary>
        public static bool TryGetColorForGrade(int grade, out Color color)
        {
            EnsureCalibrated();
            color = Color.white;
            if (!_calibrated || _palette == null || _palette.Length == 0)
            {
                return false;
            }

            color = _palette[Mathf.Clamp(grade, 0, _palette.Length - 1)];
            return true;
        }

        /// <summary>已校准调色板的品级档数（一般为 9）；未校准成功时为 0。</summary>
        public static int GradeCount
        {
            get
            {
                EnsureCalibrated();
                return (_calibrated && _palette != null) ? _palette.Length : 0;
            }
        }
```

> 仅重排取值路径；`Score` / `NearestGrade` / `_palette` / 校准逻辑、`scores[scores.Count / 2]`、`Colors.Instance.GradeColors`、`FallbackTierColors` 全部保留，契约断言不受影响。

- [ ] **Step 3: 运行测试确认通过**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS（契约测试读源码文本，重构后所有现存断言仍命中）。

- [ ] **Step 4: 提交**

```bash
git add CricketSingGradeColor/CricketSingGradeColor.Frontend/BlindBoxConfig.cs CricketSingGradeColor/CricketSingGradeColor.Frontend/SoundGradeCalculator.cs
git commit -m "CricketSingGradeColor: expose grade/color split + blind-box config holder"
```

---

### Task 3: RippleColorizer 盲盒染色路径 + 切换捕捉补丁

**Files:**
- Modify: `CricketSingGradeColor/CricketSingGradeColor.Frontend/RippleColorizer.cs`
- Modify: `CricketSingGradeColor/CricketSingGradeColor.Frontend/CricketSingColorPatch.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`（更新捕捉补丁契约断言）

**Interfaces:**
- Consumes: `BlindBox.ResolveDisplayGrade`、`BlindBoxConfig.*`、`SoundGradeCalculator.TryGetGrade/TryGetColorForGrade/GradeCount`。
- Produces:
  - `static void RippleColorizer.ColorizeForCatch(Graphic image, int singPitch, int singSize)`
  - `static void RippleColorizer.ResetToVanilla(Graphic image)`
  - `RippleColorizer.Colorize` 行为不变（内部抽出 `ApplyColor`）。

- [ ] **Step 1: 先更新契约断言（会失败）**

在 `tests/TaiwuMods.Tests/Program.cs` 的 `CricketSingGradeColorContract()` 中，把这一行（`Program.cs:564`，原文逐字）：

```csharp
        Assert(catchPatch.Contains("RippleColorizer.Colorize(image, place.SingPitch, place.SingSize)", StringComparison.Ordinal), "CricketSingGradeColor catch patch should color only from SingPitch and SingSize");
```

替换为：

```csharp
        Assert(catchPatch.Contains("RippleColorizer.ColorizeForCatch(image, place.SingPitch, place.SingSize)", StringComparison.Ordinal), "CricketSingGradeColor catch patch should route through the blind-box colorizer");
        Assert(colorizer.Contains("BlindBox.ResolveDisplayGrade", StringComparison.Ordinal), "CricketSingGradeColor catch colorizer should consult the blind-box logic");
        Assert(colorizer.Contains("ResetToVanilla", StringComparison.Ordinal), "CricketSingGradeColor should be able to restore the vanilla white ripple");
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: FAIL，报 `CricketSingGradeColor catch patch should route through the blind-box colorizer`（源码尚未改）。

- [ ] **Step 3: 实现 RippleColorizer 盲盒路径**

把 `RippleColorizer.cs` 的 `Colorize` 方法替换为下面三个方法（`Vivify` / `ApplyOutline` 保留不动）：

```csharp
        /// <summary>卡片视图用：按真实声音品级染色（不经盲盒）。</summary>
        public static void Colorize(Graphic image, int singPitch, int singSize)
        {
            if (image == null)
            {
                return;
            }

            if (!SoundGradeCalculator.TryGetTierColor(singPitch, singSize, out Color tier))
            {
                return;
            }

            ApplyColor(image, tier);
        }

        /// <summary>捕蛐蛐小游戏用：高品时按盲盒形式替换显示色，否则同真实品级。</summary>
        public static void ColorizeForCatch(Graphic image, int singPitch, int singSize)
        {
            if (image == null)
            {
                return;
            }

            if (!SoundGradeCalculator.TryGetGrade(singPitch, singSize, out int trueGrade))
            {
                return; // 校准失败：保持原版白
            }

            int displayGrade = BlindBox.ResolveDisplayGrade(
                trueGrade, singPitch, singSize,
                BlindBoxConfig.Enabled, BlindBoxConfig.ThresholdGrade, BlindBoxConfig.Mode,
                BlindBoxConfig.SessionSeed, SoundGradeCalculator.GradeCount);

            if (displayGrade < 0)
            {
                ResetToVanilla(image); // 固定默认白色
                return;
            }

            if (!SoundGradeCalculator.TryGetColorForGrade(displayGrade, out Color tier))
            {
                return;
            }

            ApplyColor(image, tier);
        }

        private static void ApplyColor(Graphic image, Color tier)
        {
            Color vivid = Vivify(tier);
            Color current = image.color;
            image.color = new Color(vivid.r, vivid.g, vivid.b, current.a);
            ApplyOutline(image, vivid);
        }

        /// <summary>还原原版白色涟漪：RGB→白、清掉我们加宽的描边、保留当前 alpha 不打断淡出。</summary>
        public static void ResetToVanilla(Graphic image)
        {
            if (image == null)
            {
                return;
            }

            Color current = image.color;
            image.color = new Color(1f, 1f, 1f, current.a);
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = Vector2.zero;
            }
        }
```

> `ApplyColor` 里保留 `new Color(vivid.r, vivid.g, vivid.b, current.a)`，`ApplyOutline` 里保留 `GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>()`，对应现存契约断言。

- [ ] **Step 4: 切换捕捉补丁调用**

在 `CricketSingColorPatch.cs` 的 `Apply` 方法里，把

```csharp
            RippleColorizer.Colorize(image, place.SingPitch, place.SingSize);
```

改为

```csharp
            RippleColorizer.ColorizeForCatch(image, place.SingPitch, place.SingSize);
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS。

- [ ] **Step 6: 提交**

```bash
git add CricketSingGradeColor/CricketSingGradeColor.Frontend/RippleColorizer.cs CricketSingGradeColor/CricketSingGradeColor.Frontend/CricketSingColorPatch.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "CricketSingGradeColor: route catch ripple through blind-box colorizer"
```

---

### Task 4: 场次盐补丁（InitCatchPlace Postfix）+ Harmony 清单

**Files:**
- Create: `CricketSingGradeColor/CricketSingGradeColor.Frontend/SessionSeedPatch.cs`
- Modify: `ModBuild/harmony-targets.json`
- Modify: `tests/TaiwuMods.Tests/Program.cs`（契约：声明 InitCatchPlace 补丁存在）

**Interfaces:**
- Consumes: `BlindBox.ComputeSessionSeed`、`BlindBoxConfig.SessionSeed`。
- Produces: 每进入一次捕蛐蛐场次，`BlindBoxConfig.SessionSeed` 被刷新为本场 21 个草丛蛐蛐 `(CricketColorId, CricketPartsId)` 的散列。

- [ ] **Step 1: 先更新清单与契约断言（会失败）**

在 `ModBuild/harmony-targets.json` 里把 CricketSingGradeColor 的 patterns 改为：

```json
    {
      "mod": "CricketSingGradeColor",
      "patterns": [
        "ShowCricketSingImage",
        "Sing",
        "InitCatchPlace"
      ]
    }
```

在 `tests/TaiwuMods.Tests/Program.cs` 的 `CricketSingGradeColorContract()` 末尾（最后一个 `Assert(...FallbackTierColors...)` 之后）加：

```csharp
        string seedPatch = ReadModFile(mod.Name, "CricketSingGradeColor.Frontend", "SessionSeedPatch.cs");
        Assert(seedPatch.Contains("[HarmonyPatch(typeof(ViewCatchCricket), \"InitCatchPlace\")]", StringComparison.Ordinal), "CricketSingGradeColor should patch InitCatchPlace to derive a per-session seed");
        Assert(seedPatch.Contains("BlindBox.ComputeSessionSeed", StringComparison.Ordinal), "CricketSingGradeColor session seed should hash the grass cricket composition");
        Assert(seedPatch.Contains("BlindBoxConfig.SessionSeed", StringComparison.Ordinal), "CricketSingGradeColor session seed patch should store the seed for the colorizer");
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: FAIL —— `Harmony manifest matches source patch surface` 报 CricketSingGradeColor 声明了 `InitCatchPlace` 但源码未 patch（或 `ReadModFile` 找不到 SessionSeedPatch.cs）。

- [ ] **Step 3: 实现场次盐补丁**

创建 `CricketSingGradeColor/CricketSingGradeColor.Frontend/SessionSeedPatch.cs`：

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 捕蛐蛐开局 <c>ViewCatchCricket.InitCatchPlace()</c> 一次性确定全部 21 个草丛蛐蛐。
    /// 其结束时各位置的 CricketColorId/CricketPartsId 均已填好——按位置顺序散列成本场盐 SessionSeed，
    /// 供盲盒「随机」形式做确定性取色：场次内不变(不闪)，换场次(组成不同)则变(强化盲盒感)。
    /// </summary>
    [HarmonyPatch(typeof(ViewCatchCricket), "InitCatchPlace")]
    internal static class SessionSeedPatch
    {
        private static FieldInfo _listField;

        public static void Postfix(ViewCatchCricket __instance)
        {
            try
            {
                _listField ??= AccessTools.Field(typeof(ViewCatchCricket), "_catchPlaceList");
                if (!(_listField?.GetValue(__instance) is Array places))
                {
                    BlindBoxConfig.SessionSeed = 0;
                    return;
                }

                int n = places.Length;
                int[] colorIds = new int[n];
                int[] partsIds = new int[n];
                for (int i = 0; i < n; i++)
                {
                    object place = places.GetValue(i);
                    if (place == null)
                    {
                        continue;
                    }

                    colorIds[i] = Convert.ToInt32(ReadField(place, "CricketColorId"));
                    partsIds[i] = Convert.ToInt32(ReadField(place, "CricketPartsId"));
                }

                BlindBoxConfig.SessionSeed = BlindBox.ComputeSessionSeed(colorIds, partsIds);
            }
            catch (Exception ex)
            {
                BlindBoxConfig.SessionSeed = 0;
                Debug.LogWarning("[CricketSingGradeColor] Could not derive session seed: " + ex);
            }
        }

        private static object ReadField(object obj, string name)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.GetValue(obj) : 0;
        }
    }
}
```

> 用反射读 `CricketColorId`/`CricketPartsId`，避开嵌套类型 `ViewCatchCricket.CricketPlaceInfo` 的命名。补丁由 `_harmony.PatchAll(...Assembly)`（FrontendPlugin 已调用）自动挂载，无需改插件入口。

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add CricketSingGradeColor/CricketSingGradeColor.Frontend/SessionSeedPatch.cs ModBuild/harmony-targets.json tests/TaiwuMods.Tests/Program.cs
git commit -m "CricketSingGradeColor: derive per-session seed from grass cricket composition"
```

---

### Task 5: config.lua 配置分组 + FrontendPlugin 读取 + 版本号

**Files:**
- Modify: `CricketSingGradeColor/config.lua`
- Modify: `CricketSingGradeColor/CricketSingGradeColor.Frontend/FrontendPlugin.cs`
- Modify: `tests/TaiwuMods.Tests/Program.cs`（契约：新配置 + 版本）

**Interfaces:**
- Consumes: `BlindBoxConfig.*`、`EBlindBoxMode`。
- Produces: 游戏设置 `EnableBlindBox`/`BlindBoxThreshold`/`BlindBoxMode` 写入 `BlindBoxConfig`，即时生效。

- [ ] **Step 1: 先更新契约断言（会失败）**

在 `CricketSingGradeColorContract()` 中，把现有版本断言行的 `version` 用法保留（`ExtractLuaString` 已取 `Version`），并在该方法内（`BlendMode` 断言之后）追加：

```csharp
        Assert(version == "1.1.0.0", "CricketSingGradeColor should ship the blind-box feature on the 1.1.0.0 line");
        Assert(config.Contains("SettingGroups = { \"声音品级\", \"高品盲盒\" }", StringComparison.Ordinal), "CricketSingGradeColor should declare the sound-grade and blind-box setting groups");
        Assert(Regex.IsMatch(config, @"Key\s*=\s*""BlendMode""[\s\S]*?GroupName\s*=\s*""声音品级"""), "CricketSingGradeColor BlendMode should sit in the sound-grade group");
        Assert(Regex.IsMatch(config, @"SettingType\s*=\s*""Toggle"",\s*Key\s*=\s*""EnableBlindBox""[\s\S]*?GroupName\s*=\s*""高品盲盒""[\s\S]*?DefaultValue\s*=\s*false"), "CricketSingGradeColor should expose a disabled-by-default blind-box toggle in the blind-box group");
        Assert(Regex.IsMatch(config, @"SettingType\s*=\s*""Dropdown"",\s*Key\s*=\s*""BlindBoxThreshold""[\s\S]*?GroupName\s*=\s*""高品盲盒""[\s\S]*?DefaultValue\s*=\s*7"), "CricketSingGradeColor blind-box threshold should default to 二品 (index 7)");
        Assert(Regex.IsMatch(config, @"SettingType\s*=\s*""Dropdown"",\s*Key\s*=\s*""BlindBoxMode""[\s\S]*?GroupName\s*=\s*""高品盲盒""[\s\S]*?DefaultValue\s*=\s*2"), "CricketSingGradeColor blind-box mode should default to 固定一品 (index 2)");
        Assert(plugin.Contains("\"EnableBlindBox\"", StringComparison.Ordinal) && plugin.Contains("\"BlindBoxThreshold\"", StringComparison.Ordinal) && plugin.Contains("\"BlindBoxMode\"", StringComparison.Ordinal), "CricketSingGradeColor plugin should read all three blind-box settings");
```

> `version` 变量在该方法开头已由 `ExtractLuaString(config, "Version")` 取得；现有断言 `plugin.Contains($"PluginConfig(\"CricketSingGradeColor\", \"RedContritio\", \"{version}\")")` 会自动校验插件版本与 config 一致。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: FAIL（`should ship ... 1.1.0.0` 或分组/设置断言未命中）。

- [ ] **Step 3: 改 config.lua**

把 `CricketSingGradeColor/config.lua` 整体替换为：

```lua
return {
    Title = "观音识蛐蛐",
    Description = "捕捉蛐蛐时，根据蛐蛐的叫声计算出品级，让蛐蛐叫声也有品级颜色。",
    Version = "1.1.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "cover.jpg",
    WorkshopCover = "cover.jpg",
    DetailImageList = {
        "details_1.png",
    },
    GameVersion = "1.0.40.0",
    FrontendPlugins = {
        "CricketSingGradeColor.Frontend.dll",
    },
    SettingGroups = { "声音品级", "高品盲盒" },
    DefaultSettings = {
        { SettingType = "Dropdown", Key = "BlendMode", GroupName = "声音品级",
          DisplayName = "声音品级权重",
          Description = "计算规则，音量优先最准确。",
          Options = { "音高优先", "均衡", "音量优先" },
          DefaultValue = 2 },
        { SettingType = "Toggle", Key = "EnableBlindBox", GroupName = "高品盲盒",
          DisplayName = "启用高品盲盒",
          Description = "声音品级达到下方门槛及以上时，捕蛐蛐小游戏的叫声圈改用盲盒色，保留抓捕悬念（仅捕捉小游戏生效，斗蛐蛐/收藏罐仍显示真实色）。",
          DefaultValue = false },
        { SettingType = "Dropdown", Key = "BlindBoxThreshold", GroupName = "高品盲盒",
          DisplayName = "盲盒门槛品级",
          Description = "声音品级达到该档及以上才触发盲盒；同时作为下方“随机高/低品级”的高低分界。",
          Options = {
              "<color=#8E8E8E>九品</color>",
              "<color=#FBFBFB>八品</color>",
              "<color=#6DB75F>七品</color>",
              "<color=#8FBAE7>六品</color>",
              "<color=#63CED0>五品</color>",
              "<color=#AE5AC8>四品</color>",
              "<color=#E3C66D>三品</color>",
              "<color=#F26A34>二品</color>",
              "<color=#E4504D>一品</color>",
          },
          DefaultValue = 7 },
        { SettingType = "Dropdown", Key = "BlindBoxMode", GroupName = "高品盲盒",
          DisplayName = "盲盒形式",
          Description = "高品蛐蛐的叫声圈如何显示。随机的高/低以门槛品级分界；固定默认白色＝还原原版白色涟漪。",
          Options = { "随机展示为低品级", "随机展示为高品级", "固定一品", "固定九品", "固定门槛品级", "固定默认白色" },
          DefaultValue = 2 },
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
```

- [ ] **Step 4: 改 FrontendPlugin 读取设置 + 版本号**

把 `[PluginConfig("CricketSingGradeColor", "RedContritio", "1.0.0.0")]` 改为 `[PluginConfig("CricketSingGradeColor", "RedContritio", "1.1.0.0")]`。

在 `ReloadSettings()` 方法体里，`SoundGradeConfig.ApplyBlend(index);` 之后、`catch` 之前，加一行调用：

```csharp
                SoundGradeConfig.ApplyBlend(index);
                ReadBlindBoxSettings();
```

并在 `ReloadSettings()` 方法之后新增方法：

```csharp
        /// <summary>读取「高品盲盒」三项设置写入 BlindBoxConfig（开关 / 门槛品级 / 盲盒形式）。</summary>
        private void ReadBlindBoxSettings()
        {
            try
            {
                bool enabled = false;
                int threshold = 7;
                int mode = (int)EBlindBoxMode.FixedTop;
                if (!string.IsNullOrEmpty(_modId))
                {
                    ModManager.GetSetting(_modId, "EnableBlindBox", ref enabled);
                    ModManager.GetSetting(_modId, "BlindBoxThreshold", ref threshold);
                    ModManager.GetSetting(_modId, "BlindBoxMode", ref mode);
                }

                BlindBoxConfig.Enabled = enabled;
                BlindBoxConfig.ThresholdGrade = threshold;
                BlindBoxConfig.Mode = (EBlindBoxMode)mode;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CricketSingGradeColor] Could not read blind-box settings, using defaults: " + ex);
            }
        }
```

> `ModManager.GetSetting` 有 `ref int` / `ref bool` 重载（Assembly-CSharp 58148/58160），Toggle 走 `ref bool`、Dropdown 走 `ref int`，返回 0-based 选中项。`OnModSettingUpdate` 已调 `ReloadSettings`，故改设置即时生效。

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS（含 `settings keys are read by source`：三个新 key 均在 FrontendPlugin 源码中出现）。

- [ ] **Step 6: 提交**

```bash
git add CricketSingGradeColor/config.lua CricketSingGradeColor/CricketSingGradeColor.Frontend/FrontendPlugin.cs tests/TaiwuMods.Tests/Program.cs
git commit -m "CricketSingGradeColor: expose blind-box settings group, bump to 1.1.0.0"
```

---

### Task 6: README + 构建产出验证

**Files:**
- Modify: `CricketSingGradeColor/README.md`
- Build: `CricketSingGradeColor/CricketSingGradeColor.Frontend/CricketSingGradeColor.Frontend.csproj`

**Interfaces:**
- Consumes: 前述全部。无新对外接口。

- [ ] **Step 1: README 增「高品盲盒」一节**

在 `CricketSingGradeColor/README.md` 的「## 设置项 / 默认参数」小节之前，插入：

````markdown
## 高品盲盒（可选）

默认关闭。开启后，**仅在捕蛐蛐小游戏**里，当某位置叫声算出的声音品级达到「盲盒门槛品级」及以上时，叫声圈不再显示真实品级色，而是按所选「盲盒形式」显示，保留抓捕时的悬念。斗蛐蛐 / 收藏罐 / 图鉴等卡片视图不受影响，仍显示真实声音品级色。

三个设置（「模组管理」内「高品盲盒」分组）：

- **启用高品盲盒**（默认关）。
- **盲盒门槛品级**（默认二品）：达到该档及以上才触发；同时作为「随机高/低品级」的高低分界（低＝门槛以下，高＝门槛及以上）。下拉每档用对应品级色显示。
- **盲盒形式**（默认固定一品）：
  - 随机展示为低品级 / 随机展示为高品级：在低区 / 高区里取一档显示。
  - 固定一品 / 固定九品 / 固定门槛品级：恒定一档。
  - 固定默认白色：还原原版白色涟漪。

「随机」是**确定性**的：种子由该位置叫声 `(音高, 圈大小)` 与**本场草丛蛐蛐的整体组成**散列得到——所以同一场次内圈色稳定不闪，换一场（草丛组成不同）同一只蛐蛐的盲盒色可能不同。改动即时生效，无需重启。
````

- [ ] **Step 2: 构建前端 DLL（需本机游戏 DLL）**

Run: `dotnet build CricketSingGradeColor/CricketSingGradeColor.Frontend/CricketSingGradeColor.Frontend.csproj -c Release`
Expected: `Build succeeded`，产出 `CricketSingGradeColor/Plugins/CricketSingGradeColor.Frontend.dll`。

> 若报找不到 `Assembly-CSharp.dll` 等：设置 `TAIWU_GAME_DIR` 环境变量指向太吾安装目录（见 `Directory.Build.props`）。

- [ ] **Step 3: 全量契约测试**

Run: `dotnet run --project tests/TaiwuMods.Tests`
Expected: PASS，末行 `All Taiwu mod contract tests passed.`

- [ ] **Step 4: 提交**

```bash
git add CricketSingGradeColor/README.md
git commit -m "CricketSingGradeColor: document the high-grade blind-box feature"
```

---

## 手动（在游戏内）验证清单

自动测试覆盖纯逻辑与源码契约；下列需实机/EasyBridge 确认（无法自动化）：

- 关闭「启用高品盲盒」→ 各位置圈色与旧版一致（回归）。
- 开启、门槛二品、固定一品 → 二品及以上位置圈显示一品色；门槛以下位置仍按真实声音品级色。
- 固定默认白色 → 高品位置回到原版白圈、无残留彩色描边。
- 随机高 / 随机低 → 同一场次内圈色稳定不闪；重进一场（草丛组成不同）盲盒色可变。
- 斗蛐蛐 / 收藏罐里的蛐蛐卡片不受盲盒影响（仍真实色）。
- 门槛下拉每档文字是否按品级色显示（富文本生效则保留；不生效则纯文字，亦可接受）。

> 部署到本机游戏做实机验证：`./deploy.ps1`（会按 `Directory.Build.props` 构建并拷贝到游戏 Mod 目录）；启用本地 mod 需「模组管理」勾选后重启游戏。

## Self-Review

**Spec coverage:**
- 仅捕捉小游戏生效 → Task 3（`ColorizeForCatch` 只在捕捉补丁调用；卡片 `Colorize` 不变）。✓
- 分组「高品盲盒」+ 三项（开关/门槛/形式）+ 默认值 → Task 5。✓
- 门槛 9 项品级色文案 → Task 5 config（富文本，best-effort，手动验证）。✓
- 盲盒 6 形式含「固定门槛品级」「固定默认白色」 → Task 1 枚举 + Task 3 还原白 + Task 5 下拉。✓
- 触发 `grade>=门槛`、高/低以门槛分界、空区间回退 → Task 1（含单测）。✓
- 随机确定性 + 场次盐(草丛组成散列) → Task 1（`ComputeSessionSeed`）+ Task 4（`InitCatchPlace` Postfix）。✓
- 「固定默认白色」清描边防对象池残留 → Task 3（`ResetToVanilla`）。✓
- 版本 1.1.0.0、契约/Harmony 清单同步 → Task 4/5。✓
- README → Task 6。✓

**Placeholder scan:** 无 TBD/TODO；每个改动步骤均含完整代码或精确字符串。✓

**Type consistency:** `EBlindBoxMode`（0..5）、`ResolveDisplayGrade(...)` 9 参签名、`ComputeSessionSeed(int[],int[])`、`TryGetGrade`/`TryGetColorForGrade`/`GradeCount`、`ColorizeForCatch`/`ResetToVanilla`、`BlindBoxConfig.{Enabled,ThresholdGrade,Mode,SessionSeed}` 在各 Task 间命名一致。✓
