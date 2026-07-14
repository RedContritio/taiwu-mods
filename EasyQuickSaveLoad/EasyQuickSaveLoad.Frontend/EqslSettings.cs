namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// Reads mod settings via the game's ModManager.GetSetting API.
    /// Defaults are used when the setting is absent (e.g. first run before config.lua is saved).
    /// </summary>
    internal static class EqslSettings
    {
        public static bool QuickSaveConfirm
        {
            get
            {
                bool v = true;
                try { ModManager.GetSetting(EqslCore.ModId, "QuickSaveConfirm", ref v); } catch { }
                return v;
            }
        }

        public static bool QuickLoadConfirm
        {
            get
            {
                bool v = true;
                try { ModManager.GetSetting(EqslCore.ModId, "QuickLoadConfirm", ref v); } catch { }
                return v;
            }
        }

        /// <summary>Number of manual slots. Clamped to [1, 99] so a bad config can't produce a zero/negative
        /// or absurd grid.</summary>
        public static int SlotCount
        {
            get
            {
                int v = 20;
                try { ModManager.GetSetting(EqslCore.ModId, "SlotCount", ref v); } catch { }
                if (v < 1) v = 1;
                if (v > 99) v = 99;
                return v;
            }
        }
    }
}
