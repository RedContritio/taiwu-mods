using System.Collections.Generic;
using Config;
using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Combat;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.EventHelper;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterCombatStarter
    {
        public static string StartForcedCombat(EventArgBox argBox, string modId)
        {
            if (!ForceEncounterEventRuntime.TryGetTargetId(argBox, out int targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            if (ForceEncounterEventRuntime.ShouldUseGuardInterceptionForTarget(targetId, modId) &&
                DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                EventHelper.HasGuard(target))
            {
                List<int> enemyTeam = EventHelper.PrepareCombatEnemy(targetId, CombatConfig.DefKey.DieNormal, false);
                if (enemyTeam.Count == 0)
                {
                    StoreCombatFailure(argBox, modId, ForceEncounterConstants.Reasons.GuardIntercepted);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                if (enemyTeam[0] != targetId)
                {
                    argBox.Set(ForceEncounterConstants.ArgBox.NativePartner, enemyTeam[0]);
                    argBox.Set(ForceEncounterConstants.ArgBox.GuardInterceptActive, true);
                    return ForceEncounterEventIds.事件.护卫出面;
                }

                if (EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal))
                {
                    StoreDirectFallenFailure(argBox, modId);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                ApplyAlertnessOnTargetCombatStart(targetId, modId);
                EventHelper.StartCombat(enemyTeam, CombatConfig.DefKey.DieNormal, ForceEncounterEventIds.事件.战斗反馈, argBox);
            }
            else
            {
                if (EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal))
                {
                    StoreDirectFallenFailure(argBox, modId);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                ApplyAlertnessOnTargetCombatStart(targetId, modId);
                EventHelper.StartCombat(
                    targetId,
                    CombatConfig.DefKey.DieNormal,
                    ForceEncounterEventIds.事件.战斗反馈,
                    argBox,
                    true);
            }

            return string.Empty;
        }

        private static void ApplyAlertnessOnTargetCombatStart(int targetId, string modId)
        {
            if (ShouldApplyAlertnessOnCombatStart(modId))
            {
                EventHelper.ChangeAlertnessOnAttack(targetId);
            }
        }

        private static void StoreDirectFallenFailure(EventArgBox argBox, string modId)
        {
            StoreCombatFailure(argBox, modId, ForceEncounterConstants.Reasons.TargetDirectFallen);
        }

        private static void StoreCombatFailure(EventArgBox argBox, string modId, string reason)
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(argBox, out int actorId, out int targetId))
            {
                ForceEncounterEventRuntime.StoreCombatSettlement(
                    argBox,
                    false,
                    false,
                    false,
                    ForceEncounterConstants.Reasons.MissingActorOrTarget);
                return;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallCombatBackend(
                modId,
                actorId,
                targetId,
                battleSucceeded: false);

            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
                result.Get(ForceEncounterConstants.Response.AppliedEnmity, out appliedEnmity);
            }

            ForceEncounterEventRuntime.StoreCombatSettlement(
                argBox,
                ok,
                succeeded,
                targetIsTaiwuVillager,
                reason,
                appliedEnmity);
        }

        private static bool ShouldApplyAlertnessOnCombatStart(string modId)
        {
            return ForceEncounterSettings.GetBool(modId, ForceEncounterConstants.Settings.ApplyAlertnessOnCombatStart, true);
        }

        public static string StartGuardCombat(EventArgBox argBox)
        {
            int partnerId = -1;
            if (argBox != null && argBox.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId))
            {
                argBox.Set(ForceEncounterConstants.ArgBox.GuardInterceptActive, true);
                EventHelper.StartCombat(
                    partnerId,
                    CombatConfig.DefKey.DieNormal,
                    ForceEncounterEventIds.事件.战斗反馈,
                    argBox,
                    true);

                return string.Empty;
            }

            StoreCombatFailure(
                argBox,
                ForceEncounterEventRuntime.GetRuntimeModId(ForceEncounterEventIds.事件.战斗反馈),
                ForceEncounterConstants.Reasons.GuardIntercepted);
            return ForceEncounterEventIds.事件.战斗反馈;
        }
    }
}
