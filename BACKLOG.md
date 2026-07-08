# Backlog

审查时间：2026-06-18
审查方法：4 个独立代理分别审查 DreamLover/AntiNTR/FertilityControl/跨 Mod 一致性，合成去重

---

## CRITICAL

### C1. [AntiNTR] 补丁位置不足，侮辱社会后果未阻止

**现状：** 补丁目标 `OfflineMakeLove`。返回 false 阻止了 Feature 197 添加和怀孕，但调用方 `OfflineExecuteFixedAction_MakeLove_Mutual`（L536136）仍会将 `MakeLoveState.RapeSucceed` 条目加入 `MakeLoveTargetList`。Complement 阶段处理该条目时仍执行：好感度 -30000、结仇（becomeEnemyType=9「情难自禁」）、秘密情报创建、人生记录。

**影响：** AntiNTR 的「阻止发生关系」功能不完整——物理行为被阻止，但社会后果照常发生。

**修复方向：** 将补丁移至 `OfflineExecuteFixedAction_MakeLove_Mutual`，在侮辱分支（L536117-536143）的入口处拦截。需要处理该方法同时包含夫妻/恋人/侮辱三条路径的复杂性。

---

### C2. [DreamLover] Prefix 中并发写入共享状态

**现状：** `PeriAdvanceMonth_RelationsUpdate` 通过 `ParallelActionManager` 并行执行，每个角色在独立线程中调用。DreamLover 的 Prefix 直接调用 `monthlyEvents.AddAdore/AddConfess/AddProposeMarriage` 写入 `MonthlyEventCollection`。

游戏原生逻辑将关系变更记录在线程本地的 `PeriAdvanceMonthRelationsUpdateModification` 中，延迟到串行的 `ComplementPeriAdvanceMonth_RelationsUpdate` 阶段才写入共享状态。

**影响：** 多线程同时写入 `MonthlyEventCollection` 可能导致数据竞争、崩溃或事件丢失。

**修复方向：** 模仿游戏原生模式——在 Prefix 中将事件意图记录到 Modification 对象中，在 Complement 阶段的补丁中实际写入月报。或者补丁 Complement 方法而非 PeriAdvanceMonth。

---

## IMPORTANT

### I1. [DreamLover] PassRelationFilter 关系方向错误

**现状：** `TryGetRelation(charId, taiwuId)` 查询的是 NPC→太吾 方向的关系。但 `RelFilterDefs` 中的类型值按「NPC 与太吾的关系角色」命名：

- `Rel_BloodParent`(1) 意为「允许太吾的亲生父母追求」。但 BloodParent(1) 在 NPC→太吾 方向上表示「NPC 是太吾的 BloodParent」，即 NPC 是太吾的父母——这恰好是对的。
- `Rel_BloodChild`(2) 意为「允许太吾的亲生子女追求」。但 BloodChild(2) 在 NPC→太吾 方向上表示「NPC 是太吾的 BloodChild」，即 NPC 是太吾的子女——这也是对的。

**重新分析：** NPC→太吾 方向的 `BloodParent(1)` 表示"NPC 是太吾的 BloodParent"。这意味着 NPC→太吾 存的是 BloodParent 标记，说明 NPC 是太吾的父/母。所以 `Rel_BloodParent` 过滤的确是太吾的父母。**方向实际上是对的。**

但 `GetOppositeRelationType(1)=2`，AddRelation 自动添加反向关系。所以如果 NPC 是太吾的父母，NPC→太吾 方向存的是 BloodParent(1)。这个过滤是正确的。

**需要进一步验证：** 让代理确认 `TryGetRelation(charId, taiwuId)` 返回的 RelationType 的语义是「charId 对 taiwuId 持有的关系类型」还是其他含义。如果是「charId 对 taiwuId」的关系，那么 NPC 是太吾父母时，NPC→太吾 方向确实存 BloodParent(1)。

**状态：** 待验证

---

### I2. [FertilityControl] 蛐蛐覆盖未修正孕期和运气值

**现状：** `CricketRatePatch` 是 `CreatePregnantState` 的 Postfix。原生代码根据 `IsHuman` 设定 `ExpectedBirthDate`（人类 6-9 天，蛐蛐 42 天）并在蛐蛐时重置 `CricketLuckPoint=0`。Postfix 改写 `IsHuman` 后，孕期和运气值不匹配：

- 覆盖为蛐蛐时：孕期仍为 6-9 天（应为 42），CricketLuckPoint 未重置
- 覆盖为人类时：孕期仍为 42 天（应为 6-9），CricketLuckPoint 已被错误重置

**修复：** Postfix 中检测 `IsHuman` 变化，同步修正 `ExpectedBirthDate` 和 `CricketLuckPoint`。

---

### I3. [AntiNTR] 配偶集合未过滤死亡角色

**现状：** `ShouldBlock` 中 `GetRelatedCharIds(charId, 1024)` 返回所有配偶 ID，包括已死亡的。已故配偶的关系仍在系统中，导致过度保护。

**修复：** 使用 `GetAliveSpouse(charId)` 替代，或在遍历时添加 `IsCharacterAlive(spouseId)` 检查。

