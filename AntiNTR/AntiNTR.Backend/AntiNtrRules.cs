namespace AntiNTR.Backend
{
    internal static class AntiNtrRules
    {
        public static bool CanEvaluatePair(bool enabled, int taiwuId, int charAId, int charBId)
        {
            return enabled &&
                   taiwuId >= 0 &&
                   charAId != taiwuId &&
                   charBId != taiwuId;
        }

        public static bool ShouldPreventAll(bool preventAll)
        {
            return preventAll;
        }

        public static bool ShouldBlockProtectedSpouse(bool allowCouple, int partnerId, int protectedSpouseId)
        {
            return !allowCouple || partnerId != protectedSpouseId;
        }
    }
}
