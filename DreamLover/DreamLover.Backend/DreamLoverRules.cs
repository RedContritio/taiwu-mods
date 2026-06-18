using System.Collections.Generic;

namespace DreamLover.Backend
{
    internal static class DreamLoverRules
    {
        public static bool PassBasicFilters(
            bool acceptSameGender,
            bool sameGender,
            bool ignoreDistance,
            bool sameLocation,
            int ageYears,
            int minAge,
            int maxAge,
            sbyte favorType,
            IReadOnlyList<bool> favor,
            int goodnessLevel,
            IReadOnlyList<bool> good,
            sbyte charmLevel,
            IReadOnlyList<bool> charm,
            sbyte rankLevel,
            IReadOnlyList<bool> rank,
            int infectState,
            IReadOnlyList<bool> infect)
        {
            if (!acceptSameGender && sameGender)
                return false;
            if (!ignoreDistance && !sameLocation)
                return false;
            if (ageYears < minAge || ageYears > maxAge)
                return false;

            int favorIdx = favorType + 6;
            if (!IsAllowed(favor, favorIdx))
                return false;
            if (!IsAllowed(good, goodnessLevel))
                return false;
            if (!IsAllowed(charm, charmLevel))
                return false;
            if (rankLevel >= 0 && rankLevel < rank.Count && !rank[rankLevel])
                return false;
            if (infectState >= 0 && infectState < infect.Count && !infect[infectState])
                return false;

            return true;
        }

        public static bool PassRelationFilter(
            ushort relationMask,
            IReadOnlyList<(string Key, ushort Type)> relationDefs,
            IReadOnlyList<bool> relationAllowed)
        {
            for (int i = 0; i < relationDefs.Count; i++)
            {
                if ((relationMask & relationDefs[i].Type) != 0 && !relationAllowed[i])
                    return false;
            }

            return true;
        }

        public static bool ShouldForgetUnreciprocatedAdoration(
            bool forgetMe,
            bool enableEnamor,
            bool enablePursued,
            bool npcAdoresTaiwu,
            bool npcIsSpouse,
            bool taiwuAdoresNpc)
        {
            return forgetMe &&
                   !enableEnamor &&
                   !enablePursued &&
                   npcAdoresTaiwu &&
                   !npcIsSpouse &&
                   !taiwuAdoresNpc;
        }

        private static bool IsAllowed(IReadOnlyList<bool> values, int index)
        {
            return index >= 0 && index < values.Count && values[index];
        }
    }
}