---

### I4. [AntiNTR] 关系方向性和缺失类型

**现状：**
- `IsProtected` 只查 `TryGetRelation(taiwuId, charId)` 方向（太吾→角色），可能遗漏反向关系
- `RelationSettings` 缺少 `AdoptiveChild(128)`：太吾是义父时，义子不受保护

**修复：** 查双向关系，或将两个方向的位掩码做 OR 运算。添加 `AdoptiveChild(128)` 到 `RelationSettings`。

---

## MINOR

### M1. [DreamLover] TryMarriage 未检查太吾的门派限制

**现状：** 只检查 `npc.OrgAndMonkTypeAllowMarriage()`，未检查太吾自身。若太吾所在组织 `ChildGrade < 0`，仍可被求婚。

**修复：** 在 `IgnoreGang=false` 时同时检查 `taiwu.OrgAndMonkTypeAllowMarriage()`。

---

### M2. [DreamLover] 可能产生重复月报事件

**现状：** Prefix 返回 true，原生 AI 也运行。高好感 NPC 可能同时被 Mod 和原生 AI 添加 AddAdore 月报事件，导致同一 NPC 同月出现两次「心生爱慕」。

**影响：** 外观问题。重复事件的 Apply 阶段会因 `AllowAddingAdoredRelation` 检查到已存在关系而跳过，不会导致数据问题。

---

### M3. [AntiNTR / FertilityControl] 缺少太吾死亡保护

**现状：** DreamLover 有 `if (taiwuId < 0) return true` 保护，但 AntiNTR 和 FertilityControl 没有。`GetTaiwuCharId()` 在太吾死亡/转世期间可能返回 -1。

**修复：** 在两个 Mod 的 Prefix 开头添加 `taiwuId < 0` 检查。

---

### M4. [FertilityControl] SetAllFertility 路径缺少已孕检查

**现状：** `CheckPregnantPatch` 的 Prefix 在 `SetAllFertility=true` 分支直接跳到概率计算，跳过了 Feature 198（已孕）检查。但实际上 Prefix 开头已经检查了 Feature 198（L25-28），所以此问题**不存在**——已在通用检查中处理。

**状态：** 经复查，非问题。

---

## ACCEPTABLE

### A1. [FertilityControl] SetAllFertility 滑块语义不直观

`AllFertilityValue` 含义是「总生育力值」，范围 0-15000。公式 `base(60) * val / 10000`，所以 val=10000 时概率 60%，val=15000 时 90%，val=100 时仅 0.6%。对玩家不够直观。

**建议：** 在 config.lua 描述中说明「值越大越容易怀孕，10000 约为正常水平」。

---

### A2. [FertilityControl] 生育力除数 20 的隐含前提

代码中硬编码除数 20（太吾参与时的值），前提是 Prefix 保证了太吾参与。这个假设正确但未文档化。

---

## 功能想法（Feature Backlog）

### F1. [DreamLover]「娶已婚对象 / 太吾一夫多妻」求婚（原 config 保留项 MarriedKiller / Polygynous，2026-07-09 从 UI+代码移除）

**背景：** config 曾有两个开关 `MarriedKiller`(娶已婚 NPC) / `Polygynous`(太吾多配偶)，但它们是**无效保留项**——`TryMarriage` 先调游戏硬规则 `AllowAddingHusbandOrWifeRelation`（任一方有在世配偶即拒），开关根本轮不到生效。为免误导玩家，已从 config + `TryMarriage` 死检查 + `Settings` 字段一并移除。

**为什么难（不是那道检查的事，是数据模型）：** 游戏按**"单配偶"**建模——`GetAliveSpouse` 从 `HusbandsAndWives` 集合返回**第一个在世配偶**，全游戏下游（传承/后代/生育/守寡/离婚/同道/人物面板）都按单配偶读它。强塞第二个 1024(配偶)关系 → 二房被系统无视或关系互指不一致 → 行为错乱甚至崩溃（同"陌生人爱慕崩溃"一类：硬造游戏不预期的状态）。

**两条路线：**
- **F1a「拆散原配再娶」（可安全实现，中等工作量）**：求婚前若对方已婚，先让对方与原配离婚（`ApplyBreakupWithBoyOrGirlFriend` / 双向去 1024，并处理原配守寡/心情/月报），再嫁太吾。语义是"棒打鸳鸯"，非真多配偶。
- **F1b 真·一夫多妻**：需改写全游戏单配偶假设，mod 内做不干净、会引连锁 bug。**不建议。**

**决定（2026-07-09）：** 先移除保留项、记入 backlog；F1a 若要做，另起独立设计 + 实机验证。

---

## 待办优先级

1. **C1 + C2**：架构级修改，必须在发布前完成
2. **I1**：需要先验证关系方向语义再决定是否修改
3. **I2**：明确的 bug，修复简单
4. **I3 + I4**：AntiNTR 逻辑修正
5. **M1-M3**：发布前修复
6. **A1-A2**：文档改进即可
7. **F1**：功能想法，需要时再评估（F1a 可做 / F1b 不建议）
