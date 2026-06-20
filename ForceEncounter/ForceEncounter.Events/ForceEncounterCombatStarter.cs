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

            if (ShouldApplyAlertnessOnCombatStart(modId))
            {
                EventHelper.ChangeAlertnessOnAttack(targetId);
            }

            if (!ForceEncounterEventRuntime.IsTaiwuVillager(targetId) &&
                DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                EventHelper.HasGuard(target))
            {
                List<int> enemyTeam = EventHelper.PrepareCombatEnemy(targetId, CombatConfig.DefKey.DieNormal, false);
                if (enemyTeam.Count > 0 && enemyTeam[0] != targetId)
                {
                    argBox.Set(ForceEncounterConstants.ArgBox.NativePartner, enemyTeam[0]);
                    return ForceEncounterEventIds.事件.护卫出面;
                }

                EventHelper.StartCombat(enemyTeam, CombatConfig.DefKey.DieNormal, ForceEncounterEventIds.事件.战斗反馈, argBox);
            }
            else
            {
                if (EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal))
                {
                    StoreDirectFallenFailure(argBox, modId);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                EventHelper.StartCombat(
                    targetId,
                    CombatConfig.DefKey.DieNormal,
                    ForceEncounterEventIds.事件.战斗反馈,
                    argBox,
                    true);
            }

            return string.Empty;
        }

        private static void StoreDirectFallenFailure(EventArgBox argBox, string modId)
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
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
            }

            ForceEncounterEventRuntime.StoreCombatSettlement(
                argBox,
                ok,
                succeeded,
                targetIsTaiwuVillager,
                ForceEncounterConstants.Reasons.TargetDirectFallen);
        }

        private static bool ShouldApplyAlertnessOnCombatStart(string modId)
        {
            bool applyAlertness = true;
            return string.IsNullOrEmpty(modId) ||
                   !DomainManager.Mod.GetSetting(modId, ForceEncounterConstants.Settings.ApplyAlertnessOnCombatStart, ref applyAlertness) ||
                   applyAlertness;
        }

        public static string StartGuardCombat(EventArgBox argBox)
        {
            int partnerId = -1;
            if (argBox != null && argBox.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId))
            {
                EventHelper.StartCombat(
                    partnerId,
                    CombatConfig.DefKey.DieNormal,
                    ForceEncounterEventIds.事件.战斗反馈,
                    argBox,
                    true);
            }

            return string.Empty;
        }
    }
}
