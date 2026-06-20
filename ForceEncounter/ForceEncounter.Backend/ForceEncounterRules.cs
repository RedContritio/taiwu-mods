using System;
using ForceEncounter.Shared;

namespace ForceEncounter.Backend
{
    internal static class ForceEncounterRules
    {
        public const int DefaultForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.DefaultForcedFavorabilityPenalty;
        public const int MinForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.MinForcedFavorabilityPenalty;
        public const int MaxForcedFavorabilityPenalty = ForceEncounterConstants.Gameplay.MaxForcedFavorabilityPenalty;
        public const int TaiwuVillagerForcedFavorabilityPenaltyPercent = ForceEncounterConstants.Gameplay.TaiwuVillagerForcedFavorabilityPenaltyPercent;

        public static bool IsResolvedSuccess(bool battleSucceeded, bool targetIsTaiwu)
        {
            return !targetIsTaiwu && battleSucceeded;
        }

        public static int CalculateForcedFavorabilityDelta(int configuredPenalty, bool targetIsTaiwuVillager)
        {
            int penalty = Math.Clamp(
                configuredPenalty,
                MinForcedFavorabilityPenalty,
                MaxForcedFavorabilityPenalty);
            if (targetIsTaiwuVillager)
            {
                penalty = penalty * TaiwuVillagerForcedFavorabilityPenaltyPercent / 100;
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
