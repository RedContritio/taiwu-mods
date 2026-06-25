using System.Collections.Generic;

namespace DreamLover.Backend
{
    internal static class DreamLoverRules
    {
        // 有序档位筛选用 [min,max] 闭区间表示（下拉框两端，索引 0 起始）。各维度的等级值即区间索引：
        //  - 好感：favorType(-6..6) 偏移成索引 favorType+6 (0..12)
        //  - 立场：goodnessLevel(BehaviorType, 0..4)
        //  - 魅力：charmLevel(GetAttractionType, 0..8)
        //  - 阶层：rankLevel(GetInteractionGrade, 0=九品..8=一品)
        //  - 入魔：infectState(GetInfectionState, 0..2)
        // 区间端点顺序无关（自动取小/大），两端取到底=不限该维度，两端同档=仅该档。
        public static bool PassBasicFilters(
            bool acceptSameGender,
            bool sameGender,
            bool ignoreDistance,
            bool sameLocation,
            int ageYears,
            int minAge,
            int maxAge,
            sbyte favorType,
            int favorMin,
            int favorMax,
            int goodnessLevel,
            int goodMin,
            int goodMax,
            sbyte charmLevel,
            int charmMin,
            int charmMax,
            sbyte rankLevel,
            int rankMin,
            int rankMax,
            int infectState,
            int infectMin,
            int infectMax)
        {
            if (!acceptSameGender && sameGender)
                return false;
            if (!ignoreDistance && !sameLocation)
                return false;
            if (ageYears < minAge || ageYears > maxAge)
                return false;

            if (!InBand(favorType + 6, favorMin, favorMax))
                return false;
            if (!InBand(goodnessLevel, goodMin, goodMax))
                return false;
            if (!InBand(charmLevel, charmMin, charmMax))
                return false;
            if (!InBand(rankLevel, rankMin, rankMax))
                return false;
            if (!InBand(infectState, infectMin, infectMax))
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

        // 只对【普通生成的凡人 NPC】生效，排除游戏为剧情机制生成的特殊角色：
        //  - creatingType != 1：固定/剧情角色(CreateFixedCharacter, type 0)、敌人模板(type 3) 等非普通生成角色；
        //  - isTemporary：临时智能角色(奇遇/事件临时生成, IsTemporaryIntelligentCharacter)。
        // 与引擎自身「受保护角色」判定(Character.IsNaturalDeathForbidden 的前两条)一致——即只认会自然老死的普通人口。
        public static bool IsOrdinaryRelationshipTarget(int creatingType, bool isTemporary)
        {
            return creatingType == 1 && !isTemporary;
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

        // 闭区间判断，端点顺序无关（用户把下限/上限填反也能正常成区间）。
        private static bool InBand(int value, int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return value >= lo && value <= hi;
        }
    }
}
