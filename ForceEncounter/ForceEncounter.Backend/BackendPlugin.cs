using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Utilities;
using ForceEncounter.Shared;
using TaiwuMod.Common;
using TaiwuModdingLib.Core.Plugin;

namespace ForceEncounter.Backend
{
    [PluginConfig(ForceEncounterConstants.Mod.Id, ForceEncounterConstants.Mod.Author, "1.1.0.0")]
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
            if (!TryGetInt(parameter, ForceEncounterConstants.Backend.ActorId, out int actorId))
            {
                actorId = DomainManager.Taiwu.GetTaiwuCharId();
            }

            if (!TryGetInt(parameter, ForceEncounterConstants.Backend.TargetId, out int targetId))
            {
                return Fail(result, ForceEncounterConstants.Reasons.MissingActorOrTarget, actorId, -1, resolutionMode: 0);
            }

            int resolutionMode = GetInt(parameter, ForceEncounterEventIds.ResolutionModeParam, ForceEncounterEventIds.ResolutionModeCombat);
            bool isCombatResolution = resolutionMode == ForceEncounterEventIds.ResolutionModeCombat;
            bool isAcceptedCommit = resolutionMode == ForceEncounterEventIds.ResolutionModeAcceptedCommit;
            DebugLog(
                "Backend enter mode=" + resolutionMode +
                ", actor=" + actorId +
                ", target=" + targetId +
                ", isCombatResolution=" + isCombatResolution +
                ", isAcceptedCommit=" + isAcceptedCommit);
            bool battleSucceeded = false;
            if (isCombatResolution && !TryGetBool(parameter, ForceEncounterEventIds.BattleSucceededParam, out battleSucceeded))
            {
                return Fail(result, ForceEncounterConstants.Reasons.MissingBattleResult, actorId, targetId, resolutionMode);
            }

