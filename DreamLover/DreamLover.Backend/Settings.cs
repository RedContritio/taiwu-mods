using GameData.Domains;

namespace DreamLover.Backend
{
    public static class Settings
    {
        public static bool EnableEnamor;
        public static bool EnablePursued;
        public static bool EnableMarry;
        public static bool ForgetMe;

        public static bool AcceptSameGender;
        public static bool IgnoreDistance;

        public static int MinAge;
        public static int MaxAge;

        // 有序档位筛选：以 0 起始的档位索引表示 [下限, 上限] 闭区间（下拉框存索引，显示档位名）。
        // 顺序与游戏取值一致：好感=好感档+6、立场=BehaviorType、魅力=GetAttractionType、阶层=GetInteractionGrade(0=九品..8=一品)、入魔=GetInfectionState。
        public static int FavorMin, FavorMax;
        public static int GoodMin, GoodMax;
        public static int CharmMin, CharmMax;
        public static int RankMin, RankMax;
        public static int InfectMin, InfectMax;

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
            IgnoreDistance = GetToggle(modId, "IgnoreDistance");

            MinAge = GetInt(modId, "MinAge");
            MaxAge = GetInt(modId, "MaxAge");

            FavorMin = GetInt(modId, "FavorMin");
            FavorMax = GetInt(modId, "FavorMax");
            GoodMin = GetInt(modId, "GoodMin");
            GoodMax = GetInt(modId, "GoodMax");
            CharmMin = GetInt(modId, "CharmMin");
            CharmMax = GetInt(modId, "CharmMax");
            RankMin = GetInt(modId, "RankMin");
            RankMax = GetInt(modId, "RankMax");
            InfectMin = GetInt(modId, "InfectMin");
            InfectMax = GetInt(modId, "InfectMax");

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
