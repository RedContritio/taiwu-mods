using System.Collections.Generic;
using Config;
using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Combat;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.EventHelper;
using TaiwuMod.Common;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterCombatStarter
    {
        public static string StartForcedCombat(EventArgBox argBox, string modId)
        {
            if (!ForceEncounterEventRuntime.TryGetTargetId(argBox, out int targetId))
            {
                ForceEncounterEventRuntime.DebugLog(modId, "StartForcedCombat failed: missing target");
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            bool useGuardInterception = ForceEncounterEventRuntime.ShouldUseGuardInterceptionForTarget(targetId, modId);
            bool targetIsTaiwuVillager = ForceEncounterEventRuntime.IsTaiwuVillager(targetId);
            bool hasGuard = DomainManager.Character.TryGetElement_Objects(targetId, out var targetForLog) &&
                            EventHelper.HasGuard(targetForLog);
            ForceEncounterEventRuntime.DebugLog(
                modId,
                "StartForcedCombat target=" + targetId +
                ", useGuardInterception=" + useGuardInterception +
                ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                ", hasGuard=" + hasGuard +
                ", applyAlertness=" + ShouldApplyAlertnessOnCombatStart(modId));

            if (TargetIsAlreadyPrisonerOfActor(argBox, targetId, out int actorId))
            {
                ForceEncounterEventRuntime.DebugLog(
                    modId,
                    "Target already prisoner before forced combat actor=" + actorId +
                    ", target=" + targetId);
                StoreCombatSettlement(argBox, modId, ForceEncounterConstants.Reasons.TargetAlreadyPrisoner, battleSucceeded: true);
                return ForceEncounterEventIds.事件.战斗反馈;
            }

            if (useGuardInterception &&
                DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                EventHelper.HasGuard(target))
            {
                List<int> enemyTeam = EventHelper.PrepareCombatEnemy(targetId, CombatConfig.DefKey.DieNormal, false);
                argBox.Set(ForceEncounterConstants.ArgBox.GuardTeamPrepared, true);
                ForceEncounterEventRuntime.DebugLog(
                    modId,
                    "Guard preparation target=" + targetId +
                    ", enemyTeamCount=" + enemyTeam.Count +
                    ", firstEnemy=" + (enemyTeam.Count > 0 ? enemyTeam[0] : -1));
                if (enemyTeam.Count == 0)
                {
                    StoreCombatFailure(argBox, modId, ForceEncounterConstants.Reasons.GuardIntercepted);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                if (enemyTeam[0] != targetId)
                {
                    argBox.Set(ForceEncounterConstants.ArgBox.NativePartner, enemyTeam[0]);
                    argBox.Set(ForceEncounterConstants.ArgBox.GuardInterceptActive, true);
                    ForceEncounterEventRuntime.DebugLog(
                        modId,
                        "Guard intercept active target=" + targetId +
                        ", guard=" + enemyTeam[0] +
                        ", nextEvent=" + ForceEncounterEventIds.事件.护卫出面);
                    return ForceEncounterEventIds.事件.护卫出面;
                }

                if (EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal))
                {
                    ForceEncounterEventRuntime.DebugLog(modId, "Target direct fallen before guarded combat target=" + targetId);
                    StoreDirectFallenSuccess(argBox, modId);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                ApplyAlertnessOnTargetCombatStart(targetId, modId);
                ForceEncounterEventRuntime.DebugLog(modId, "Start guarded target combat target=" + targetId);
                EventHelper.StartCombat(enemyTeam, CombatConfig.DefKey.DieNormal, ForceEncounterEventIds.事件.战斗反馈, argBox);
            }
            else
            {
                if (EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal))
                {
                    ForceEncounterEventRuntime.DebugLog(modId, "Target direct fallen before direct combat target=" + targetId);
                    StoreDirectFallenSuccess(argBox, modId);
                    return ForceEncounterEventIds.事件.战斗反馈;
                }

                ApplyAlertnessOnTargetCombatStart(targetId, modId);
                ForceEncounterEventRuntime.DebugLog(modId, "Start direct target combat target=" + targetId);
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
                ForceEncounterEventRuntime.DebugLog(modId, "ApplyAlertnessOnCombatStart target=" + targetId);
                EventHelper.ChangeAlertnessOnAttack(targetId);
            }
            else
            {
                ForceEncounterEventRuntime.DebugLog(modId, "ApplyAlertnessOnCombatStart skipped target=" + targetId);
            }
        }

        private static void StoreDirectFallenSuccess(EventArgBox argBox, string modId)
        {
            StoreCombatSettlement(argBox, modId, ForceEncounterConstants.Reasons.TargetDirectFallen, battleSucceeded: true);
        }

        private static void StoreCombatFailure(EventArgBox argBox, string modId, string reason)
        {
            StoreCombatSettlement(argBox, modId, reason, battleSucceeded: false);
        }

        private static void StoreCombatSettlement(EventArgBox argBox, string modId, string reason, bool battleSucceeded)
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(argBox, out int actorId, out int targetId))
            {
                ForceEncounterEventRuntime.DebugLog(
                    modId,
                    "StoreCombatSettlement missing actor/target reason=" + reason +
                    ", battleSucceeded=" + battleSucceeded);
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
                battleSucceeded);

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
            ForceEncounterEventRuntime.DebugLog(
                modId,
                "StoreCombatSettlement actor=" + actorId +
                ", target=" + targetId +
                ", ok=" + ok +
                ", succeeded=" + succeeded +
                ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                ", appliedEnmity=" + appliedEnmity +
                ", reason=" + reason +
                ", battleSucceeded=" + battleSucceeded);
        }

        private static bool ShouldApplyAlertnessOnCombatStart(string modId)
        {
            return TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.ApplyAlertnessOnCombatStart, true);
        }

        private static bool TargetIsAlreadyPrisonerOfActor(EventArgBox argBox, int targetId, out int actorId)
        {
            actorId = DomainManager.Taiwu.GetTaiwuCharId();
            if (argBox != null)
            {
                argBox.Get(ForceEncounterEventIds.参数.行为者, ref actorId);
            }

            return DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                   target.GetKidnapperId() == actorId;
        }

        public static string StartGuardCombat(EventArgBox argBox)
        {
            int partnerId = -1;
            if (argBox != null && argBox.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId))
            {
                argBox.Set(ForceEncounterConstants.ArgBox.GuardInterceptActive, true);
                string modId = ForceEncounterEventRuntime.GetRuntimeModId(ForceEncounterEventIds.事件.战斗反馈);
                ForceEncounterEventRuntime.DebugLog(modId, "StartGuardCombat guard=" + partnerId);
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
