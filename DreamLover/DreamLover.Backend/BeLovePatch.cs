using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Config.EventConfig;
using HarmonyLib;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.ParallelModifications;
using GameData.Domains.Character.Relation;
using GameData.Utilities;

namespace DreamLover.Backend
{
    // ============================================================================================
    // 设计（2026-06-25 重构 + 探针定稿）：扩展原生月度恋爱流程，而非替代。
    //
    // 原版引擎：NPC 过月时若对【太吾】产生新意向，complement(Character.ComplementPeriAdvanceMonth_RelationsUpdate)
    // 不直接写关系，而排队一条月报记录(AddAdore=45 / AddConfess=46 / AddProposeMarriage=48)，真正落关系/交互在月报
    // 事件链里。但这些月报事件的 OnCheckEventCondition(=CanStartRelation_*) 是好感/资格门控，失败时 HandleMonthlyEvent
    // 硬抛异常卡死过月。同格限制只在【候选收集】阶段(OfflineExecuteCharacterActionsInArea：太吾仅进入自身所在 block 的
    // charSet)，complement 排队/校验/派发全链路都【无】block 判断（已反编译查实）。
    //
    // DreamLover 做法：
    //  1) 前置遍历(本类 Prefix)：按本 mod 筛选条件命中后，【记录】对太吾的关系修改(与原版同一通道
    //     RecordParameterClass(PeriAdvanceMonthRelationsUpdateModification))，并把 NPC + 其将触发的月报事件 GUID
    //     记入 ForcedThisMonth。直接复用 complement 通道 → 绕过候选收集的同格门 → 离格(IgnoreDistance)NPC 同样会出月报。
    //  2) LoveEventGatePatch(挂 TaiwuEventItem.CheckCondition)：仅当事件本不通过、且其 GUID 正是本 mod 为该 NPC 促成的
    //     那一个恋爱事件(精确 GUID 匹配，不再按 EventGroup 放行)时才放行——既避免硬抛异常，又【不会】误放行同组的非恋爱
    //     事件(如结友 a546e498/recordType49)或该 NPC 当月其它月报事件。原版自发事件本就通过门控，第一行直接返回，不受影响。
    //
    // 爱慕/表白/求婚 三条都有原生月报事件，按 recordType→GUID：
    //   45 爱慕  → 11ce2ba2-5abc-4f9e-9fbc-8892f03cd8f6 (心生爱慕, OnEventEnter 直接落单向爱慕16384)
    //   46 表白  → 8ce2db54-994d-4790-bfe3-6cedd7473277 (表露心事, 选项成两情相悦8192/回应)
    //   48 求婚  → 9c81352d-b715-4554-85fc-50322fe428f6 (头事件, 成功分支链入 e7b23d15 其条件恒真无需放行)
    //
    // 心去难留(断情)：原生断情月报(47 → 63a3c0e9)的 OnCheckEventCondition 是 CanEndRelation(16384)，要求【双向】爱慕；
    // 而本功能针对【单向】未被回应的爱慕(太吾不爱该 NPC)，原生断情事件条件恒 false，即便强行放行其两个选项也都不会真正
    // 移除单向 16384(一个会按好感重新加回、一个因要求太吾也爱慕而提前返回)。故断情【不能】走原生月报，改为直接
    // Character.ApplySeverAdore(移除 npc→太吾 16384 + 记 EndAdored 生平 + 好感/心情结算)。
    //
    // 好感(及其它维度)在本 mod 里只作【筛选】，不作门槛——门槛由 hook 精确放行绕过。
    // ============================================================================================
    [HarmonyPatch(typeof(Character), "PeriAdvanceMonth_RelationsUpdate")]
    public static class BeLovePatch
    {
        private const ushort Adored = 16384;
        private const ushort Spouse = 1024;

        // 本 mod 促成的三条月报恋爱事件 GUID（与 recordType 45/46/48 一一对应）。
        internal static readonly Guid AdoreGuid = new Guid("11ce2ba2-5abc-4f9e-9fbc-8892f03cd8f6");
        internal static readonly Guid ConfessGuid = new Guid("8ce2db54-994d-4790-bfe3-6cedd7473277");
        internal static readonly Guid MarryGuid = new Guid("9c81352d-b715-4554-85fc-50322fe428f6");

        // 本月「DreamLover 促成」的 NPC → 它将触发的恋爱事件 GUID。Prefix 填充，LoveEventGatePatch 精确匹配后放行。
        internal static readonly ConcurrentDictionary<int, Guid> ForcedThisMonth =
            new ConcurrentDictionary<int, Guid>();

        private static volatile int _lastStamp = int.MinValue;
        private static readonly object _stampLock = new object();

        private static void Log(string message)
        {
            if (Settings.DebugMode)
                AdaptableLog.Info("[DreamLover] " + message);
        }

        [HarmonyPrefix]
        public static bool Prefix(Character __instance, DataContext context)
        {
            ResetForcedSetOnNewMonth();

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0)
                return true;

