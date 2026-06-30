# 观音识蛐蛐 — 高品盲盒（High-Grade Blind Box）设计

- 日期：2026-07-01
- Mod：`CricketSingGradeColor`（观音识蛐蛐），纯前端 Harmony 补丁
- 版本：`1.0.0.0` → `1.1.0.0`

## 背景与目标

本 Mod 现在会把捕蛐蛐叫声涟漪圈按「声音品级」染色，让玩家一眼分辨品级。但部分玩家希望**对高品级蛐蛐保留抓捕时的悬念（盲盒感）**：当计算出的声音品级达到某个阈值及以上时，**不直接显示真实品级色**，而是用「盲盒色」替代。

**关键约束：盲盒伪装只作用于捕蛐蛐小游戏**（`ViewCatchCricket`）。斗蛐蛐 / 收藏罐 / 图鉴 tooltip 等卡片视图（`CricketView` / `CricketViewNew`）仍显示真实计算色——此时蛐蛐已到手，无需隐藏。

品级 index 约定（与 DreamLover 一致）：`0 = 九品（最低）… 8 = 一品（最高）`。`SoundGradeCalculator` 产出的 grade 直接用作 `_palette` 下标，范围 0..8。

## 新增配置（config.lua）

顶层新增分组顺序：

```lua
SettingGroups = { "声音品级", "高品盲盒" },
```

现有 `BlendMode`（声音品级权重）归入「声音品级」组（加 `GroupName = "声音品级"`）。

「高品盲盒」组三项：

| Key | SettingType | DisplayName | 默认 | 说明 |
|---|---|---|---|---|
| `EnableBlindBox` | Toggle | 高品盲盒 | `false` | 总开关；关闭时行为与旧版完全一致 |
| `BlindBoxThreshold` | Dropdown | 盲盒门槛品级 | `7`（二品） | 9 项：九品…一品，每项文字用对应品级色（rich-text，best-effort） |
| `BlindBoxMode` | Dropdown | 盲盒形式 | `2`（固定一品） | 6 项，见下 |

- 下拉返回 **0-based index**（与现有 `BlendMode` 同约定：`config.lua` `DefaultValue` 与代码 index 直接对应）。
- `EnableBlindBox` 默认 `false`：老用户更新后表现不变，符合「不加自启用总开关、但允许按功能开关」的项目约定（这是该功能自身的开关，非整 Mod 的启停）。
- `BlindBoxThreshold` 默认 `7` = 二品，触发条件「二品及以上」。
- 触发条件含阈值本身：`真实grade >= 阈值`。

### `BlindBoxThreshold` 下拉项（index 0→8 与对应色）

| index | 文案 | 品级色 hex |
|---|---|---|
| 0 | 九品 | `8E8E8E` |
| 1 | 八品 | `FBFBFB` |
| 2 | 七品 | `6DB75F` |
| 3 | 六品 | `8FBAE7` |
| 4 | 五品 | `63CED0` |
| 5 | 四品 | `AE5AC8` |
| 6 | 三品 | `E3C66D` |
| 7 | 二品 | `F26A34` |
| 8 | 一品 | `E4504D` |

文案用 `<color=#hex>九品</color>` 形式（与 `FallbackTierColors` 同一套游戏标准品级色）。若游戏下拉不渲染富文本，则退化为纯文字显示——属 best-effort（用户表述为「最好」）。

### `BlindBoxMode` 下拉项（index 0→5）

| index | 文案 | 含义 |
|---|---|---|
| 0 | 随机展示为低品级 | 在低区 `[0, 阈值-1]` 内确定性取一个 grade |
| 1 | 随机展示为高品级 | 在高区 `[阈值, 8]` 内确定性取一个 grade |
| 2 | 固定一品（默认） | 恒为 grade 8 |
| 3 | 固定九品 | 恒为 grade 0 |
| 4 | 固定门槛品级 | 恒为阈值 grade 本身 |
| 5 | 固定默认白色 | 还原成原版白色涟漪（不染色、清掉描边） |

## 触发与映射逻辑

仅在捕蛐蛐路径执行：

1. 算真实 `grade`（`SoundGradeCalculator.TryGetGrade`）。失败则不染色（保持原版白）。
2. 若 `!EnableBlindBox || grade < 阈值` → 显示真实 grade 色（现有行为）。
3. 否则按 `BlindBoxMode` 求「展示 grade」或「原版白色」：
   - 随机低：`pick([0, 阈值-1])`；区间为空（阈值=0）→ 回退 grade 0。
   - 随机高：`pick([阈值, 8])`（阈值=8 时区间为 `{8}`）。
   - 固定一品 → 8；固定九品 → 0；固定门槛品级 → 阈值。
   - 固定默认白色 → 还原原版白（见下）。
4. 展示 grade → 取 `_palette` 对应色 → 走现有 `RippleColorizer` 染色（vivify + outline）。

### 随机的确定性与「场次盐」

`pick([lo, hi])` 必须**稳定**，否则同一蛐蛐每次鸣叫会闪色。decoy 种子 = 确定性散列 `hash(place.SingPitch, place.SingSize, SessionSeed)`：

