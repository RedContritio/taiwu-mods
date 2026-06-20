using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Utilities;
using ForceEncounter.Shared;
using TaiwuModdingLib.Core.Plugin;

namespace ForceEncounter.Backend
{
    [PluginConfig(ForceEncounterConstants.Mod.Id, ForceEncounterConstants.Mod.Author, "0.0.0.18")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
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
            if (!GetBoolSetting(ForceEncounterConstants.Settings.Enabled, true))
            {
                return Fail(result, ForceEncounterConstants.Reasons.ModDisabled);
            }

            if (!TryGetInt(parameter, ForceEncounterConstants.Backend.ActorId, out int actorId))
            {
                actorId = DomainManager.Taiwu.GetTaiwuCharId();
            }

            if (!TryGetInt(parameter, ForceEncounterConstants.Backend.TargetId, out int targetId))
            {
                return Fail(result, ForceEncounterConstants.Reasons.MissingActorOrTarget);
            }

            int resolutionMode = GetInt(parameter, ForceEncounterEventIds.ResolutionModeParam, ForceEncounterEventIds.ResolutionModeCombat);
            bool isCombatResolution = resolutionMode == ForceEncounterEventIds.ResolutionModeCombat;
            bool isAcceptedCommit = resolutionMode == ForceEncounterEventIds.ResolutionModeAcceptedCommit;
            bool battleSucceeded = false;
            if (isCombatResolution && !TryGetBool(parameter, ForceEncounterEventIds.BattleSucceededParam, out battleSucceeded))
            {
                return Fail(result, ForceEncounterConstants.Reasons.MissingBattleResult);
            }

            if (actorId == targetId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.SameActorAndTarget);
            }

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (actorId != taiwuId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.NonTaiwuActorNotAllowed);
            }

            if (targetId == taiwuId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.TaiwuTargetNotAllowed);
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out Character actor))
            {
                return Fail(result, ForceEncounterConstants.Reasons.ActorNotFound);
            }

            if (!DomainManager.Character.TryGetElement_Objects(targetId, out Character target))
            {
                return Fail(result, ForceEncounterConstants.Reasons.TargetNotFound);
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return Fail(result, ForceEncounterConstants.Reasons.CharacterNotAlive);
            }

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.BabyAgeGroup ||
                target.GetAgeGroup() == ForceEncounterConstants.Gameplay.BabyAgeGroup)
            {
                return Fail(result, ForceEncounterConstants.Reasons.BabyNotAllowed);
            }

            if (!isCombatResolution)
            {
                if (isAcceptedCommit)
                {
                    return ExecuteAcceptedEncounter(context, result, actor, target);
                }

                return ProbeEncounter(result, actor, target);
            }

            bool success = ExecuteResolvedAction(context, actor, target, battleSucceeded, out bool targetIsTaiwuVillager, out bool appliedEnmity);

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, success);
            result.Set(ForceEncounterConstants.Response.ActorId, actorId);
            result.Set(ForceEncounterConstants.Response.TargetId, targetId);
            result.Set(ForceEncounterConstants.Response.TargetIsTaiwuVillager, targetIsTaiwuVillager);
            result.Set(ForceEncounterConstants.Response.AppliedEnmity, appliedEnmity);
            result.Set(ForceEncounterConstants.Response.Reason, success ? ForceEncounterConstants.Reasons.Succeed : ForceEncounterConstants.Reasons.Failed);
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
                result.Set(ForceEncounterConstants.Response.Ok, true);
                result.Set(ForceEncounterConstants.Response.Succeeded, false);
                result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
                result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
                result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
                result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.Accepted);
                DebugLog($"Probe accepted {actor.GetId()}->{target.GetId()}");
                return result;
            }

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, false);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionNeedCombatChoice);
            result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
            result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
            result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.NeedCombatChoice);
            DebugLog($"Probe needs combat choice {actor.GetId()}->{target.GetId()}");
            return result;
        }

        private static SerializableModData ExecuteAcceptedEncounter(
            DataContext context,
            SerializableModData result,
            Character actor,
            Character target)
        {
            if (!CanResolveAsIntimateAcceptance(actor, target))
            {
                return Fail(result, ForceEncounterConstants.Reasons.NeedCombatChoice);
            }

            ForceEncounterEffectApplier.ApplySuccess(
                context,
                actor,
                target,
                addHatredRelation: false,
                favorabilityDelta: GetAcceptedFavorabilityDelta(actor, target),
                createSecret: ShouldCreateAcceptedSecret());

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, true);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
            result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
            result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
            result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.Accepted);
            DebugLog($"Accepted encounter committed {actor.GetId()}->{target.GetId()}");
            return result;
        }

        private static bool ExecuteResolvedAction(
            DataContext context,
            Character actor,
            Character target,
            bool battleSucceeded,
            out bool targetIsTaiwuVillager,
            out bool appliedEnmity)
        {
            int targetId = target.GetId();
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            targetIsTaiwuVillager = ForceEncounterRelationReader.IsTaiwuVillager(target);
            appliedEnmity = false;
            bool success = ForceEncounterRules.IsResolvedSuccess(
                battleSucceeded,
                targetId == taiwuId);

            if (success)
            {
                appliedEnmity = ShouldBecomeEnemyOnForcedRoute(targetIsTaiwuVillager);
                ForceEncounterEffectApplier.ApplySuccess(
                    context,
                    actor,
                    target,
                    addHatredRelation: appliedEnmity,
                    favorabilityDelta: GetForcedFavorabilityDelta(targetIsTaiwuVillager),
                    createSecret: ShouldCreateForcedSecret());
            }
            else
            {
                appliedEnmity = ShouldBecomeEnemyOnForcedRoute(targetIsTaiwuVillager);
                ForceEncounterEffectApplier.ApplyFailure(
                    context,
                    actor,
                    target,
                    addHatredRelation: appliedEnmity,
                    favorabilityDelta: GetForcedFavorabilityDelta(targetIsTaiwuVillager));
            }

            return success;
        }

        private static bool CanResolveAsIntimateAcceptance(Character actor, Character target)
        {
            if (!ForceEncounterRelationReader.TryCreateSnapshot(actor, target, out ForceEncounterRelationSnapshot snapshot))
            {
                return false;
            }

            return ForceEncounterRules.CanAcceptIntimateEncounter(
                snapshot.IsSpouse,
                snapshot.IsMutualLover,
                snapshot.TargetAdoresActor,
                snapshot.TargetIsDeepValleyCloseFriend,
                snapshot.TargetIsTaiwuVillager,
                snapshot.TargetHasExclusiveAttachmentToOther,
                snapshot.ActorFavorabilityType,
                snapshot.TargetFavorabilityType,
                snapshot.ActorBehaviorType);
        }

        private static int GetAcceptedFavorabilityDelta(Character actor, Character target)
        {
            if (!ForceEncounterRelationReader.TryCreateSnapshot(actor, target, out ForceEncounterRelationSnapshot snapshot))
            {
                return 0;
            }

            if (ForceEncounterRules.ShouldApplyReducedAcceptedFavorabilityPenalty(
                    snapshot.IsSpouse,
                    snapshot.IsMutualLover,
                    snapshot.TargetUnilaterallyAdoresActor,
                    snapshot.DeepValleyCloseFriendHasOtherAttachment))
            {
                return GetReducedAcceptedFavorabilityDelta();
            }

            return 0;
        }

        private static int GetForcedFavorabilityDelta(bool targetIsTaiwuVillager)
        {
            return ForceEncounterRules.CalculateForcedFavorabilityDelta(
                GetIntSetting(ForceEncounterConstants.Settings.ForcedFavorabilityPenalty, ForceEncounterRules.DefaultForcedFavorabilityPenalty),
                targetIsTaiwuVillager,
                GetIntSetting(ForceEncounterConstants.Settings.TaiwuVillagerPenaltyPercent, ForceEncounterRules.DefaultTaiwuVillagerPenaltyPercent));
        }

        private static int GetReducedAcceptedFavorabilityDelta()
        {
            return ForceEncounterRules.CalculateForcedFavorabilityDelta(
                GetIntSetting(ForceEncounterConstants.Settings.ForcedFavorabilityPenalty, ForceEncounterRules.DefaultForcedFavorabilityPenalty),
                targetIsTaiwuVillager: true,
                GetIntSetting(ForceEncounterConstants.Settings.TaiwuVillagerPenaltyPercent, ForceEncounterRules.DefaultTaiwuVillagerPenaltyPercent));
        }

        private static SerializableModData Fail(SerializableModData result, string reason)
        {
            result.Set(ForceEncounterConstants.Response.Ok, false);
            result.Set(ForceEncounterConstants.Response.Succeeded, false);
            result.Set(ForceEncounterConstants.Response.Reason, reason);
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
            return ForceEncounterSettings.GetBool(ModId, key, fallback);
        }

        private static int GetIntSetting(string key, int fallback)
        {
            return ForceEncounterSettings.GetInt(ModId, key, fallback);
        }

        private static bool ShouldBecomeEnemyOnForcedRoute(bool targetIsTaiwuVillager)
        {
            return !targetIsTaiwuVillager &&
                   GetBoolSetting(ForceEncounterConstants.Settings.BecomeEnemyOnForcedRoute, true);
        }

        private static bool ShouldCreateForcedSecret()
        {
            return GetBoolSetting(ForceEncounterConstants.Settings.CreateSecretOnForcedSuccess, true);
        }

        private static bool ShouldCreateAcceptedSecret()
        {
            return GetBoolSetting(ForceEncounterConstants.Settings.CreateSecretOnAcceptedSuccess, true);
        }

        private static void AddExecuteMethod(string modId)
        {
            try
            {
                DomainManager.Mod.AddModMethod(modId, ForceEncounterConstants.Backend.ExecuteMethod, ExecuteForcedAction);
            }
            catch
            {
                // The development mod id and runtime mod id can be identical.
            }

            try
            {
                DomainManager.Mod.AddModMethod(modId, ForceEncounterConstants.Backend.ExecuteMethod, ExecuteForcedActionNoReturn);
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
            return GetBoolSetting(ForceEncounterConstants.Settings.DebugMode, true);
        }

    }
}
