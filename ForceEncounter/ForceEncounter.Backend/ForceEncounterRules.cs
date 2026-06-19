using System;

namespace ForceEncounter.Backend
{
    internal static class ForceEncounterRules
    {
        public const int DefaultForcedFavorabilityPenalty = 30000;
        public const int MinForcedFavorabilityPenalty = 0;
        public const int MaxForcedFavorabilityPenalty = 30000;
        public const int TaiwuVillagerForcedFavorabilityPenaltyPercent = 30;

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
            bool isTaiwuVillager,
            int actorFavorabilityType,
            int targetFavorabilityType,
            int actorBehaviorType)
        {
            if (!isSpouse && !isMutualLover && !isTaiwuVillager)
            {
                return false;
            }

            int requiredFavorabilityType = GetIntimateAcceptanceFavorabilityType(actorBehaviorType);
            if (isTaiwuVillager)
            {
                requiredFavorabilityType = Math.Max(3, requiredFavorabilityType - 1);
            }

            if (!isSpouse && !isMutualLover)
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
