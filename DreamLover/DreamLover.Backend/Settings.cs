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

        public static bool[] Favor = new bool[13];
        public static bool[] Good = new bool[5];
        public static bool[] Charm = new bool[9];

        public static bool IgnoreGang;
        public static bool MarriedKiller;
        public static bool Polygynous;
        public static bool MonkKiller;
        public static bool CharmingBonze;

        public static bool[] Rank = new bool[9];
        public static bool[] Infect = new bool[3];
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

        private static int GetSlider(string modId, string key)
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

            MinAge = GetSlider(modId, "MinAge");
            MaxAge = GetSlider(modId, "MaxAge");

            for (int i = 0; i < 13; i++)
                Favor[i] = GetToggle(modId, "Favor_" + i);
            for (int i = 0; i < 5; i++)
                Good[i] = GetToggle(modId, "Good_" + i);
            for (int i = 0; i < 9; i++)
                Charm[i] = GetToggle(modId, "Charm_" + i);

            for (int i = 0; i < 9; i++)
                Rank[i] = GetToggle(modId, "Rank_" + i);
            for (int i = 0; i < 3; i++)
                Infect[i] = GetToggle(modId, "Infect_" + i);

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
