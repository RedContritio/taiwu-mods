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
            if (targetIsDeepValleyCloseFriend && targetHasExclusiveAttachmentToOther)
            {
                return targetFavorabilityType > ForceEncounterConstants.FavorabilityTypes.Favorite2;
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
                return false;
            }

            int requiredFavorabilityType = GetIntimateAcceptanceFavorabilityType(actorBehaviorType);
            if (isTaiwuVillager && !targetHasExclusiveAttachmentToOther)
            {
                requiredFavorabilityType = Math.Max(3, requiredFavorabilityType - 1);
            }

            if (!isSpouse && !isMutualLover && !targetAdoresActor)
            {
                requiredFavorabilityType = Math.Max(4, requiredFavorabilityType);
            }

            return actorFavorabilityType >= requiredFavorabilityType &&
                   targetFavorabilityType >= requiredFavorabilityType;
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
