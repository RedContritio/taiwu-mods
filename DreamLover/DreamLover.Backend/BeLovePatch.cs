using System.Collections.Concurrent;
using System.Collections.Generic;
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

            if (Settings.EnableMarry && TryMarriage(context, charId, __instance, taiwu))
                return true;

            if (Settings.EnablePursued && TryConfession(context, charId, __instance, taiwu))
                return true;

            if (Settings.EnableEnamor && TryEnamor(context, charId, __instance, taiwu))
                return true;

            return true;
        }

        private static void TryForgetMe(DataContext context, Character npc, int charId, int taiwuId)
        {
            if (!DomainManager.Character.HasRelation(charId, taiwuId, Adored))
                return;
            if (DomainManager.Character.HasRelation(charId, taiwuId, Spouse))
                return;
            if (DomainManager.Character.HasRelation(taiwuId, charId, Adored))
                return;

            Log(charId + " queued to forget adoration for Taiwu");
            ForgetMeQueue.Enqueue(charId);
            RecordEmptyRelationsUpdate(context, npc);
        }

        private static bool PassFilters(Character npc, int charId, int taiwuId, Character taiwu)
        {
            if (!Settings.AcceptSameGender && npc.GetGender() == taiwu.GetGender())
                return false;

            if (!Settings.IgnoreDistance && !CharacterUtils.IsAtSameLocation(npc, taiwu))
                return false;

            int ageYears = npc.GetCurrAge();
            if (ageYears < Settings.MinAge || ageYears > Settings.MaxAge)
            {
                Log(charId + " filtered: age " + ageYears);
                return false;
            }

            sbyte favorType = CharacterUtils.GetFavorabilityType(charId, taiwuId);
            int favorIdx = favorType + 6;
            if (favorIdx < 0 || favorIdx >= Settings.Favor.Length || !Settings.Favor[favorIdx])
            {
                Log(charId + " filtered: favorability type " + favorType);
                return false;
            }

            int goodnessLevel = CharacterUtils.GetGoodnessLevel(npc);
            if (goodnessLevel < 0 || goodnessLevel >= Settings.Good.Length || !Settings.Good[goodnessLevel])
            {
                Log(charId + " filtered: goodness level " + goodnessLevel);
                return false;
            }

            sbyte charmLevel = CharacterUtils.GetCharmLevel(npc);
            if (charmLevel < 0 || charmLevel >= Settings.Charm.Length || !Settings.Charm[charmLevel])
            {
                Log(charId + " filtered: charm level " + charmLevel);
                return false;
            }

            sbyte rankLevel = CharacterUtils.GetRankLevel(npc);
            if (rankLevel >= 0 && rankLevel < Settings.Rank.Length && !Settings.Rank[rankLevel])
            {
                Log(charId + " filtered: rank level " + rankLevel);
                return false;
            }

            int infectState = CharacterUtils.GetInfectionState(npc);
            if (infectState >= 0 && infectState < Settings.Infect.Length && !Settings.Infect[infectState])
            {
                Log(charId + " filtered: infection state " + infectState);
                return false;
            }

            if (!PassRelationFilter(charId, taiwuId))
            {
                Log(charId + " filtered: blocked relation type");
                return false;
            }

            return true;
        }

        private static bool PassRelationFilter(int charId, int taiwuId)
        {
            if (!DomainManager.Character.TryGetRelation(taiwuId, charId, out RelatedCharacter rel))
                return true;

            for (int i = 0; i < Settings.RelFilterDefs.Length; i++)
            {
                ushort relType = Settings.RelFilterDefs[i].Type;
                if (RelationType.HasRelation(rel.RelationType, relType) && !Settings.RelFilter[i])
                    return false;
            }
            return true;
        }

        private static bool TryMarriage(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
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

            Log(charId + " marriage: recording monthly relation update");
            RecordNewRegularRelation(context, npc, taiwu, Spouse, true);
            return true;
        }

        private static bool TryConfession(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
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

            Log(charId + " confession: recording monthly relation update");
            var mod = new PeriAdvanceMonthRelationsUpdateModification(npc)
            {
                NewBoyOrGirlFriend = (targetChar: taiwu, succeed: true)
            };
            RecordRelationsUpdate(context, mod);
            return true;
        }

        private static bool TryEnamor(DataContext context, int charId, Character npc, Character taiwu)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
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

            Log(charId + " enamor: recording monthly relation update");
            RecordNewRegularRelation(context, npc, taiwu, Adored, false);
            return true;
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

        private static void RecordEmptyRelationsUpdate(DataContext context, Character npc)
        {
            RecordRelationsUpdate(context, new PeriAdvanceMonthRelationsUpdateModification(npc));
        }

        private static void RecordRelationsUpdate(DataContext context, PeriAdvanceMonthRelationsUpdateModification mod)
        {
            var recorder = context.ParallelModificationsRecorder;
            recorder.RecordType(ParallelModificationType.PeriAdvanceMonthRelationsUpdate);
            recorder.RecordParameterClass(mod);
        }

        internal static void DrainForgetMeQueue(DataContext context)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0 || !DomainManager.Character.TryGetElement_Objects(taiwuId, out Character taiwu))
                return;

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
            BeLovePatch.DrainForgetMeQueue(context);
        }
    }
}