            int charId = __instance.GetId();
            if (charId == taiwuId)
                return true;

            if (!DomainManager.Character.IsCharacterAlive(charId))
                return true;

            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out Character taiwu))
                return true;

            bool anyEnabled = Settings.EnableEnamor || Settings.EnablePursued || Settings.EnableMarry;
            if (Settings.ForgetMe && !Settings.EnableEnamor && !Settings.EnablePursued)
                TryForgetMe(context, __instance, charId, taiwuId, taiwu);

            if (!anyEnabled)
                return true;

            if (!PassFilters(__instance, charId, taiwuId, taiwu))
                return true;

            // 决策顺序：双向爱慕→求婚；单向爱慕→表白；尚未爱慕→爱慕。命中即记录 + 标记，交给原生月报事件处理。
            if (Settings.EnableMarry && TryMarriage(context, charId, __instance, taiwu))
                return true;

            if (Settings.EnablePursued && TryConfession(context, charId, __instance, taiwu))
                return true;

            if (Settings.EnableEnamor && TryEnamor(context, charId, __instance, taiwu))
                return true;

            return true;
        }

        // 每月初清空「本月促成」集合。Prefix 在并行 worker 线程上逐角色调用，用月度戳 + 锁保证整月清一次。
        // 不能在 complement 后置里清——那时月报尚未处理，hook 还要读这个集合。
        private static void ResetForcedSetOnNewMonth()
        {
            int stamp = DomainManager.World.GetCurrDate();
            if (stamp == _lastStamp)
                return;
            lock (_stampLock)
            {
                if (stamp == _lastStamp)
                    return;
                ForcedThisMonth.Clear();
                _lastStamp = stamp;
            }
        }

        private static void Force(int charId, Guid eventGuid)
        {
            ForcedThisMonth[charId] = eventGuid;
        }

        // ---------- 筛选（本 mod 自己的选择条件，非门槛） ----------

        private static bool PassFilters(Character npc, int charId, int taiwuId, Character taiwu)
        {
            int ageYears = npc.GetCurrAge();
            sbyte favorType = CharacterUtils.GetFavorabilityType(charId, taiwuId);
            int goodnessLevel = CharacterUtils.GetGoodnessLevel(npc);
            sbyte charmLevel = CharacterUtils.GetCharmLevel(npc);
            sbyte rankLevel = CharacterUtils.GetRankLevel(npc);
            int infectState = CharacterUtils.GetInfectionState(npc);

            if (!DreamLoverRules.PassBasicFilters(
                    Settings.AcceptSameGender,
                    npc.GetGender() == taiwu.GetGender(),
                    Settings.IgnoreDistance,
                    CharacterUtils.IsAtSameLocation(npc, taiwu),
                    ageYears,
                    Settings.MinAge,
                    Settings.MaxAge,
                    favorType,
                    Settings.FavorMin,
                    Settings.FavorMax,
                    goodnessLevel,
                    Settings.GoodMin,
                    Settings.GoodMax,
                    charmLevel,
                    Settings.CharmMin,
                    Settings.CharmMax,
                    rankLevel,
                    Settings.RankMin,
                    Settings.RankMax,
                    infectState,
                    Settings.InfectMin,
                    Settings.InfectMax))
            {
                Log(charId + " filtered: basic filter");
                return false;
            }

            if (!PassRelationFilter(charId, taiwuId))
            {
                Log(charId + " filtered: relation filter");
                return false;
            }

            return true;
        }

        private static bool PassRelationFilter(int charId, int taiwuId)
        {
            if (!DomainManager.Character.TryGetRelation(taiwuId, charId, out RelatedCharacter rel))
                return true;

            for (int i = 0; i < Settings.RelFilterDefs.Length; i++)
                if (RelationType.HasRelation(rel.RelationType, Settings.RelFilterDefs[i].Type))
                    return DreamLoverRules.PassRelationFilter(rel.RelationType, Settings.RelFilterDefs, Settings.RelFilter);

            return true;
        }

        // ---------- 决策 + 记录（不再做 CanStartRelation_* 好感门控——交给 hook 精确放行） ----------

        private static bool TryMarriage(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            if (!DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                return false;
            if (DomainManager.Character.HasRelation(charId, taiwuId, Spouse))
                return false;
            // 保留硬性婚姻规则（一夫一妻/血亲等），这不是好感门槛。
            if (!RelationTypeHelper.AllowAddingHusbandOrWifeRelation(charId, taiwuId))
            {
                Log(charId + " marriage blocked: formal marriage rules reject it");
                return false;
            }
            if (!Settings.MarriedKiller && CharacterUtils.IsMarried(charId))
            {
                Log(charId + " marriage blocked: NPC already married");
                return false;
            }
            if (!Settings.Polygynous && CharacterUtils.IsMarried(taiwuId))
            {
                Log(charId + " marriage blocked: Taiwu already married");
                return false;
            }
            if (!Settings.IgnoreGang && !npc.OrgAndMonkTypeAllowMarriage())
            {
                Log(charId + " marriage blocked: NPC org/monk restriction");
                return false;
            }
            if (!Settings.MonkKiller && npc.GetMonkType() != 0)
            {
                Log(charId + " marriage blocked: NPC is a monk");
                return false;
            }
            if (!Settings.CharmingBonze && taiwu.GetMonkType() != 0)
            {
                Log(charId + " marriage blocked: Taiwu is a monk");
                return false;
            }

            Log(charId + " marriage: recording 求婚(共结连理) monthly update");
            RecordNewRegularRelation(context, npc, taiwu, Spouse, true);
            Force(charId, MarryGuid);
            return true;
        }

        private static bool TryConfession(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            if (DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                return false;

            Log(charId + " confession: recording 表白(表露心事) monthly update");
            var mod = new PeriAdvanceMonthRelationsUpdateModification(npc)
            {
                NewBoyOrGirlFriend = (targetChar: taiwu, succeed: true)
            };
            RecordRelationsUpdate(context, mod);
            Force(charId, ConfessGuid);
            return true;
        }

        private static bool TryEnamor(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            // 保留 740/741 特殊恋爱标记等硬约束（非好感门槛）。
            if (!RelationTypeHelper.AllowAddingAdoredRelation(charId, taiwuId))
            {
                Log(charId + " enamor blocked: formal adore rules reject it");
                return false;
            }

            Log(charId + " enamor: recording 爱慕(心生爱慕) monthly update");
            RecordNewRegularRelation(context, npc, taiwu, Adored, false);
            Force(charId, AdoreGuid);
            return true;
        }

        // 心去难留：直接移除 NPC 对太吾的【单向】爱慕（原生断情月报无法处理单向，详见文件头说明）。
        // ApplySeverAdore 内部会判定 HasRelation(npc,太吾,16384) 才动手，并记 EndAdored 生平 + 好感/心情结算。
        private static void TryForgetMe(DataContext context, Character npc, int charId, int taiwuId, Character taiwu)
        {
            if (!DreamLoverRules.ShouldForgetUnreciprocatedAdoration(
                    Settings.ForgetMe,
                    Settings.EnableEnamor,
                    Settings.EnablePursued,
                    DomainManager.Character.HasRelation(charId, taiwuId, Adored),
                    DomainManager.Character.HasRelation(charId, taiwuId, Spouse),
                    DomainManager.Character.HasRelation(taiwuId, charId, Adored)))
                return;

            Log(charId + " forget: severing unrequited adoration (direct ApplySeverAdore)");
            Character.ApplySeverAdore(context, npc, taiwu, npc.GetBehaviorType(),
                DomainManager.Character.IsTaiwuPeople(charId));
        }

        private static void RecordNewRegularRelation(DataContext context, Character npc, Character taiwu, ushort relationType, bool succeed)
        {
            var mod = new PeriAdvanceMonthRelationsUpdateModification(npc)
            {
                NewRegularRelations = new List<(Character targetChar, ushort relationType, bool succeed)>
                {
                    (taiwu, relationType, succeed)
                }
            };
            RecordRelationsUpdate(context, mod);
        }

        private static void RecordRelationsUpdate(DataContext context, PeriAdvanceMonthRelationsUpdateModification mod)
        {
            var recorder = context.ParallelModificationsRecorder;
            recorder.RecordType(ParallelModificationType.PeriAdvanceMonthRelationsUpdate);
            recorder.RecordParameterClass(mod);
        }
    }

    // 月报事件门控放行：仅对【本 mod 本月为该 NPC 促成】的那一个恋爱事件(精确 GUID 匹配)、且事件本不通过时放行，
    // 避免硬抛异常；不影响原版自发事件(本就通过，第一行返回)，也不会误放行同组非恋爱事件或该 NPC 当月其它事件。
    [HarmonyPatch(typeof(TaiwuEventItem), "CheckCondition")]
    public static class LoveEventGatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TaiwuEventItem __instance, ref bool __result)
        {
            if (__result)
                return; // 本就通过（原版自发/合格事件）→ 绝不插手
            Guid guid = __instance.Guid;
            if (guid != BeLovePatch.AdoreGuid && guid != BeLovePatch.ConfessGuid && guid != BeLovePatch.MarryGuid)
                return; // 只关心本 mod 促成的三类恋爱事件
            if (__instance.ArgBox == null)
                return;

            Character npc = __instance.ArgBox.GetCharacter("MonthlyEvent_arg0");
            if (npc == null)
                return;

            // 仅当本 mod 本月确实为该 NPC 促成了【这一个】事件时才放行（GUID 精确匹配）。
            if (BeLovePatch.ForcedThisMonth.TryGetValue(npc.GetId(), out Guid promoted) && promoted == guid)
                __result = true;
        }
    }
}