            if (actorId == targetId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.SameActorAndTarget, actorId, targetId, resolutionMode);
            }

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (actorId != taiwuId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.NonTaiwuActorNotAllowed, actorId, targetId, resolutionMode);
            }

            if (targetId == taiwuId)
            {
                return Fail(result, ForceEncounterConstants.Reasons.TaiwuTargetNotAllowed, actorId, targetId, resolutionMode);
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out Character actor))
            {
                return Fail(result, ForceEncounterConstants.Reasons.ActorNotFound, actorId, targetId, resolutionMode);
            }

            if (!DomainManager.Character.TryGetElement_Objects(targetId, out Character target))
            {
                return Fail(result, ForceEncounterConstants.Reasons.TargetNotFound, actorId, targetId, resolutionMode);
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return Fail(result, ForceEncounterConstants.Reasons.CharacterNotAlive, actorId, targetId, resolutionMode);
            }

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组 ||
                target.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组)
            {
                return Fail(result, ForceEncounterConstants.Reasons.BabyNotAllowed, actorId, targetId, resolutionMode);
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
            DebugLog(
                "Backend combat result actor=" + actorId +
                ", target=" + targetId +
                ", battleSucceeded=" + battleSucceeded +
                ", success=" + success +
                ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                ", appliedEnmity=" + appliedEnmity);
            return result;
        }

        private static SerializableModData ProbeEncounter(
            SerializableModData result,
            Character actor,
            Character target)
        {
            bool accepted = CanResolveAsIntimateAcceptance(actor, target, "Probe");
            if (accepted)
            {
                result.Set(ForceEncounterConstants.Response.Ok, true);
                result.Set(ForceEncounterConstants.Response.Succeeded, false);
                result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
                result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
                result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
                result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.Accepted);
                DebugLog("Probe route=Accepted actor=" + actor.GetId() + ", target=" + target.GetId());
                return result;
            }

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, false);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionNeedCombatChoice);
            result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
            result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
            result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.NeedCombatChoice);
            DebugLog("Probe route=NeedCombatChoice actor=" + actor.GetId() + ", target=" + target.GetId());
            return result;
        }

        private static SerializableModData ExecuteAcceptedEncounter(
            DataContext context,
            SerializableModData result,
            Character actor,
            Character target)
        {
            if (!CanResolveAsIntimateAcceptance(actor, target, "AcceptedCommitRecheck"))
            {
                return Fail(result, ForceEncounterConstants.Reasons.NeedCombatChoice, actor.GetId(), target.GetId(), ForceEncounterEventIds.ResolutionModeAcceptedCommit);
            }

            int favorabilityDelta = GetAcceptedFavorabilityDelta(actor, target);
            bool createSecret = ShouldCreateAcceptedSecret();
            DebugLog(
                "Accepted commit effects actor=" + actor.GetId() +
                ", target=" + target.GetId() +
                ", favorabilityDelta=" + favorabilityDelta +
                ", createSecret=" + createSecret +
                ", addHatredRelation=False");
            ForceEncounterEffectApplier.ApplySuccess(
                context,
                actor,
                target,
                addHatredRelation: false,
                favorabilityDelta: favorabilityDelta,
                createSecret: createSecret);

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, true);
            result.Set(ForceEncounterEventIds.ResolutionParam, ForceEncounterEventIds.ResolutionAccepted);
            result.Set(ForceEncounterConstants.Response.ActorId, actor.GetId());
            result.Set(ForceEncounterConstants.Response.TargetId, target.GetId());
            result.Set(ForceEncounterConstants.Response.Reason, ForceEncounterConstants.Reasons.Accepted);
            DebugLog("Accepted encounter committed actor=" + actor.GetId() + ", target=" + target.GetId());
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
                int favorabilityDelta = GetForcedFavorabilityDelta(targetIsTaiwuVillager);
                bool createSecret = ShouldCreateForcedSecret();
                DebugLog(
                    "Forced success effects actor=" + actor.GetId() +
                    ", target=" + target.GetId() +
                    ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                    ", appliedEnmity=" + appliedEnmity +
                    ", favorabilityDelta=" + favorabilityDelta +
                    ", createSecret=" + createSecret);
                ForceEncounterEffectApplier.ApplySuccess(
                    context,
                    actor,
                    target,
                    addHatredRelation: appliedEnmity,
                    favorabilityDelta: favorabilityDelta,
                    createSecret: createSecret);
            }
            else
            {
                appliedEnmity = ShouldBecomeEnemyOnForcedRoute(targetIsTaiwuVillager);
                int favorabilityDelta = GetForcedFavorabilityDelta(targetIsTaiwuVillager);
                DebugLog(
                    "Forced failure effects actor=" + actor.GetId() +
                    ", target=" + target.GetId() +
                    ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                    ", appliedEnmity=" + appliedEnmity +
                    ", favorabilityDelta=" + favorabilityDelta);
                ForceEncounterEffectApplier.ApplyFailure(
                    context,
                    actor,
                    target,
                    addHatredRelation: appliedEnmity,
                    favorabilityDelta: favorabilityDelta);
            }

            return success;
        }

        private static bool CanResolveAsIntimateAcceptance(Character actor, Character target, string phase)
        {
            if (!ForceEncounterRelationReader.TryCreateSnapshot(actor, target, out ForceEncounterRelationSnapshot snapshot))
            {
                DebugLog(
                    "IntimateAcceptance phase=" + phase +
                    ", actor=" + actor.GetId() +
                    ", target=" + target.GetId() +
                    ", accepted=False, reason=MissingRelationSnapshot");
                return false;
            }

            ForceEncounterIntimateAcceptanceDecision decision = ForceEncounterRules.EvaluateIntimateEncounter(
                snapshot.IsSpouse,
                snapshot.IsMutualLover,
                snapshot.TargetAdoresActor,
                snapshot.TargetIsDeepValleyCloseFriend,
                snapshot.TargetIsTaiwuVillager,
                snapshot.TargetHasExclusiveAttachmentToOther,
                snapshot.ActorFavorabilityType,
                snapshot.TargetFavorabilityType,
                snapshot.ActorBehaviorType);
            DebugLogIntimateDecision(phase, actor.GetId(), target.GetId(), snapshot, decision);
            return decision.Accepted;
        }

        private static void DebugLogIntimateDecision(
            string phase,
            int actorId,
            int targetId,
            ForceEncounterRelationSnapshot snapshot,
            ForceEncounterIntimateAcceptanceDecision decision)
        {
            DebugLog(
                "IntimateAcceptance phase=" + phase +
                ", actor=" + actorId +
                ", target=" + targetId +
                ", accepted=" + decision.Accepted +
                ", reason=" + decision.Reason +
                ", isSpouse=" + snapshot.IsSpouse +
                ", isMutualLover=" + snapshot.IsMutualLover +
                ", actorAdoresTarget=" + snapshot.ActorAdoresTarget +
                ", targetAdoresActor=" + snapshot.TargetAdoresActor +
                ", targetIsDeepValleyCloseFriend=" + snapshot.TargetIsDeepValleyCloseFriend +
                ", targetIsTaiwuVillager=" + snapshot.TargetIsTaiwuVillager +
                ", targetHasExclusiveAttachmentToOther=" + snapshot.TargetHasExclusiveAttachmentToOther +
                ", actorFavorabilityType=" + snapshot.ActorFavorabilityType +
                ", targetFavorabilityType=" + snapshot.TargetFavorabilityType +
                ", actorBehaviorType=" + snapshot.ActorBehaviorType +
                ", hasIntimateSource=" + decision.HasIntimateSource +
                ", deepValleyAttachedRule=" + decision.DeepValleyAttachedRule +
                ", villagerLeniencyApplied=" + decision.VillagerLeniencyApplied +
                ", nonLoverMinimumApplied=" + decision.NonLoverMinimumApplied +
                ", baseRequiredFavorabilityType=" + decision.BaseRequiredFavorabilityType +
                ", requiredFavorabilityType=" + decision.RequiredFavorabilityType +
                ", actorFavorabilityPassed=" + decision.ActorFavorabilityPassed +
                ", targetFavorabilityPassed=" + decision.TargetFavorabilityPassed);
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

        private static SerializableModData Fail(
            SerializableModData result,
            string reason,
            int actorId = -1,
            int targetId = -1,
            int resolutionMode = 0)
        {
            result.Set(ForceEncounterConstants.Response.Ok, false);
            result.Set(ForceEncounterConstants.Response.Succeeded, false);
            result.Set(ForceEncounterConstants.Response.Reason, reason);
            DebugLog(
                "Backend failed reason=" + reason +
                ", mode=" + resolutionMode +
                ", actor=" + actorId +
                ", target=" + targetId);
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
            return TaiwuModSettings.GetBool(ModId, key, fallback);
        }

        private static int GetIntSetting(string key, int fallback)
        {
            return TaiwuModSettings.GetInt(ModId, key, fallback);
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
            EventHelper.AddOptionToEvent(
                ForceEncounterEventIds.NativeKidnappedInteractionEventGuid,
                ForceEncounterEventIds.PrisonerEntryEventGuid,
                ForceEncounterEventIds.ExecutePrisonerOptionKey);
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
