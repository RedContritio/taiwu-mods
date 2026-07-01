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
            int targetFavorabilityType)
        {
            return EvaluateIntimateEncounter(
                isSpouse,
                isMutualLover,
                targetAdoresActor,
                targetIsDeepValleyCloseFriend,
                isTaiwuVillager,
                targetFavorabilityType).Accepted;
        }

        public static ForceEncounterIntimateAcceptanceDecision EvaluateIntimateEncounter(
            bool isSpouse,
            bool isMutualLover,
            bool targetAdoresActor,
            bool targetIsDeepValleyCloseFriend,
            bool isTaiwuVillager,
            int targetFavorabilityType)
        {
            // 亲密来源：配偶 / 双向爱慕 / 目标单恋太吾 / 谷中密友 / 太吾村民。
            bool hasIntimateSource = isSpouse ||
                                     isMutualLover ||
                                     targetAdoresActor ||
                                     targetIsDeepValleyCloseFriend ||
                                     isTaiwuVillager;
            if (!hasIntimateSource)
            {
                return new ForceEncounterIntimateAcceptanceDecision(
                    false,
                    "NoIntimateSource",
                    hasIntimateSource: false,
                    requiredFavorabilityType: 0,
                    targetFavorabilityPassed: false);
            }

            // 统一门槛：只看目标对太吾的好感，达到「热忱」(Favorite2 + 1) 即可。
            // 不看太吾对目标的好感，也不按性格浮动。
            int requiredFavorabilityType = ForceEncounterConstants.FavorabilityTypes.Favorite2 + 1;
            bool targetFavorabilityPassed = targetFavorabilityType >= requiredFavorabilityType;
            return new ForceEncounterIntimateAcceptanceDecision(
                targetFavorabilityPassed,
                targetFavorabilityPassed ? "AcceptedByFavorability" : "FavorabilityTooLow",
                hasIntimateSource: true,
                requiredFavorabilityType,
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
    }
}
