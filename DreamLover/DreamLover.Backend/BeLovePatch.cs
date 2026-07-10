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
using GameData.Domains.Organization;
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
    // 心去难留(断情)：原生断情【交互月报事件】(47 → 63a3c0e9)的 OnCheckEventCondition 是 CanEndRelation(16384)，要求
    // 【双向】爱慕；而本功能针对【单向】未被回应的爱慕(太吾不爱该 NPC)，该事件条件恒 false，即便强行放行其两个选项也都
    // 不会真正移除单向 16384(一个会按好感重新加回、一个因要求太吾也爱慕而提前返回)。故断情【不走原生交互事件】，改为直接
    // Character.ApplySeverAdore(移除 npc→太吾 16384 + 记 EndAdored 生平 + 好感/心情结算)，并补一条原生断情【月报通知】
    // (MonthlyNotificationCollection.AddSeverLove，recordType 28，与原生双向分手同款)让玩家在月报「人情往来」看到。
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

            // 【性能①】全功能关闭：整月对每个 NPC 直接返回，零额外开销。
            bool anyEnabled = Settings.EnableEnamor || Settings.EnablePursued || Settings.EnableMarry;
            if (!anyEnabled && !Settings.ForgetMe)
                return true;

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0)
                return true;

            int charId = __instance.GetId();
            if (charId == taiwuId)
                return true;

            if (!DomainManager.Character.IsCharacterAlive(charId))
                return true;

            // 不对游戏为剧情机制生成的【特殊 NPC】生效（固定/剧情角色、敌人模板、临时奇遇/事件角色）——
            // 这些连断情也不碰。只作用于普通生成的凡人 NPC(CreatingType==1 且非临时)。
            if (!DreamLoverRules.IsOrdinaryRelationshipTarget(
                    __instance.GetCreatingType(),
                    DomainManager.Character.IsTemporaryIntelligentCharacter(charId)))
            {
                if (Settings.DebugMode)
                    Log(charId + " skipped: special/story or temporary character (CreatingType=" + __instance.GetCreatingType() + ")");
                return true;
            }

            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out Character taiwu))
                return true;

            // 心去难留是【全局】清理(与性别/距离无关)，放在下面的廉价预门之前执行。
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
            // 廉价门先行（无关系查询）：性别 / 追求范围 / 年龄——挡掉绝大多数 NPC，再做较重的好感查询（性能②）。
            // 性别按生理性别(GetGender)；男生女相/女生男相 = 生理性别 + 变装(显示性别≠生理性别)。
            sbyte npcGender = npc.GetGender();
            bool npcIsTransgender = npc.GetDisplayingGender() != npcGender;
            if (!DreamLoverRules.PassGenderRangeAge(
                    Settings.AcceptSameGender,
                    Settings.AcceptOppositeGender,
                    Settings.AcceptMale,
                    Settings.AcceptFemale,
                    Settings.AcceptMaleLooksFemale,
                    Settings.AcceptFemaleLooksMale,
                    npcGender,
                    taiwu.GetGender(),
                    npcIsTransgender,
                    Settings.Range,
                    npc.IsInTaiwuGroup(),
                    CharacterUtils.IsAtSameLocation(npc, taiwu),
                    CharacterUtils.IsAtSameArea(npc, taiwu),
                    Settings.EnableAgeFilter,
                    npc.GetCurrAge(),
                    Settings.MinAge,
                    Settings.MaxAge))
            {
                if (Settings.DebugMode) Log(charId + " filtered: gender/range/age");
                return false;
            }

            // 较重门：好感/立场/魅力/阶层/入魔 档位（含一次好感关系查询）。
            if (!DreamLoverRules.PassTiers(
                    Settings.EnableFavorFilter,
                    CharacterUtils.GetFavorabilityType(charId, taiwuId),
                    Settings.FavorTiers,
                    Settings.EnableStanceFilter,
                    CharacterUtils.GetGoodnessLevel(npc),
                    Settings.GoodTiers,
                    Settings.EnableCharmFilter,
                    CharacterUtils.GetCharmLevel(npc),
                    Settings.CharmTiers,
                    Settings.EnableRankFilter,
                    CharacterUtils.GetRankLevel(npc),
                    Settings.RankTiers,
                    Settings.RankAutoMin,
                    DomainManager.World.GetXiangshuLevel(),
                    Settings.EnableInfectFilter,
                    CharacterUtils.GetInfectionState(npc),
                    Settings.InfectTiers))
            {
                if (Settings.DebugMode) Log(charId + " filtered: tier filter");
                return false;
            }

            if (Settings.EnableRelationFilter && !PassRelationFilter(charId, taiwuId))
            {
                if (Settings.DebugMode) Log(charId + " filtered: relation filter");
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
            // 一夫一妻/血亲等硬性规则由原生 AllowAddingHusbandOrWifeRelation 统一把关（已婚NPC、太吾重婚都在此被拒）。
            // 曾有 MarriedKiller/Polygynous 两开关想绕过它，但保留了硬规则→永不生效（死开关），已移除。见 BACKLOG F1。
            if (!RelationTypeHelper.AllowAddingHusbandOrWifeRelation(charId, taiwuId))
            {
                Log(charId + " marriage blocked: formal marriage rules reject it");
                return false;
            }
            // 门派/出家属"决策层软规则"：落地层 ApplyBecomeHusbandOrWife 只复查【已婚+血亲】这条硬规则，不看门派/出家，
            // 所以下面三个开关都能真正放行原本被门派/出家禁止的婚事。原生 OrgAndMonkTypeAllowMarriage
            // =「非出家 且 门派允许成家(ChildGrade>=0)」是复合判断，会让 MonkKiller 被 IgnoreGang 盖住；
            // 这里拆成正交三条，每个开关只管自己那条、互不影响：
            //   IgnoreGang    → 只解除【门派禁婚】(ChildGrade<0)，出家与否不管
            //   MonkKiller    → 只解除【出家NPC】，门派规章不管
            //   CharmingBonze → 只解除【太吾自身出家】
            if (!Settings.IgnoreGang &&
                OrganizationDomain.GetOrgMemberConfig(npc.GetOrganizationInfo()).ChildGrade < 0)
            {
                Log(charId + " marriage blocked: NPC 门派禁婚(ChildGrade<0)");
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
            // 陌生人（与太吾无任何关系记录）：原生「心生爱慕」事件条件会用不安全的 GetRelation(npc,太吾) 直接取，
            // 无记录会抛 KeyNotFoundException → 卡死过月。默认跳过；开「允许陌生人爱慕」则在同一条修改里先记
            // NewlyMetCharacters，让 complement 用原生 TryCreateGeneralRelation 建立"相识"关系后再爱慕。
            bool hasRelation = DomainManager.Character.TryGetRelation(charId, taiwuId, out _);
            if (!hasRelation && !Settings.AllowStranger)
            {
                Log(charId + " enamor skipped: 与太吾无关系记录且未开启「允许陌生人爱慕」");
                return false;
            }
            // 保留 740/741 特殊恋爱标记等硬约束（非好感门槛）。
            if (!RelationTypeHelper.AllowAddingAdoredRelation(charId, taiwuId))
            {
                Log(charId + " enamor blocked: formal adore rules reject it");
                return false;
            }

            Log(charId + " enamor: recording 爱慕(心生爱慕) monthly update" + (hasRelation ? "" : " (+先建立相识)"));
            RecordNewRegularRelation(context, npc, taiwu, Adored, false, meetFirst: !hasRelation);
            Force(charId, AdoreGuid);
            return true;
        }

        // 心去难留：直接移除 NPC 对太吾的【单向】爱慕（原生断情【交互月报事件】无法处理单向，详见文件头说明）。
        // ApplySeverAdore 内部会判定 HasRelation(npc,太吾,16384) 才动手，并记 EndAdored 生平 + 好感/心情结算。
        // 另补一条原生「断情」月报【通知】(MonthlyNotificationCollection.AddSeverLove，recordType 28，与原生双向分手
        // ApplyBreakupWithBoyOrGirlFriend 同款)，让玩家在月报「人情往来」看到，而非静默。
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

            Log(charId + " forget: severing unrequited adoration (direct ApplySeverAdore + 月报通知)");
            Character.ApplySeverAdore(context, npc, taiwu, npc.GetBehaviorType(),
                DomainManager.Character.IsTaiwuPeople(charId));
            DomainManager.World.GetMonthlyNotificationCollection()
                .AddSeverLove(charId, npc.GetLocation(), taiwuId);
        }

        private static void RecordNewRegularRelation(DataContext context, Character npc, Character taiwu, ushort relationType, bool succeed, bool meetFirst = false)
        {
            var mod = new PeriAdvanceMonthRelationsUpdateModification(npc)
            {
                NewRegularRelations = new List<(Character targetChar, ushort relationType, bool succeed)>
                {
                    (taiwu, relationType, succeed)
                }
            };
            // 陌生人先"相识"：complement 会先对 NewlyMetCharacters 调原生 TryCreateGeneralRelation（串行、幂等、双向），
            // 再处理爱慕并排队「心生爱慕」事件——过月校验事件条件时关系记录已存在，不再空引用崩溃。
            if (meetFirst)
                mod.NewlyMetCharacters = new List<Character> { taiwu };
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
