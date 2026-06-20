using System;
using ForceEncounter.Shared;

namespace ForceEncounter.Backend
{
    internal static class ForceEncounterRules
    {
        public const int DefaultForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.DefaultForcedFavorabilityPenalty;
        public const int MinForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.MinForcedFavorabilityPenalty;
        public const int MaxForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.MaxForcedFavorabilityPenalty;
        public const int DefaultTaiwuVillagerPenaltyPercent = ForceEncounterConstants.Gameplay.DefaultTaiwuVillagerPenaltyPercent;
        public const int MinTaiwuVillagerPenaltyPercent = ForceEncounterConstants.Gameplay.MinTaiwuVillagerPenaltyPercent;
        public const int MaxTaiwuVillagerPenaltyPercent = ForceEncounterConstants.Gameplay.MaxTaiwuVillagerPenaltyPercent;

        public static bool IsResolvedSuccess(bool battleSucceeded, bool targetIsTaiwu)
        {
            return !targetIsTaiwu && battleSucceeded;
        }

        public static int CalculateForcedFavorabilityDelta(
            int configuredPenalty,
            bool targetIsTaiwuVillager,
            int taiwuVillagerPenaltyPercent)
        {
            int penalty = Math.Clamp(
                configuredPenalty,
                MinForcedFavorabilityPenalty,
                MaxForcedFavorabilityPenalty);
            if (targetIsTaiwuVillager)
            {
                int percent = Math.Clamp(
                    taiwuVillagerPenaltyPercent,
                    MinTaiwuVillagerPenaltyPercent,
                    MaxTaiwuVillagerPenaltyPercent);
                penalty = penalty * percent / 100;
            }

            return -penalty;
        }

        public static bool CanAcceptIntimateEncounter(
            bool isSpouse,
            bool isMutualLover,
            bool targetAdoresActor,
            bool targetIsDeepValleyCloseFriend,
            bool isTaiwuVillager,
            bool targetHasExclusiveAttachmentToOther,
            int actorFavorabilityType,
            int targetFavorabilityType,
            int actorBehaviorType)
        {
            return EvaluateIntimateEncounter(
                isSpouse,
                isMutualLover,
                targetAdoresActor,
                targetIsDeepValleyCloseFriend,
                isTaiwuVillager,
                targetHasExclusiveAttachmentToOther,
                actorFavorabilityType,
                targetFavorabilityType,
                actorBehaviorType).Accepted;
        }

        public static ForceEncounterIntimateAcceptanceDecision EvaluateIntimateEncounter(
            bool isSpouse,
            bool isMutualLover,
            bool targetAdoresActor,
            bool targetIsDeepValleyCloseFriend,
            bool isTaiwuVillager,
            bool targetHasExclusiveAttachmentToOther,
            int actorFavorabilityType,
            int targetFavorabilityType,
            int actorBehaviorType)
        {
            if (targetIsDeepValleyCloseFriend && targetHasExclusiveAttachmentToOther)
            {
                bool deepValleyAccepted = targetFavorabilityType > ForceEncounterConstants.FavorabilityTypes.Favorite2;
                return new ForceEncounterIntimateAcceptanceDecision(
                    deepValleyAccepted,
                    deepValleyAccepted ? "DeepValleyAttachedAccepted" : "DeepValleyAttachedFavorabilityTooLow",
                    hasIntimateSource: true,
                    deepValleyAttachedRule: true,
                    villagerLeniencyApplied: false,
                    nonLoverMinimumApplied: false,
                    baseRequiredFavorabilityType: ForceEncounterConstants.FavorabilityTypes.Favorite2 + 1,
                    requiredFavorabilityType: ForceEncounterConstants.FavorabilityTypes.Favorite2 + 1,
                    actorFavorabilityPassed: true,
                    targetFavorabilityPassed: deepValleyAccepted);
            }

            bool deepValleyCloseFriendCanAccept = targetIsDeepValleyCloseFriend &&
                                                  !targetHasExclusiveAttachmentToOther;
            bool hasIntimateSource = isSpouse ||
                                     isMutualLover ||
                                     targetAdoresActor ||
                                     deepValleyCloseFriendCanAccept ||
                                     isTaiwuVillager;
            if (!hasIntimateSource)
            {
                return new ForceEncounterIntimateAcceptanceDecision(
                    false,
                    "NoIntimateSource",
                    hasIntimateSource: false,
                    deepValleyAttachedRule: false,
                    villagerLeniencyApplied: false,
                    nonLoverMinimumApplied: false,
                    baseRequiredFavorabilityType: 0,
                    requiredFavorabilityType: 0,
                    actorFavorabilityPassed: false,
                    targetFavorabilityPassed: false);
            }

            int requiredFavorabilityType = GetIntimateAcceptanceFavorabilityType(actorBehaviorType);
            int baseRequiredFavorabilityType = requiredFavorabilityType;
            bool villagerLeniencyApplied = false;
            if (isTaiwuVillager && !targetHasExclusiveAttachmentToOther)
            {
                requiredFavorabilityType = Math.Max(3, requiredFavorabilityType - 1);
                villagerLeniencyApplied = true;
            }

            bool nonLoverMinimumApplied = false;
            if (!isSpouse && !isMutualLover && !targetAdoresActor)
            {
                requiredFavorabilityType = Math.Max(4, requiredFavorabilityType);
                nonLoverMinimumApplied = true;
            }

            bool actorFavorabilityPassed = actorFavorabilityType >= requiredFavorabilityType;
            bool targetFavorabilityPassed = targetFavorabilityType >= requiredFavorabilityType;
            bool accepted = actorFavorabilityPassed && targetFavorabilityPassed;
            return new ForceEncounterIntimateAcceptanceDecision(
                accepted,
                accepted ? "AcceptedByFavorability" : "FavorabilityTooLow",
                hasIntimateSource,
                deepValleyAttachedRule: false,
                villagerLeniencyApplied,
                nonLoverMinimumApplied,
                baseRequiredFavorabilityType,
                requiredFavorabilityType,
                actorFavorabilityPassed,
                targetFavorabilityPassed);
        }

        public static bool ShouldApplyReducedAcceptedFavorabilityPenalty(
            bool isSpouse,
            bool isMutualLover,
            bool targetUnilaterallyAdoresActor,
            bool deepValleyCloseFriendHasOtherAttachment)
        {
            if (isSpouse || isMutualLover)
            {
                return false;
            }

            return targetUnilaterallyAdoresActor || deepValleyCloseFriendHasOtherAttachment;
        }

        private static int GetIntimateAcceptanceFavorabilityType(int actorBehaviorType)
        {
            return actorBehaviorType switch
            {
                3 => 3,
                4 => 3,
                2 => 4,
                _ => 5
            };
        }
    }
}
