using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;
using GameData.Domains.Map;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Utilities;
using TaiwuModdingLib.Core.Plugin;

namespace ForceEncounter.Backend
{
    [PluginConfig("ForceEncounter", "RedContritio", "0.0.0.18")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        private const string ExecuteMethod = "ExecuteForcedAction";
        private const string ForcedFavorabilityPenaltySettingKey = "ForcedFavorabilityPenalty";
        private const sbyte TaiwuVillageOrgTemplateId = 16;
        private const int HatredRelationType = 32768;

        internal static string ModId;

        public override void Initialize()
        {
            ModId = ModIdStr;
            AddExecuteMethod(ModId);
        }

        public override void Dispose()
        {
        }

        public override void OnModSettingUpdate()
        {
            // Settings are read live from DomainManager.Mod so existing saves pick up changes immediately.
        }

        public override void OnEnterNewWorld()
        {
            EnsureHostileMenuOption();
        }

        public override void OnLoadedArchiveData()
        {
            EnsureHostileMenuOption();
        }

        private static SerializableModData ExecuteForcedAction(DataContext context, SerializableModData parameter)
        {
            var result = new SerializableModData();
            if (!GetBoolSetting("Enabled", true))
            {
                return Fail(result, "ModDisabled");
            }

            if (!TryGetInt(parameter, "ActorId", out int actorId))
            {
                actorId = DomainManager.Taiwu.GetTaiwuCharId();
            }

            if (!TryGetInt(parameter, "TargetId", out int targetId))
            {
                return Fail(result, "MissingActorOrTarget");
            }

            int resolutionMode = GetInt(parameter, ForceEncounterEventIds.ResolutionModeParam, ForceEncounterEventIds.ResolutionModeCombat);
            bool isCombatResolution = resolutionMode == ForceEncounterEventIds.ResolutionModeCombat;
            bool isAcceptedCommit = resolutionMode == ForceEncounterEventIds.ResolutionModeAcceptedCommit;
            bool battleSucceeded = false;
            if (isCombatResolution && !TryGetBool(parameter, ForceEncounterEventIds.BattleSucceededParam, out battleSucceeded))
            {
                return Fail(result, "MissingBattleResult");
            }

            if (actorId == targetId)
            {
                return Fail(result, "SameActorAndTarget");
            }

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (targetId == taiwuId)
            {
                return Fail(result, "TaiwuTargetNotAllowed");
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out Character actor))
            {
                return Fail(result, "ActorNotFound");
            }

            if (!DomainManager.Character.TryGetElement_Objects(targetId, out Character target))
            {
                return Fail(result, "TargetNotFound");
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return Fail(result, "CharacterNotAlive");
            }

            if (actor.GetAgeGroup() == 0 || target.GetAgeGroup() == 0)
            {
                return Fail(result, "BabyNotAllowed");
            }

            if (!isCombatResolution)
            {
                if (isAcceptedCommit)
                {
                    return ExecuteAcceptedEncounter(context, result, actor, target);
                }

                return ProbeEncounter(result, actor, target);
            }

            bool success = ExecuteResolvedAction(context, actor, target, battleSucceeded);
            bool targetIsTaiwuVillager = IsTaiwuVillager(target);

            result.Set("Ok", true);
            result.Set("Succeeded", success);
            result.Set("ActorId", actorId);
            result.Set("TargetId", targetId);
            result.Set("TargetIsTaiwuVillager", targetIsTaiwuVillager);
            result.Set("Reason", success ? "Succeed" : "Failed");
            DebugLog($"Execute {actorId}->{targetId}, battleSucceeded={battleSucceeded}, success={success}, targetIsTaiwuVillager={targetIsTaiwuVillager}");
            return result;
        }

