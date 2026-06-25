using System;
using System.Collections;
using System.Reflection;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Combat;
using GameData.Domains.Item;
using HarmonyLib;

namespace EasyBridge.Backend
{
    internal static class CombatPrepareHookUtil
    {
        private static readonly FieldInfo CombatCharField =
            typeof(CombatCharacterStateBase).GetField("CombatChar", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AttackLeftPrepareFrameField =
            typeof(CombatCharacterStatePrepareAttack).GetField("_leftPrepareFrame", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo OtherActionLeftPrepareFrameField =
            typeof(CombatCharacterStatePrepareOtherAction).GetField("_leftPrepareFrame", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo DelayCallListField =
            typeof(CombatCharacterStateBase).GetField("_delayCallList", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo DelayedFramesField =
            typeof(CombatCharacterStateBase.DelayCallData).GetField("DelayedFrames");

        private static readonly FieldInfo TotalDelayFramesField =
            typeof(CombatCharacterStateBase.DelayCallData).GetField("TotalDelayFrames");

        public static CombatCharacter GetCombatChar(object state)
        {
            try { return CombatCharField?.GetValue(state) as CombatCharacter; }
            catch { return null; }
        }

        public static DataContext GetContext(CombatCharacter ch)
        {
            try { return ch?.GetDataContext(); }
            catch { return null; }
        }

        public static int GetIntField(FieldInfo field, object obj, int fallback)
        {
            try { return field != null ? Convert.ToInt32(field.GetValue(obj)) : fallback; }
            catch { return fallback; }
        }

        public static bool TryBreakAttack(object state)
        {
            var ch = GetCombatChar(state);
            int left = GetIntField(AttackLeftPrepareFrameField, state, int.MaxValue);
            if (ch == null || left > 1)
                return true;
            return CombatStepper.TryBreakBeforeCommit(GetContext(ch), ch, "attackBeforeCommit", 0, 100);
        }

        public static bool TryBreakOtherAction(object state)
        {
            var ch = GetCombatChar(state);
            if (ch == null)
                return true;
            sbyte action;
            try { action = ch.GetPreparingOtherAction(); }
            catch { return true; }
            int left = GetIntField(OtherActionLeftPrepareFrameField, state, int.MaxValue);
            if (action < 0 || action == 4 || left > 1)
                return true;
            return CombatStepper.TryBreakBeforeCommit(GetContext(ch), ch, "otherActionBeforeCommit", action, 100);
        }

        public static bool TryBreakSkill(object state)
        {
            var ch = GetCombatChar(state);
            if (ch == null)
                return true;

            short skillId;
            try { skillId = ch.GetPreparingSkillId(); }
            catch { return true; }
            if (skillId < 0 || ch.SkillPrepareTotalProgress <= 0)
                return true;

            int speed;
            try { speed = DomainManager.Combat.GetSkillPrepareSpeed(ch); }
            catch { return true; }

            if (ch.SkillPrepareCurrProgress + speed < ch.SkillPrepareTotalProgress)
                return true;
            return CombatStepper.TryBreakBeforeCommit(GetContext(ch), ch, "skillBeforeCommit", skillId, 100);
        }

        public static bool TryBreakUseItem(object state)
        {
            var ch = GetCombatChar(state);
            if (ch == null)
                return true;

            ItemKey item;
            try { item = ch.GetPreparingItem(); }
            catch { return true; }
            if (!item.IsValid())
                return true;

            if (!NextDelayCallWillTick(state))
                return true;
            return CombatStepper.TryBreakBeforeCommit(GetContext(ch), ch, "useItemBeforeCommit", item.TemplateId, 100);
        }

        private static bool NextDelayCallWillTick(object state)
        {
            try
            {
                var queue = DelayCallListField?.GetValue(state);
                if (queue == null)
                    return false;
                object next = null;
                var peek = queue.GetType().GetMethod("Peek", BindingFlags.Instance | BindingFlags.Public);
                if (peek != null)
                    next = peek.Invoke(queue, Array.Empty<object>());
                else if (queue is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        next = item;
                        break;
                    }
                }
                if (next == null)
                    return false;

                int delayed = Convert.ToInt32(DelayedFramesField.GetValue(next));
                int total = Convert.ToInt32(TotalDelayFramesField.GetValue(next));
                return delayed + 1 >= total;
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(CombatCharacterStatePrepareAttack), "OnUpdate")]
    internal static class CombatPrepareAttackOnUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CombatCharacterStatePrepareAttack __instance)
            => CombatPrepareHookUtil.TryBreakAttack(__instance);
    }

    [HarmonyPatch(typeof(CombatCharacterStatePrepareSkill), "OnUpdate")]
    internal static class CombatPrepareSkillOnUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CombatCharacterStatePrepareSkill __instance)
            => CombatPrepareHookUtil.TryBreakSkill(__instance);
    }

    [HarmonyPatch(typeof(CombatCharacterStatePrepareOtherAction), "OnUpdate")]
    internal static class CombatPrepareOtherActionOnUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CombatCharacterStatePrepareOtherAction __instance)
            => CombatPrepareHookUtil.TryBreakOtherAction(__instance);
    }

    [HarmonyPatch(typeof(CombatCharacterStatePrepareUseItem), "OnUpdate")]
    internal static class CombatPrepareUseItemOnUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CombatCharacterStatePrepareUseItem __instance)
            => CombatPrepareHookUtil.TryBreakUseItem(__instance);
    }
}
