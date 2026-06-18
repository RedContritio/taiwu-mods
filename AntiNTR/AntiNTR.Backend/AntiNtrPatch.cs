using System.Collections.Generic;
using System.Reflection;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.ParallelModifications;
using GameData.Domains.Character.Relation;
using GameData.Utilities;
using HarmonyLib;
using Redzen.Random;

namespace AntiNTR.Backend
{
    [HarmonyPatch]
    public static class AntiNtrPatch
    {
        private static readonly (string Key, ushort Type)[] RelationSettings =
        {
            ("Rel_Friend", 8192),
            ("Rel_BloodBrotherOrSister", 4),
            ("Rel_BloodParent", 1),
            ("Rel_BloodChild", 2),
            ("Rel_StepParent", 8),
            ("Rel_StepChild", 16),
            ("Rel_StepBrotherOrSister", 32),
            ("Rel_AdoptiveParent", 64),
            ("Rel_AdoptiveChild", 128),
            ("Rel_AdoptiveBrotherOrSister", 256),
            ("Rel_Mentor", 2048),
            ("Rel_Mentee", 4096),
            ("Rel_SwornBrotherOrSister", 512),
            ("Rel_Enemy", 32768),
            ("Rel_Adored", 16384),
            ("Rel_HusbandOrWife", 1024),
        };

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Character), "OfflineMakeLove",
                new[] { typeof(IRandomSource), typeof(Character), typeof(Character), typeof(bool) });
        }

        [HarmonyPrefix]
        public static bool Prefix(Character father, Character mother, ref bool __result)
        {
            var plugin = BackendPlugin.Instance;
            if (plugin == null || !plugin.GetBoolSetting("Enabled"))
                return true;

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0)
                return true;

            int fatherId = father.GetId();
            int motherId = mother.GetId();

            if (ShouldBlockPair(plugin, taiwuId, fatherId, motherId))
            {
                __result = false;
                return false;
            }

            return true;
        }

        internal static bool ShouldBlockPair(BackendPlugin plugin, int taiwuId, int charAId, int charBId)
        {
            if (plugin == null || !plugin.GetBoolSetting("Enabled"))
                return false;
            if (taiwuId < 0)
                return false;
            if (charAId == taiwuId || charBId == taiwuId)
                return false;

            if (plugin.GetBoolSetting("Rel_PreventAll"))
            {
                Log(plugin, charAId, charBId, "PreventAll");
                return true;
            }

            bool allowCouple = plugin.GetBoolSetting("AllowCouple");
            return ShouldBlock(plugin, taiwuId, charAId, charBId, allowCouple) ||
                   ShouldBlock(plugin, taiwuId, charBId, charAId, allowCouple);
        }

        private static bool ShouldBlock(BackendPlugin plugin, int taiwuId, int charId, int partnerId, bool allowCouple)
        {
            HashSet<int> spouseIds = DomainManager.Character.GetRelatedCharIds(charId, 1024);
            foreach (int spouseId in spouseIds)
            {
                if (!DomainManager.Character.IsCharacterAlive(spouseId))
                    continue;
                if (!IsProtected(plugin, taiwuId, spouseId))
                    continue;

                if (allowCouple && partnerId == spouseId)
                    continue;

                Log(plugin, charId, partnerId, $"spouse {spouseId} is protected");
                return true;
            }
            return false;
        }

        private static bool IsProtected(BackendPlugin plugin, int taiwuId, int charId)
        {
            if (charId == taiwuId)
                return true;

            foreach (var (key, relType) in RelationSettings)
            {
                if (!plugin.GetBoolSetting(key))
                    continue;

                if (HasRelation(taiwuId, charId, relType) || HasRelation(charId, taiwuId, relType))
                    return true;
            }

            return false;
        }

        private static bool HasRelation(int charId, int relatedCharId, ushort relType)
        {
            return DomainManager.Character.TryGetRelation(charId, relatedCharId, out RelatedCharacter rel) &&
                   RelationType.HasRelation(rel.RelationType, relType);
        }

        private static void Log(BackendPlugin plugin, int charA, int charB, string reason)
        {
            if (!plugin.GetBoolSetting("DebugMode"))
                return;

            AdaptableLog.Info($"[AntiNTR] Blocked: {charA} x {charB}, reason: {reason}");
        }
    }

    [HarmonyPatch(typeof(Character), "ComplementPeriAdvanceMonth_ExecuteFixedActions")]
    public static class AntiNtrFixedActionComplementPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PeriAdvanceMonthFixedActionModification mod)
        {
            var plugin = BackendPlugin.Instance;
            if (plugin == null || !plugin.GetBoolSetting("Enabled"))
                return;
            if (mod.MakeLoveTargetList == null || mod.MakeLoveTargetList.Count == 0)
                return;

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (taiwuId < 0)
                return;

            int selfId = mod.Character.GetId();
            var kept = new List<(Character target, PeriAdvanceMonthFixedActionModification.MakeLoveState makeLoveState, bool isPregnant, bool targetIsFather)>();
            foreach (var item in mod.MakeLoveTargetList)
            {
                int targetId = item.target.GetId();
                if (AntiNtrPatch.ShouldBlockPair(plugin, taiwuId, selfId, targetId))
                    continue;

                kept.Add(item);
            }

            mod.MakeLoveTargetList = kept.Count == 0 ? null : kept;
        }
    }
}
