using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.EventHelper;
using TaiwuMod.Common;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventRuntime
    {
        public static string GetRuntimeModId(TaiwuEventItem eventItem)
        {
            return eventItem.Package?.ModIdString ?? string.Empty;
        }

        public static string GetRuntimeModId(string eventGuid)
        {
            return DomainManager.TaiwuEvent.GetEvent(eventGuid)?.EventConfig?.Package?.ModIdString ?? string.Empty;
        }

        public static int GetActorId()
        {
            return DomainManager.Taiwu.GetTaiwuCharId();
        }

        public static bool TryGetTargetId(EventArgBox argBox, out int targetId)
        {
            targetId = -1;
            return argBox != null &&
                   (argBox.Get(ForceEncounterEventIds.参数.目标, ref targetId) ||
                    argBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId));
        }

        public static bool TryGetActorAndTarget(EventArgBox argBox, out int actorId, out int targetId)
        {
            actorId = GetActorId();
            if (argBox != null)
            {
                argBox.Get(ForceEncounterEventIds.参数.行为者, ref actorId);
            }

            return TryGetTargetId(argBox, out targetId);
        }

        public static void StoreInteractionArgs(EventArgBox argBox, int actorId, int targetId)
        {
            argBox.Set(ForceEncounterEventIds.参数.行为者, actorId);
            argBox.Set(ForceEncounterEventIds.参数.目标, targetId);
            argBox.Set(ForceEncounterEventIds.参数.未成年, 需要未成年提示(actorId, targetId));
        }

        public static void ConfirmOuterWaitOption(EventArgBox argBox)
        {
            argBox?.Set(ForceEncounterConstants.WaitConfirm.ConfirmSignal, ForceEncounterConstants.WaitConfirm.OuterPreview);
        }

        public static bool 需要未成年提示(int actorId, int targetId)
        {
            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return false;
            }

            return actor.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组 ||
                   target.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组;
        }

        public static bool 是未成年分支(EventArgBox argBox)
        {
            bool 未成年 = false;
            return argBox != null &&
                   argBox.Get(ForceEncounterEventIds.参数.未成年, ref 未成年) &&
                   未成年;
        }

        public static bool TargetHasGuard(EventArgBox argBox, string modId)
        {
            return TryGetTargetId(argBox, out int targetId) &&
                   ShouldUseGuardInterceptionForTarget(targetId, modId) &&
                   DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                   EventHelper.HasGuard(target);
        }

        public static bool ShouldUseGuardInterceptionForTarget(int targetId, string modId)
        {
            return IsGuardInterceptionEnabled(modId) &&
                   (!IsTaiwuVillager(targetId) || IsTaiwuVillagerGuardInterceptionEnabled(modId));
        }

        public static bool IsGuardInterceptionEnabled(string modId)
        {
            return TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.EnableGuardInterception, true);
        }

        public static bool IsTaiwuVillagerGuardInterceptionEnabled(string modId)
        {
            return TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.EnableTaiwuVillagerGuardInterception, false);
        }

        public static bool IsTaiwuVillager(int characterId)
        {
            return DomainManager.Character.TryGetElement_Objects(characterId, out var character) &&
                   character.GetOrganizationInfo().OrgTemplateId == ForceEncounterConstants.Gameplay.TaiwuVillageOrgTemplateId;
        }

        public static void DebugLog(string modId, string message)
        {
            if (TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.DebugMode, true))
            {
                EventHelper.Log("[ForceEncounter] " + message);
            }
        }

        public static SerializableModData CallBackend(string modId, int actorId, int targetId, int resolutionMode)
        {
            DebugLog(
                modId,
                "CallBackend mode=" + resolutionMode +
                ", actor=" + actorId +
                ", target=" + targetId);
            var parameter = new SerializableModData();
            parameter.Set(ForceEncounterConstants.Backend.ActorId, actorId);
            parameter.Set(ForceEncounterConstants.Backend.TargetId, targetId);
            parameter.Set(ForceEncounterEventIds.后端.结算模式, resolutionMode);

            SerializableModData result = DomainManager.Mod.CallModMethodWithParamAndRet(
                DataContextManager.GetCurrentThreadDataContext(),
                modId,
                ForceEncounterEventIds.后端.执行方法,
                parameter);
            DebugLogBackendResult(modId, "CallBackendResult", result);
            return result;
        }

        public static SerializableModData CallCombatBackend(string modId, int actorId, int targetId, bool battleSucceeded)
        {
            DebugLog(
                modId,
                "CallCombatBackend actor=" + actorId +
                ", target=" + targetId +
                ", battleSucceeded=" + battleSucceeded);
            var parameter = new SerializableModData();
            parameter.Set(ForceEncounterConstants.Backend.ActorId, actorId);
            parameter.Set(ForceEncounterConstants.Backend.TargetId, targetId);
            parameter.Set(ForceEncounterEventIds.后端.战斗成功, battleSucceeded);
            parameter.Set(ForceEncounterEventIds.后端.结算模式, ForceEncounterEventIds.结算模式.战斗结算);

            SerializableModData result = DomainManager.Mod.CallModMethodWithParamAndRet(
                DataContextManager.GetCurrentThreadDataContext(),
                modId,
                ForceEncounterEventIds.后端.执行方法,
                parameter);
            DebugLogBackendResult(modId, "CallCombatBackendResult", result);
            return result;
        }

        public static void StoreCombatSettlement(
            EventArgBox argBox,
            bool ok,
            bool succeeded,
            bool targetIsTaiwuVillager,
            string reason,
            bool appliedEnmity = false)
        {
            if (argBox == null)
            {
                return;
            }

            argBox.Set(ForceEncounterEventIds.参数.战斗结果已处理, true);
            argBox.Set(ForceEncounterEventIds.参数.战斗结算成功, ok);
            argBox.Set(ForceEncounterEventIds.参数.战斗分支成功, succeeded);
            argBox.Set(ForceEncounterEventIds.参数.战斗目标是太吾村民, targetIsTaiwuVillager);
            argBox.Set(ForceEncounterEventIds.参数.战斗应用结仇后果, appliedEnmity);
            argBox.Set(ForceEncounterEventIds.参数.战斗结算原因, reason ?? string.Empty);
        }

        public static void StoreAcceptedSettlement(
            EventArgBox argBox,
            bool ok,
            bool succeeded,
            string reason)
        {
            if (argBox == null)
            {
                return;
            }

            argBox.Set(ForceEncounterEventIds.参数.亲密结果已处理, true);
            argBox.Set(ForceEncounterEventIds.参数.亲密结算成功, ok);
            argBox.Set(ForceEncounterEventIds.参数.亲密分支成功, succeeded);
            argBox.Set(ForceEncounterEventIds.参数.亲密结算原因, reason ?? string.Empty);
        }

        private static void DebugLogBackendResult(string modId, string label, SerializableModData result)
        {
            if (!TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.DebugMode, true))
            {
                return;
            }

            if (result == null)
            {
                EventHelper.Log("[ForceEncounter] " + label + " result=null");
                return;
            }

            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            int resolution = 0;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            result.Get(ForceEncounterConstants.Response.Ok, out ok);
            result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
            result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
            result.Get(ForceEncounterConstants.Response.AppliedEnmity, out appliedEnmity);
            result.Get(ForceEncounterConstants.Response.Reason, out reason);
            result.Get(ForceEncounterEventIds.后端.结算结果, out resolution);
            EventHelper.Log(
                "[ForceEncounter] " + label +
                " ok=" + ok +
                ", succeeded=" + succeeded +
                ", resolution=" + resolution +
                ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                ", appliedEnmity=" + appliedEnmity +
                ", reason=" + reason);
        }
    }
}
