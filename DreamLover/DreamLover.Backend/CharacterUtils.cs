using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;

namespace DreamLover.Backend
{
    public static class CharacterUtils
    {
        public static sbyte GetCharmLevel(Character c)
        {
            return AttractionType.GetAttractionType(c.GetAttraction());
        }

        public static sbyte GetFavorabilityType(int charId, int relatedCharId)
        {
            return DomainManager.Character.GetFavorabilityType(charId, relatedCharId);
        }

        public static sbyte GetGoodnessLevel(Character c)
        {
            sbyte bt = c.GetBehaviorType();
            if (bt >= 0 && bt <= 4) return bt;
            return 2;
        }

        public static bool HasRelation(int charId, int targetId, ushort relationType)
        {
            return DomainManager.Character.HasRelation(charId, targetId, relationType);
        }

        public static bool IsMarried(int charId)
        {
            return DomainManager.Character.GetAliveSpouse(charId) >= 0;
        }

        public static sbyte GetRankLevel(Character c)
        {
            return c.GetInteractionGrade();
        }

        public static int GetInfectionState(Character c)
        {
            if (c.IsCompletelyInfected()) return 2;
            if (c.IsPartiallyInfected()) return 1;
            return 0;
        }

        public static bool IsAtSameLocation(Character a, Character b)
        {
            var locA = a.GetLocation();
            var locB = b.GetLocation();
            return locA.AreaId == locB.AreaId && locA.BlockId == locB.BlockId;
        }

        public static bool IsAtSameArea(Character a, Character b)
        {
            return a.GetLocation().AreaId == b.GetLocation().AreaId;
        }
    }
}