        private static SerializableModData ProbeEncounter(
            SerializableModData result,
            Character actor,
            Character target)
        {
            bool accepted = CanResolveAsIntimateAcceptance(actor, target);
            if (accepted)
            {
                result.Set("Ok", true);
                result.Set("Succeeded", false);
                result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
                result.Set("ActorId", actor.GetId());
                result.Set("TargetId", target.GetId());
                result.Set("Reason", "Accepted");
                DebugLog($"Probe accepted {actor.GetId()}->{target.GetId()}");
                return result;
            }

            result.Set("Ok", true);
            result.Set("Succeeded", false);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionNeedCombatChoice);
            result.Set("ActorId", actor.GetId());
            result.Set("TargetId", target.GetId());
            result.Set("Reason", "NeedCombatChoice");
            DebugLog($"Probe needs combat choice {actor.GetId()}->{target.GetId()}");
            return result;
        }

        private static SerializableModData ExecuteAcceptedEncounter(
            DataContext context,
            SerializableModData result,
            Character actor,
            Character target)
        {
            ApplyRapeSuccess(
                context,
                actor,
                target,
                addHatredRelation: false,
                favorabilityDelta: 0);

            result.Set("Ok", true);
            result.Set("Succeeded", true);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
            result.Set("ActorId", actor.GetId());
            result.Set("TargetId", target.GetId());
            result.Set("Reason", "Accepted");
            DebugLog($"Accepted encounter committed {actor.GetId()}->{target.GetId()}");
            return result;
        }

        private static bool ExecuteResolvedAction(
            DataContext context,
            Character actor,
            Character target,
            bool battleSucceeded)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            bool success = ForceEncounterRules.IsResolvedSuccess(
                battleSucceeded,
                targetId == taiwuId);

            if (success)
            {
                bool targetIsTaiwuVillager = IsTaiwuVillager(target);
                ApplyRapeSuccess(
                    context,
                    actor,
                    target,
                    addHatredRelation: !targetIsTaiwuVillager,
                    favorabilityDelta: GetForcedFavorabilityDelta(targetIsTaiwuVillager));
            }
            else
            {
                int currDate = DomainManager.World.GetCurrDate();
                Location location = actor.GetLocation();
                if (targetId == taiwuId)
                {
                    DomainManager.World.GetMonthlyNotificationCollection().AddRapeFailure(actorId, location, targetId);
                }

                DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeFail(actorId, currDate, targetId, location);
                bool targetIsTaiwuVillager = IsTaiwuVillager(target);
                if (!targetIsTaiwuVillager)
                {
                    DomainManager.Character.AddRelation(context, targetId, actorId, HatredRelationType, currDate);
                }

                ApplyForcedFavorabilityDelta(context, target, actor, GetForcedFavorabilityDelta(targetIsTaiwuVillager));
            }

            return success;
        }

        private static void ApplyRapeSuccess(
            DataContext context,
            Character actor,
            Character target,
            bool addHatredRelation,
            int favorabilityDelta)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            int currDate = DomainManager.World.GetCurrDate();
            Location location = actor.GetLocation();

            actor.MakeLove(context, target, isRape: true);
            DomainManager.LifeRecord.GetLifeRecordCollection().AddRapeSucceed(actorId, currDate, targetId, location);

            if (addHatredRelation)
            {
                DomainManager.Character.AddRelation(context, targetId, actorId, HatredRelationType, currDate);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);

            int dataOffset = DomainManager.Information.GetSecretInformationCollection().AddRape(actorId, targetId);
            DomainManager.Information.AddSecretInformation(context, dataOffset);
        }

        private static bool CanResolveAsIntimateAcceptance(Character actor, Character target)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            if (!DomainManager.Character.TryGetRelation(actorId, targetId, out RelatedCharacter actorToTarget) ||
                !DomainManager.Character.TryGetRelation(targetId, actorId, out RelatedCharacter targetToActor))
            {
                return false;
            }

            bool isSpouse = RelationType.HasRelation(actorToTarget.RelationType, 1024) &&
                            RelationType.HasRelation(targetToActor.RelationType, 1024);
            bool isMutualLover = RelationType.HasRelation(actorToTarget.RelationType, 16384) &&
                                 RelationType.HasRelation(targetToActor.RelationType, 16384);
            return ForceEncounterRules.CanAcceptIntimateEncounter(
                isSpouse,
                isMutualLover,
                IsTaiwuVillager(target),
                actorToTarget.GetFavorabilityType(),
                targetToActor.GetFavorabilityType(),
                actor.GetBehaviorType());
        }

        private static bool IsTaiwuVillager(Character character)
        {
            return character.GetOrganizationInfo().OrgTemplateId == TaiwuVillageOrgTemplateId;
        }

        private static int GetForcedFavorabilityDelta(bool targetIsTaiwuVillager)
        {
            return ForceEncounterRules.CalculateForcedFavorabilityDelta(
                GetIntSetting(ForcedFavorabilityPenaltySettingKey, ForceEncounterRules.DefaultForcedFavorabilityPenalty),
                targetIsTaiwuVillager);
        }

        private static void ApplyForcedFavorabilityDelta(
            DataContext context,
            Character target,
            Character actor,
            int favorabilityDelta)
        {
            if (favorabilityDelta == 0)
            {
                return;
            }

            DomainManager.Character.ChangeFavorabilityOptionalMonthlyEvolution(context, target, actor, favorabilityDelta);
        }

        private static SerializableModData Fail(SerializableModData result, string reason)
        {
            result.Set("Ok", false);
            result.Set("Succeeded", false);
            result.Set("Reason", reason);
            DebugLog($"Failed: {reason}");
            return result;
        }

        private static bool TryGetInt(SerializableModData data, string key, out int value)
        {
            value = 0;
            return data != null && data.Get(key, out value);
        }

        private static bool TryGetBool(SerializableModData data, string key, out bool value)
        {
            value = false;
            return data != null && data.Get(key, out value);
        }

        private static int GetInt(SerializableModData data, string key, int fallback)
        {
            if (data != null && data.Get(key, out int value))
            {
                return value;
            }

            return fallback;
        }

        private static bool GetBoolSetting(string key, bool fallback)
        {
            bool value = fallback;
            DomainManager.Mod.GetSetting(ModId, key, ref value);
            return value;
        }

        private static int GetIntSetting(string key, int fallback)
        {
            int value = fallback;
            DomainManager.Mod.GetSetting(ModId, key, ref value);
            return value;
        }

        private static void AddExecuteMethod(string modId)
        {
            try
            {
                DomainManager.Mod.AddModMethod(modId, ExecuteMethod, ExecuteForcedAction);
            }
            catch
            {
                // The development mod id and runtime mod id can be identical.
            }

            try
            {
                DomainManager.Mod.AddModMethod(modId, ExecuteMethod, ExecuteForcedActionNoReturn);
            }
            catch
            {
                // The development mod id and runtime mod id can be identical.
            }
        }

        private static void ExecuteForcedActionNoReturn(DataContext context, SerializableModData parameter)
        {
            ExecuteForcedAction(context, parameter);
        }

        private static void EnsureHostileMenuOption()
        {
            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.NativeEnemyInteractionEventGuid,
                ForceEncounterEventIds.EventGuid,
                ForceEncounterEventIds.OptionKey);
            DebugLog("Ensured hostile menu option");
        }

        internal static void DebugLog(string message)
        {
            if (IsDebugModeEnabled())
            {
                AdaptableLog.Info($"[ForceEncounter] {message}");
            }
        }

        internal static bool IsDebugModeEnabled()
        {
            return GetBoolSetting("DebugMode", true);
        }

    }
}
