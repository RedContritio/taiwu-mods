namespace FertilityControl.Backend
{
    internal static class FertilityRules
    {
        public static bool PassChildLimit(
            int fatherChildren,
            int motherChildren,
            short fatherFertility,
            short motherFertility)
        {
            return fatherFertility / 20 >= fatherChildren &&
                   motherFertility / 20 >= motherChildren;
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

        public static int CalculateTotalFertilityChance(bool isRape, int totalFertility)
        {
            int baseChance = isRape ? 20 : 60;
            return baseChance * totalFertility / 10000;
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