- `(SingPitch, SingSize)`：该位置蛐蛐固定不变的声音签名（与现有「同位置颜色稳定」同源）——区分**同一场次内不同位置**的盲盒色。
- `SessionSeed`：**本场全部草丛蛐蛐的内容散列**。捕蛐蛐开局时 `ViewCatchCricket.InitCatchPlace()`（无参）一次性确定全部 21 个位置的蛐蛐；其结束时 `_catchPlaceList[0..20]` 的 `CricketColorId`/`CricketPartsId`/`SingPitch`/`SingSize` 均已填好（反编译 70286-70287 行确认）。对其 Postfix，按**位置顺序**把 21 个 `(CricketColorId, CricketPartsId)`（蛐蛐根身份）散列成一个 int 存入 `BlindBoxConfig.SessionSeed`——区分**不同场次**。
  - 效果：**同一场次内** seed 不变 → 圈色稳定不闪；**换场次重刷**（草丛组成不同）seed 改变 → 同一只蛐蛐的盲盒色可能不同，强化盲盒感。
  - 由游戏状态直接决定、可复现（无可变计数器）；两场组成完全相同 → seed 相同，可接受。
  - 反射读取 `_catchPlaceList` 失败时回退 `SessionSeed = 0`（仍确定性，仅丢失换场次变化），不抛错。
- 散列用稳定整数运算（避免依赖 `Object.GetHashCode`），`((seed % range) + range) % range + lo` 落入 `[lo, hi]`。

### 「固定默认白色」=还原原版

原版 `ShowCricketSingImage` 已把圈设为白色再淡出。此模式我们**主动还原**而非依赖「什么都不做」（涟漪 `CImage` 可能被对象池复用、残留上次的颜色/Outline）：将 RGB 重置为白、`useGraphicAlpha` 下移除/禁用我们加的 `Outline`（`effectDistance = 0` 或禁用组件），保留当前 alpha 以不打断淡出。

## 代码结构（小单元、不污染卡片视图共享路径）

1. **`BlindBoxConfig.cs`（新）**：`Enabled` / `ThresholdGrade`(int 0..8) / `Mode`(枚举) 静态字段；`EBlindBoxMode` 枚举；`SessionSeed` 静态字段。品级中文名与 hex 色表常量（供文档/校验，不强依赖）。
2. **`SoundGradeCalculator.cs`**：拆出
   - `bool TryGetGrade(int pitch, int size, out int grade)`
   - `bool TryGetColorForGrade(int grade, out Color color)`（clamp 到 palette）
   - `int GradeCount`（= `_palette.Length`，供 clamp）
   - `TryGetTierColor` 改为二者组合（**卡片视图路径行为不变**）。
3. **`RippleColorizer.cs`**：
   - 新增 `ColorizeForCatch(Graphic image, int pitch, int size)`：算真实 grade → 盲盒变换 → 染色或 `ResetToVanilla`。
   - 新增 `ResetToVanilla(Graphic image)`：RGB→白、清 Outline、保留 alpha。
   - 原 `Colorize`（卡片视图用）**不变**。
4. **`BlindBox.cs`（新，或并入 RippleColorizer）**：纯逻辑 `Resolve(int trueGrade, int pitch, int size, out int displayGrade, out bool vanillaWhite)`，无 Unity 依赖、易单测。
5. **`CricketSingColorPatch.cs`**：catch 路径由 `Colorize` 改调 `ColorizeForCatch`。
6. **`SessionSeedPatch.cs`（新）**：`[HarmonyPatch(typeof(ViewCatchCricket), "InitCatchPlace", new Type[0])]` Postfix(`__instance`) → 反射读 `_catchPlaceList`，按位置顺序散列 21 个 `(CricketColorId, CricketPartsId)` 写入 `BlindBoxConfig.SessionSeed`（失败回退 0）。
7. **`FrontendPlugin.cs`**：`ReloadSettings` 读取 `EnableBlindBox` / `BlindBoxThreshold` / `BlindBoxMode` 写入 `BlindBoxConfig`（沿用 `ModManager.GetSetting`）；`PluginConfig` 版本 → `1.1.0.0`。
8. **`config.lua`**：加 `SettingGroups` 与三项设置；`Version` → `1.1.0.0`。
9. **`README.md`**：新增「高品盲盒」一节（含 6 种盲盒形式、门槛、场次盐说明、仅捕捉小游戏生效）。

`ModBuild/mods.json` 无需改动（已含 `CricketSingGradeColor`）；构建沿用现有 csproj，输出 `Plugins/CricketSingGradeColor.Frontend.dll`。

## 边界与错误处理

- 校准失败 / 读不到 grade：保持原版白色圈，不抛错（沿用现有 try/catch 风格）。
- `BlindBoxThreshold` / `BlindBoxMode` index 读取后 clamp 到合法范围。
- 随机区间为空：回退到边界 grade（见上）。
- 盲盒逻辑只在 catch 路径生效，卡片视图零影响（回归点：斗蛐蛐/收藏罐仍显示真实计算色）。
- `SessionSeed` 仅做散列盐，溢出无害（按需取模）；反射失败回退 0。

## 测试与验证

- 单元（`BlindBox.Resolve`，纯逻辑，可进 `tests/TaiwuMods.Tests`）：
  - `grade < 阈值` → 显示真实 grade，盲盒不触发。
  - 各模式映射正确（固定一品/九品/门槛；随机落在对应区间）。
  - 随机确定性：相同 `(pitch,size,SessionSeed)` 多次调用同结果；`SessionSeed` 改变结果可变。
  - 空区间回退正确（阈值=九品的随机低）。
- 手动/在用（EasyBridge 或实机）：
  - 关闭开关 → 与旧版逐位置颜色一致（回归）。
  - 开启、阈值二品、固定一品 → 高品圈显示一品色；低品圈仍真实色。
  - 固定默认白色 → 高品圈回到原版白、无残留描边。
  - 卡片视图（斗蛐蛐）不受盲盒影响。
  - 富文本下拉文案着色是否生效（生效则保留，否则纯文字亦可）。
