using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;
using GameData.Domains.Map;
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

            bool success = ExecuteResolvedAction(context, actor, target, battleSucceeded);
            bool targetIsTaiwuVillager = IsTaiwuVillager(target);

            result.Set(ForceEncounterConstants.Response.Ok, true);
            result.Set(ForceEncounterConstants.Response.Succeeded, success);
            result.Set(ForceEncounterConstants.Response.ActorId, actorId);
            result.Set(ForceEncounterConstants.Response.TargetId, targetId);
            result.Set(ForceEncounterConstants.Response.TargetIsTaiwuVillager, targetIsTaiwuVillager);
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
            ApplyRapeSuccess(
                context,
                actor,
                target,
                addHatredRelation: false,
                favorabilityDelta: GetAcceptedFavorabilityDelta(actor, target));

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
                    ApplyNativeBecomeEnemy(target, actor);
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
                ApplyNativeBecomeEnemy(target, actor);
            }

            ApplyForcedFavorabilityDelta(context, target, actor, favorabilityDelta);

            int dataOffset = DomainManager.Information.GetSecretInformationCollection().AddRape(actorId, targetId);
            DomainManager.Information.AddSecretInformation(context, dataOffset);
        }

        private static void ApplyNativeBecomeEnemy(Character target, Character actor)
        {
            EventHelper.ApplyRelationBecomeEnemy(target, actor);
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

            bool actorAdoresTarget = RelationType.HasRelation(actorToTarget.RelationType, ForceEncounterConstants.Relations.Adored);
            bool targetAdoresActor = RelationType.HasRelation(targetToActor.RelationType, ForceEncounterConstants.Relations.Adored);
            bool isSpouse = RelationType.HasRelation(actorToTarget.RelationType, ForceEncounterConstants.Relations.Spouse) &&
                            RelationType.HasRelation(targetToActor.RelationType, ForceEncounterConstants.Relations.Spouse);
            bool isMutualLover = actorAdoresTarget && targetAdoresActor;
            bool targetHasExclusiveAttachmentToOther = HasExclusiveLivingAttachmentToOther(target, actorId);
            bool targetIsDeepValleyCloseFriend = IsDeepValleyCloseFriendToActor(actor, target);
            return ForceEncounterRules.CanAcceptIntimateEncounter(
                isSpouse,
                isMutualLover,
                targetAdoresActor,
                targetIsDeepValleyCloseFriend,
                IsTaiwuVillager(target),
                targetHasExclusiveAttachmentToOther,
                actorToTarget.GetFavorabilityType(),
                targetToActor.GetFavorabilityType(),
                actor.GetBehaviorType());
        }

        private static int GetAcceptedFavorabilityDelta(Character actor, Character target)
        {
            int actorId = actor.GetId();
            int targetId = target.GetId();
            if (!DomainManager.Character.TryGetRelation(actorId, targetId, out RelatedCharacter actorToTarget) ||
                !DomainManager.Character.TryGetRelation(targetId, actorId, out RelatedCharacter targetToActor))
            {
                return 0;
            }

            bool actorAdoresTarget = RelationType.HasRelation(actorToTarget.RelationType, ForceEncounterConstants.Relations.Adored);
            bool targetAdoresActor = RelationType.HasRelation(targetToActor.RelationType, ForceEncounterConstants.Relations.Adored);
            bool targetUnilaterallyAdoresActor = targetAdoresActor && !actorAdoresTarget;
            bool deepValleyCloseFriendHasOtherAttachment = IsDeepValleyCloseFriendToActor(actor, target) &&
                                                           HasExclusiveLivingAttachmentToOther(target, actorId);
            if (targetUnilaterallyAdoresActor || deepValleyCloseFriendHasOtherAttachment)
            {
                return GetReducedAcceptedFavorabilityDelta();
            }

            return 0;
        }

        private static bool IsDeepValleyCloseFriendToActor(
            Character actor,
            Character target)
        {
            return target.GetFeatureIds().Contains(ForceEncounterConstants.Features.DeepValleyCloseFriend) &&
                   actor.GetId() == DomainManager.Taiwu.GetTaiwuCharIdForCloseFriend();
        }

        private static bool HasExclusiveLivingAttachmentToOther(Character target, int actorId)
        {
            int targetId = target.GetId();
            int spouseId = DomainManager.Character.GetAliveSpouse(targetId);
            if (spouseId >= 0 && spouseId != actorId)
            {
                return true;
            }

            int livingAdoredCount = 0;
            bool onlyLivingAdoredIsActor = false;
            foreach (int adoredCharId in DomainManager.Character.GetRelatedCharIds(targetId, ForceEncounterConstants.Relations.Adored))
            {
                if (!DomainManager.Character.IsCharacterAlive(adoredCharId))
                {
                    continue;
                }

                livingAdoredCount++;
                onlyLivingAdoredIsActor = adoredCharId == actorId;
                if (livingAdoredCount > 1)
                {
                    return false;
                }
            }

            return livingAdoredCount == 1 && !onlyLivingAdoredIsActor;
        }

        private static bool IsTaiwuVillager(Character character)
        {
            return character.GetOrganizationInfo().OrgTemplateId == ForceEncounterConstants.Gameplay.TaiwuVillageOrgTemplateId;
        }

        private static int GetForcedFavorabilityDelta(bool targetIsTaiwuVillager)
        {
            return ForceEncounterRules.CalculateForcedFavorabilityDelta(
                GetIntSetting(ForceEncounterConstants.Settings.ForcedFavorabilityPenalty, ForceEncounterRules.DefaultForcedFavorabilityPenalty),
                targetIsTaiwuVillager);
        }

        private static int GetReducedAcceptedFavorabilityDelta()
        {
            return ForceEncounterRules.CalculateForcedFavorabilityDelta(
                GetIntSetting(ForceEncounterConstants.Settings.ForcedFavorabilityPenalty, ForceEncounterRules.DefaultForcedFavorabilityPenalty),
                targetIsTaiwuVillager: true);
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
