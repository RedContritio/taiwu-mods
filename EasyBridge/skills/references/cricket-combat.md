# 斗蛐蛐自动化（参考页，按需加载）

> `taiwu-game` skill 的参考页。**只在做斗蛐蛐相关的搭场景/调试时才读**。类名/枚举值以当前反编译为准（版本更新可能漂移）。

斗蛐蛐**不是实时算的**：开局 `CricketCombatKit.Board.StartCombat()` 里 `_matchContext.Simulate()` **一次性预算整局**，产出事件队列 `_matchContext.Logs`（`Queue<CricketCombatLog>`）；之后 `Board.NextLog()` 逐条**出队** + DOTween/协程**回放动画**。所以"停在某事件"要读**事件队列**，不是读动画。

## 一、搭场景（后端 `/eval`，运行时内存、别保存存档）

先开启后端 eval：`SB -Path "/config" -Body @{ enableEval=$true }`。

- **造我方蛐蛐**：`Item.CreateCricket(ctx, colorId, partId)` → `taiwu.AddInventoryItem(...)`。品名由 `(colorId,partId).CalcCricketGrade()` 决定（内部 0~8）。**品名与品级反直觉**：`八败`=templateId 21=Level 8=**1品(最强)**；`呆物`=templateId 0=Level 0=**9品(最弱)**。
- **造 N 品对手**：`/spawn {villager:$false}` 生成散人 NPC（村民过月会崩、且散人通常无护卫最易开战）→ `ChangeOrganization` 设 `OrganizationInfo(org, grade)`（`grade=7`=2品）。对手三只蛐蛐由 `CricketGenerator.Generate(grade,grade,…)` **现生成**（如 grade7 → 内部 `[7,7,4]`）。
- **设出战方案**：`Taiwu.SetCricketPlan(ctx, planIndex, …)` + `SetLastCricketPlan`（方案0 = 先锋/大将/主帅 三只）。
- **开局**：`Item.GmCmd_StartCricketCombat(ctx, enemyId)` → 前端 `CricketCombat` 窗口打开，停在 `StartCombat` 等点击。
- **坑**：开局前先 `/time {pause}`，只用小步 `/time step` 渲染界面，看到 `StartCombat` 再点；否则 resume 久了对局会自动打完。重开残局用窗口里「落闸罢斗」。

## 二、卡在特定事件（前端反射，**纯反射够用、零编译**）

先开启前端反射写/调类：`UI -Path "/config" -Body '{"enableInvoke":true}'`。

**读事件队列、别读 Spine 动画名**（动画名是糊弄信号，和"播到第几条 log"不对齐 → 会停早/停错）。

- **静态根**：`Board` = `Game.Views.Cricket.Combat.CricketCombatKit.Board`，**静态 readonly 字段**（非属性，故 `/static get_Board` 行不通）。
- **读到队列**：`POST /reflect/invoke`，目标用 `{"$ref":{"type":"…CricketCombatKit","member":"Board._matchContext.Logs…"}}` —— **`$ref` 能解析静态字段并沿点号 `Traverse` 读私有实例字段**（`_matchContext` 私有 → `Logs` 公有）。`Queue<T>` 不可索引，但内部数组可：`Logs._array`、`Logs._head`、`Logs._size`（出队 slot 置 null/读 0）。
- **回显字段值**（`/static` 只 snapshot **方法返回值**、读不到字段本身）：把目标值传进"原样返回"的静态方法当返回值读出 —— int 用 `CricketCombatKit.WrapProperty(int,int)`（唯一签名，回显 `returnValue`）；long（如 `RuntimeId`，int 转换会抛）用 `System.TimeSpan.FromTicks(long)` → 读返回值 `_ticks`。
- **定位事件**：`CricketCombatLog.Type` = `ECricketCombatLogEventType`（攻击/撕咬是 `Damage=4`，`CricketCombatLogDamage` 带 `Attacker/Defender.RuntimeId` + `DamageType=Bite`）；`Board.SelfCricket`/`EnemyCricket` 是双方当前出战蛐蛐（`.Data.RuntimeId`），`Board.IsAlly(id)` 判我方，`CurrentMatch`=第几局(0=先锋)。扫队列找第一条 `Type==Damage && Attacker.RuntimeId==我方` = "我方首次攻击"。
- **精确冻结**：回放掺了**实时协程**（`DelayCallRealTime`），`/time step` 不稳；改用 **resume + 高频轮询 `_size`（出队进度）+ 命中即 `/time {pause}`（`timeScale=0`）**。要细到接触帧就把命中后微调步长收到 ~50–80ms（整条 Bite 约 0.5–0.7s，期间 `_size` 不变、可安全细调）。

> **通用范式**：凡"预模拟 → 事件队列 → 回放"的子系统，都能用 `$ref` 读静态+私有字段、轮询队列出队、卡在目标事件冻结 —— 不必为它写专用端点。实机验证：用此法精确停在"八败首次撕咬"帧。
