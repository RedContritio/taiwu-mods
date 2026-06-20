using System;
using GameData.Domains;

namespace TaiwuMod.Common
{
    internal static class TaiwuModSettings
    {
        public static bool GetBool(string modId, string key, bool fallback)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return fallback;
            }

            bool value = fallback;
            return DomainManager.Mod.GetSetting(modId, key, ref value) ? value : fallback;
        }

        public static int GetInt(string modId, string key, int fallback)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return fallback;
            }

            int value = fallback;
            return DomainManager.Mod.GetSetting(modId, key, ref value) ? value : fallback;
        }

        public static int GetClampedInt(string modId, string key, int fallback, int min, int max)
        {
            return Math.Clamp(GetInt(modId, key, fallback), min, max);
        }
    }
}
