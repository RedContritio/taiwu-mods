using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.EventHelper;

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
            argBox.Set(ForceEncounterEventIds.参数.特殊年龄, RequiresSpecialAgeNotice(actorId, targetId));
        }

        public static bool RequiresSpecialAgeNotice(int actorId, int targetId)
        {
            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return false;
            }

            return actor.GetAgeGroup() != ForceEncounterConstants.Gameplay.AdultAgeGroup ||
                   target.GetAgeGroup() != ForceEncounterConstants.Gameplay.AdultAgeGroup;
        }

        public static bool IsSpecialAge(EventArgBox argBox)
        {
            bool specialAge = false;
            return argBox != null &&
                   argBox.Get(ForceEncounterEventIds.参数.特殊年龄, ref specialAge) &&
                   specialAge;
        }

        public static bool TargetHasGuard(EventArgBox argBox)
        {
            return TryGetTargetId(argBox, out int targetId) &&
                   !IsTaiwuVillager(targetId) &&
                   DomainManager.Character.TryGetElement_Objects(targetId, out var target) &&
                   EventHelper.HasGuard(target);
        }

        public static bool IsTaiwuVillager(int characterId)
        {
            return DomainManager.Character.TryGetElement_Objects(characterId, out var character) &&
                   character.GetOrganizationInfo().OrgTemplateId == ForceEncounterConstants.Gameplay.TaiwuVillageOrgTemplateId;
        }

        public static SerializableModData CallBackend(string modId, int actorId, int targetId, int resolutionMode)
        {
            var parameter = new SerializableModData();
            parameter.Set(ForceEncounterConstants.Backend.ActorId, actorId);
            parameter.Set(ForceEncounterConstants.Backend.TargetId, targetId);
            parameter.Set(ForceEncounterEventIds.后端.结算模式, resolutionMode);

            return DomainManager.Mod.CallModMethodWithParamAndRet(
                DataContextManager.GetCurrentThreadDataContext(),
                modId,
                ForceEncounterEventIds.后端.执行方法,
                parameter);
        }

        public static SerializableModData CallCombatBackend(string modId, int actorId, int targetId, bool battleSucceeded)
        {
            var parameter = new SerializableModData();
            parameter.Set(ForceEncounterConstants.Backend.ActorId, actorId);
            parameter.Set(ForceEncounterConstants.Backend.TargetId, targetId);
            parameter.Set(ForceEncounterEventIds.后端.战斗成功, battleSucceeded);
            parameter.Set(ForceEncounterEventIds.后端.结算模式, ForceEncounterEventIds.结算模式.战斗结算);

            return DomainManager.Mod.CallModMethodWithParamAndRet(
                DataContextManager.GetCurrentThreadDataContext(),
                modId,
                ForceEncounterEventIds.后端.执行方法,
                parameter);
        }
    }
}
