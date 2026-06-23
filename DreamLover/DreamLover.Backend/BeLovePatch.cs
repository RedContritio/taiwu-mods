using System.Collections.Concurrent;
using HarmonyLib;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Ai;
using GameData.Domains.Character.ParallelModifications;
using GameData.Domains.Character.Relation;
using GameData.Utilities;

namespace DreamLover.Backend
{
    [HarmonyPatch(typeof(Character), "PeriAdvanceMonth_RelationsUpdate")]
    public static class BeLovePatch
    {
        private const ushort Adored = 16384;
        private const ushort Spouse = 1024;

        // NPC 主动对【太吾】发起的关系，必须在月度补全阶段（单线程）直接 Apply。
        // 原因：游戏的 ComplementPeriAdvanceMonth_RelationsUpdate 在“新关系目标是太吾”时，
        // 只会把它转成月报事件（AddAdore/AddConfess/AddProposeMarriage），并【不】真正建立关系；
        // 只有目标非太吾时才直接调用 Apply*。所以记录 NewRegularRelations(target=太吾) 无效，
        // 必须自己在补全阶段直接调用 Character.Apply* 把关系落到太吾身上（与 ForgetMe 的处理方式一致）。
        private enum LoveAction { Enamor, Confession, Marriage }

        private static readonly ConcurrentQueue<(int CharId, LoveAction Action)> ActionQueue
            = new ConcurrentQueue<(int, LoveAction)>();
        private static readonly ConcurrentQueue<int> ForgetMeQueue = new ConcurrentQueue<int>();

        private static void Log(string message)
        {
            if (Settings.DebugMode)
                AdaptableLog.Info("[DreamLover] " + message);
        }

        [HarmonyPrefix]
        public static bool Prefix(Character __instance, DataContext context)
        {
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
                TryForgetMe(context, __instance, charId, taiwuId);

            if (!anyEnabled)
                return true;

            if (!PassFilters(__instance, charId, taiwuId, taiwu))
                return true;

            // 决策顺序：双向爱慕→求婚；单向(npc爱慕)→表白；尚未爱慕→爱慕。
            // 仅做决策与入队，真正的关系变更在补全阶段（DrainQueues）单线程执行。
            if (Settings.EnableMarry && ShouldMarry(charId, __instance, taiwu, taiwuId))
            {
                Enqueue(context, __instance, charId, LoveAction.Marriage);
                return true;
            }

            if (Settings.EnablePursued && ShouldConfess(charId, __instance, taiwu, taiwuId))
            {
                Enqueue(context, __instance, charId, LoveAction.Confession);
                return true;
            }

            if (Settings.EnableEnamor && ShouldEnamor(charId, __instance, taiwu, taiwuId))
            {
                Enqueue(context, __instance, charId, LoveAction.Enamor);
                return true;
            }

            return true;
        }

        private static void Enqueue(DataContext context, Character npc, int charId, LoveAction action)
        {
            Log(charId + " queued: " + action);
            ActionQueue.Enqueue((charId, action));
            // 记录一条空的关系更新，确保补全阶段（ComplementPeriAdvanceMonth_RelationsUpdate）会被调用，
            // 从而 DrainQueues 得以执行。
            RecordEmptyRelationsUpdate(context, npc);
        }

