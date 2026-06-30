namespace FertilityControl.Backend
{
    internal static class FertilityRules
    {
        public const int PregnancyCarrierNativeFather = 0;
        public const int PregnancyCarrierNativeMother = 1;
        public const int PregnancyCarrierTaiwu = 2;
        public const int PregnancyCarrierPartner = 3;

        public static bool PassChildLimit(
            int fatherChildren,
            int motherChildren,
            short fatherFertility,
            short motherFertility,
            bool ignoreChildLimit = false)
        {
            if (ignoreChildLimit)
            {
                return true;
            }

            return fatherFertility / 20 >= fatherChildren &&
                   motherFertility / 20 >= motherChildren;
        }

        public static bool CanUseNativePregnancyRole(
            int fatherGender,
            bool fatherHasFlexibleRole,
            int motherGender,
            bool motherHasFlexibleRole)
        {
            return (fatherGender == 1 || fatherHasFlexibleRole) &&
                   (motherGender == 0 || motherHasFlexibleRole);
        }

        public static bool CanResolveNativeMakeLoveRole(
            int firstGender,
            bool firstHasFlexibleRole,
            int secondGender,
            bool secondHasFlexibleRole)
        {
            return firstHasFlexibleRole || secondHasFlexibleRole || firstGender != secondGender;
        }

        public static bool ShouldBypassNativeRoleGate(
            bool allowNonstandardPregnancyRoles,
            bool involvesTaiwu,
            bool nativeGateAllows)
        {
            return allowNonstandardPregnancyRoles && involvesTaiwu && !nativeGateAllows;
        }

        public static bool ShouldBypassPregnancyRoleCheck(
            bool allowNonstandardPregnancyRoles,
            bool involvesTaiwu,
            bool nativeRoleAllows,
            bool nativeAssignmentCanResolve,
            int pregnancyCarrierMode)
        {
            return involvesTaiwu &&
                   !nativeRoleAllows &&
                   (allowNonstandardPregnancyRoles ||
                    (nativeAssignmentCanResolve && IsConfiguredCarrierMode(pregnancyCarrierMode)));
        }

        public static bool ShouldUseConfiguredCarrier(int pregnancyCarrierMode, bool involvesTaiwu)
        {
            return involvesTaiwu && IsConfiguredCarrierMode(pregnancyCarrierMode);
        }

        public static bool ShouldSwapForPregnancyCarrier(
            int pregnancyCarrierMode,
            int taiwuId,
            int fatherId,
            int motherId)
        {
            return pregnancyCarrierMode switch
            {
                PregnancyCarrierNativeFather => true,
                PregnancyCarrierTaiwu => fatherId == taiwuId,
                PregnancyCarrierPartner => motherId == taiwuId,
                _ => false
            };
        }

        public static int NormalizePregnancyCarrierMode(int pregnancyCarrierMode)
        {
            return pregnancyCarrierMode is PregnancyCarrierNativeFather
                or PregnancyCarrierNativeMother
                or PregnancyCarrierTaiwu
                or PregnancyCarrierPartner
                    ? pregnancyCarrierMode
                    : PregnancyCarrierNativeMother;
        }

        private static bool IsConfiguredCarrierMode(int pregnancyCarrierMode)
        {
            return pregnancyCarrierMode is PregnancyCarrierNativeFather
                or PregnancyCarrierTaiwu
                or PregnancyCarrierPartner;
        }

        public static bool ShouldOverridePregnancyCheck(
            bool fertilityOverridden,
            bool rateOverridden,
            bool ignoreChildLimit)
        {
            return fertilityOverridden || rateOverridden || ignoreChildLimit;
        }

        public static int CalculatePregnancyChance(
            bool isRape,
            short fatherFertility,
            short motherFertility,
            bool rateOverridden,
            int pregnancyRate)
        {
            if (rateOverridden)
            {
                return pregnancyRate;
            }

            int baseChance = isRape ? 20 : 60;
            return (int)((long)baseChance * fatherFertility * motherFertility / 10000);
        }

        public static bool ShouldBlockForZeroFertility(
            short fatherFertility,
            short motherFertility,
            bool rateOverridden)
        {
            return !rateOverridden && (fatherFertility <= 0 || motherFertility <= 0);
        }

        public static bool IsTaiwuInvolved(int taiwuId, int firstCharId, int secondCharId)
        {
            return taiwuId >= 0 && (firstCharId == taiwuId || secondCharId == taiwuId);
        }

        public static bool ShouldOverrideInbreeding(bool disableInbreeding, bool involvesTaiwu)
        {
            return disableInbreeding && involvesTaiwu;
        }

        public static bool ShouldOverrideCricketRate(bool setCricketRate, bool hadPregnantState, bool involvesTaiwu)
        {
            return setCricketRate && !hadPregnantState && involvesTaiwu;
        }
    }
}
