using GameData.Domains;

namespace DreamLover.Backend
{
    public static class Settings
    {
        public static bool EnableEnamor;
        public static bool EnablePursued;
        public static bool EnableMarry;
        public static bool ForgetMe;

        // 性别筛选：六个开关「或」关系，NPC 命中任一即通过；全不勾=无人通过。
        public static bool AcceptSameGender;        // 与太吾生理同性
        public static bool AcceptOppositeGender;    // 与太吾生理异性
        public static bool AcceptMale;              // 生理男性
        public static bool AcceptFemale;            // 生理女性
        public static bool AcceptMaleLooksFemale;   // 男生女相(生理男·外貌女)
        public static bool AcceptFemaleLooksMale;   // 女生男相(生理女·外貌男)

        // 追求范围（递进）：0=同道(仅太吾同伴) / 1=同格(太吾所在格,含同道) / 2=不受距离限制。
        public static int Range;

        public static int MinAge;
        public static int MaxAge;

        // 逐档筛选：每维度一个 bool[]（索引=档位，勾选=接受该档；某维度一个都不勾=该维度无人通过）。
        // 索引与游戏取值一致：好感=好感档+6(0..12)、立场=BehaviorType(0..4)、魅力=GetAttractionType(0..8)、
        // 品级=GetInteractionGrade(0=九品..8=一品)、入魔=GetInfectionState(0..2)。
        public static readonly bool[] FavorTiers = new bool[13];
        public static readonly bool[] GoodTiers = new bool[5];
        public static readonly bool[] CharmTiers = new bool[9];
        public static readonly bool[] RankTiers = new bool[9];
        public static readonly bool[] InfectTiers = new bool[3];

        // 品级自适应下限：开启则忽略 RankTiers 手动勾选，仅收 品级 >= 当前相枢等级(GetXiangshuLevel) 的 NPC。
        public static bool RankAutoMin;

        public static bool IgnoreGang;
        public static bool MarriedKiller;
        public static bool Polygynous;
        public static bool MonkKiller;
        public static bool CharmingBonze;

        public static bool[] RelFilter = new bool[11];

        public static bool DebugMode;

        public static readonly (string Key, ushort Type)[] RelFilterDefs =
        {
            ("Rel_BloodParent", 1),
            ("Rel_BloodChild", 2),
            ("Rel_BloodSibling", 4),
            ("Rel_AdoptiveParent", 64),
            ("Rel_AdoptiveChild", 128),
            ("Rel_SwornSibling", 512),
            ("Rel_Mentor", 2048),
            ("Rel_Mentee", 4096),
            ("Rel_Friend", 8192),
            ("Rel_Adored", 16384),
            ("Rel_Enemy", 32768),
        };

        private static bool GetToggle(string modId, string key)
        {
            bool val = false;
            DomainManager.Mod.GetSetting(modId, key, ref val);
            return val;
        }

        // Slider 与 Dropdown 都以 int 存值（Dropdown 存的是 0 起始的选项索引）。
        private static int GetInt(string modId, string key)
        {
            int val = 0;
            DomainManager.Mod.GetSetting(modId, key, ref val);
            return val;
        }

        public static void Reload(string modId)
        {
            EnableEnamor = GetToggle(modId, "EnableEnamor");
            EnablePursued = GetToggle(modId, "EnablePursued");
            EnableMarry = GetToggle(modId, "EnableMarry");
            ForgetMe = GetToggle(modId, "ForgetMe");

            AcceptSameGender = GetToggle(modId, "AcceptSameGender");
            AcceptOppositeGender = GetToggle(modId, "AcceptOppositeGender");
            AcceptMale = GetToggle(modId, "AcceptMale");
            AcceptFemale = GetToggle(modId, "AcceptFemale");
            AcceptMaleLooksFemale = GetToggle(modId, "AcceptMaleLooksFemale");
            AcceptFemaleLooksMale = GetToggle(modId, "AcceptFemaleLooksMale");
            Range = GetInt(modId, "Range");

            MinAge = GetInt(modId, "MinAge");
            MaxAge = GetInt(modId, "MaxAge");

            for (int i = 0; i < FavorTiers.Length; i++) FavorTiers[i] = GetToggle(modId, "Favor_" + i);
            for (int i = 0; i < GoodTiers.Length; i++) GoodTiers[i] = GetToggle(modId, "Good_" + i);
            for (int i = 0; i < CharmTiers.Length; i++) CharmTiers[i] = GetToggle(modId, "Charm_" + i);
            for (int i = 0; i < RankTiers.Length; i++) RankTiers[i] = GetToggle(modId, "Rank_" + i);
            for (int i = 0; i < InfectTiers.Length; i++) InfectTiers[i] = GetToggle(modId, "Infect_" + i);
            RankAutoMin = GetToggle(modId, "RankAutoMin");

            IgnoreGang = GetToggle(modId, "IgnoreGang");
            MarriedKiller = GetToggle(modId, "MarriedKiller");
            Polygynous = GetToggle(modId, "Polygynous");
            MonkKiller = GetToggle(modId, "MonkKiller");
            CharmingBonze = GetToggle(modId, "CharmingBonze");

            for (int i = 0; i < RelFilterDefs.Length; i++)
                RelFilter[i] = GetToggle(modId, RelFilterDefs[i].Key);

            DebugMode = GetToggle(modId, "DebugMode");
        }
    }
}