        private static void TryForgetMe(DataContext context, Character npc, int charId, int taiwuId)
        {
            if (!DreamLoverRules.ShouldForgetUnreciprocatedAdoration(
                    Settings.ForgetMe,
                    Settings.EnableEnamor,
                    Settings.EnablePursued,
                    DomainManager.Character.HasRelation(charId, taiwuId, Adored),
                    DomainManager.Character.HasRelation(charId, taiwuId, Spouse),
                    DomainManager.Character.HasRelation(taiwuId, charId, Adored)))
                return;

            Log(charId + " queued to forget adoration for Taiwu");
            ForgetMeQueue.Enqueue(charId);
            RecordEmptyRelationsUpdate(context, npc);
        }

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
                    Settings.Favor,
                    goodnessLevel,
                    Settings.Good,
                    charmLevel,
                    Settings.Charm,
                    rankLevel,
                    Settings.Rank,
                    infectState,
                    Settings.Infect))
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

        // ---------- 决策（worker 线程，只读状态 + 正式规则校验） ----------

        private static bool ShouldMarry(int charId, Character npc, Character taiwu, int taiwuId)
        {
            if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            if (!DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                return false;
            if (DomainManager.Character.HasRelation(charId, taiwuId, Spouse))
                return false;
            if (!RelationTypeHelper.AllowAddingHusbandOrWifeRelation(charId, taiwuId))
            {
                Log(charId + " marriage blocked: formal marriage rules reject it");
                return false;
            }
            if (!DomainManager.Character.TryGetRelation(charId, taiwuId, out RelatedCharacter npcToTaiwu) ||
                !DomainManager.Character.TryGetRelation(taiwuId, charId, out RelatedCharacter taiwuToNpc) ||
                !AiHelper.Relation.CanStartRelation_HusbandOrWife(charId, npcToTaiwu, npc.GetBehaviorType(),
                    taiwuId, taiwuToNpc, taiwu.GetBehaviorType()))
            {
                Log(charId + " marriage blocked: formal relation rules reject it");
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

            return true;
        }

        private static bool ShouldConfess(int charId, Character npc, Character taiwu, int taiwuId)
        {
            if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            if (DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                return false;
            if (!DomainManager.Character.TryGetRelation(charId, taiwuId, out RelatedCharacter npcToTaiwu) ||
                !DomainManager.Character.TryGetRelation(taiwuId, charId, out RelatedCharacter taiwuToNpc) ||
                !AiHelper.Relation.CanStartRelation_BoyOrGirlFriend(npcToTaiwu, npc.GetBehaviorType(),
                    taiwuToNpc, taiwu.GetBehaviorType()))
            {
                Log(charId + " confession blocked: formal relation rules reject it");
                return false;
            }

            return true;
        }

        private static bool ShouldEnamor(int charId, Character npc, Character taiwu, int taiwuId)
        {
            if (DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return false;
            if (!RelationTypeHelper.AllowAddingAdoredRelation(charId, taiwuId))
            {
                Log(charId + " enamor blocked: formal adore rules reject it");
                return false;
            }
            if (!DomainManager.Character.TryGetRelation(charId, taiwuId, out RelatedCharacter npcToTaiwu) ||
                !AiHelper.Relation.CanStartRelation_Adored(npcToTaiwu, npc.GetBehaviorType()))
            {
                Log(charId + " enamor blocked: formal relation rules reject it");
                return false;
            }

            return true;
        }

        private static void RecordEmptyRelationsUpdate(DataContext context, Character npc)
        {
            var mod = new PeriAdvanceMonthRelationsUpdateModification(npc);
            var recorder = context.ParallelModificationsRecorder;
            recorder.RecordType(ParallelModificationType.PeriAdvanceMonthRelationsUpdate);
            recorder.RecordParameterClass(mod);
        }

        // ---------- 应用（补全阶段，单线程，直接落关系到太吾身上） ----------

        internal static void DrainQueues(DataContext context)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0 || !DomainManager.Character.TryGetElement_Objects(taiwuId, out Character taiwu))
                return;

            bool taiwuIsTaiwuPeople = DomainManager.Character.IsTaiwuPeople(taiwuId);

            while (ActionQueue.TryDequeue(out var item))
            {
                if (!DomainManager.Character.TryGetElement_Objects(item.CharId, out Character npc))
                    continue;

                sbyte bt = npc.GetBehaviorType();
                bool npcIsTaiwuPeople = DomainManager.Character.IsTaiwuPeople(item.CharId);

                switch (item.Action)
                {
                    case LoveAction.Enamor:
                        if (DomainManager.Character.HasRelation(item.CharId, taiwuId, Adored))
                            break;
                        Log(item.CharId + " enamor: applying adore for Taiwu");
                        Character.ApplyAddRelation_Adore(context, npc, taiwu, bt, false, npcIsTaiwuPeople, taiwuIsTaiwuPeople);
                        break;

                    case LoveAction.Confession:
                        if (DomainManager.Character.HasRelation(taiwuId, item.CharId, Adored))
                            break;
                        Log(item.CharId + " confession: applying mutual adoration with Taiwu");
                        Character.ApplyBecomeBoyOrGirlFriend(context, npc, taiwu, bt, true, npcIsTaiwuPeople, taiwuIsTaiwuPeople);
                        break;

                    case LoveAction.Marriage:
                        if (DomainManager.Character.HasRelation(item.CharId, taiwuId, Spouse))
                            break;
                        Log(item.CharId + " marriage: applying marriage with Taiwu");
                        Character.ApplyBecomeHusbandOrWife(context, npc, taiwu, bt, true, npcIsTaiwuPeople, taiwuIsTaiwuPeople);
                        break;
                }
            }

            DrainForgetMeQueue(context, taiwuId, taiwu);
        }

        private static void DrainForgetMeQueue(DataContext context, int taiwuId, Character taiwu)
        {
            while (ForgetMeQueue.TryDequeue(out int charId))
            {
                if (!DomainManager.Character.TryGetElement_Objects(charId, out Character npc))
                    continue;
                if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                    continue;
                if (DomainManager.Character.HasRelation(charId, taiwuId, Spouse))
                    continue;
                if (DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                    continue;

                Log(charId + " forgets adoration for Taiwu");
                Character.ApplySeverAdore(context, npc, taiwu,
                    npc.GetBehaviorType(), DomainManager.Character.IsTaiwuPeople(charId));
            }
        }
    }

    [HarmonyPatch(typeof(Character), "ComplementPeriAdvanceMonth_RelationsUpdate")]
    public static class BeLoveComplementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(DataContext context)
        {
            BeLovePatch.DrainQueues(context);
        }
    }
}
