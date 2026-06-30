using System.Collections.Generic;

namespace DreamLover.Backend
{
    internal static class DreamLoverRules
    {
        // 廉价门（无关系查询）：性别 / 追求范围 / 年龄。先于较重的好感/关系查询，用以挡掉绝大多数 NPC（性能）。
        // 性别：六个开关「或」——命中任一被勾选的类别即通过；全不勾=无人。
        //  同性/异性 按生理性别相对太吾；男性/女性 为生理性别；男生女相=生理男且变装、女生男相=生理女且变装(显示性别≠生理性别)。
        // 追求范围（递进）：0=同道(仅太吾同伴) ⊂ 1=同格 ⊂ 2=同区域 ⊂ 3+=不受距离限制。
        public static bool PassGenderRangeAge(
            bool acceptSameGender,
            bool acceptOppositeGender,
            bool acceptMale,
            bool acceptFemale,
            bool acceptMaleLooksFemale,
            bool acceptFemaleLooksMale,
            sbyte npcGender,
            sbyte taiwuGender,
            bool npcIsTransgender,
            int range,
            bool npcInTaiwuGroup,
            bool sameLocation,
            bool sameArea,
            int ageYears,
            int minAge,
            int maxAge)
        {
            bool sameGender = npcGender == taiwuGender;
            bool npcIsMale = npcGender == 1;
            bool genderOk =
                (acceptSameGender && sameGender) ||
                (acceptOppositeGender && !sameGender) ||
                (acceptMale && npcIsMale) ||
                (acceptFemale && !npcIsMale) ||
                (acceptMaleLooksFemale && npcIsMale && npcIsTransgender) ||
                (acceptFemaleLooksMale && !npcIsMale && npcIsTransgender);
            if (!genderOk)
                return false;

            bool geoOk = range <= 0
                ? npcInTaiwuGroup
                : range == 1 ? (sameLocation || npcInTaiwuGroup)
                : range == 2 ? (sameArea || npcInTaiwuGroup)
                : true;
            if (!geoOk)
                return false;

            if (ageYears < minAge || ageYears > maxAge)
                return false;

            return true;
        }

        // 逐档筛选：每维度一个 bool[]（索引=档位，勾选=接受该档）。各维度等级值即档位索引：
        //  好感 favorType(-6..6)→索引+6(0..12)、立场 BehaviorType(0..4)、魅力 GetAttractionType(0..8)、
        //  品级 GetInteractionGrade(0=九品..8=一品)、入魔 GetInfectionState(0..2)。
        // NPC 的档位索引必须被勾选才通过；某维度一个都不勾=该维度无人通过。
        public static bool PassTiers(
            sbyte favorType,
            IReadOnlyList<bool> favorTiers,
            int goodnessLevel,
            IReadOnlyList<bool> goodTiers,
            sbyte charmLevel,
            IReadOnlyList<bool> charmTiers,
            sbyte rankLevel,
            IReadOnlyList<bool> rankTiers,
            bool rankAutoMin,
            int autoMinRankIndex,
            int infectState,
            IReadOnlyList<bool> infectTiers)
        {
            if (!TierAllowed(favorType + 6, favorTiers))
                return false;
            if (!TierAllowed(goodnessLevel, goodTiers))
                return false;
            if (!TierAllowed(charmLevel, charmTiers))
                return false;
            // 品级：自适应下限开启时忽略手动勾选，仅收 品级 >= 相枢等级；否则按逐档勾选。
            if (rankAutoMin)
            {
                if (rankLevel < autoMinRankIndex)
                    return false;
            }
            else if (!TierAllowed(rankLevel, rankTiers))
            {
                return false;
            }
            if (!TierAllowed(infectState, infectTiers))
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

        // 逐档筛选：NPC 的档位索引必须被勾选；越界或未勾选=不通过（某维度全不勾即无人通过）。
        private static bool TierAllowed(int index, IReadOnlyList<bool> tiers)
        {
            return index >= 0 && index < tiers.Count && tiers[index];
        }
    }
}
