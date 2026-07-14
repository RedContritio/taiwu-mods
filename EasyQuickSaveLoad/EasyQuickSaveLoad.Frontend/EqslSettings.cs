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

        /// <summary>覆盖已有存档前是否弹确认框(存档选择器，默认开)。</summary>
        public static bool OverwriteConfirm
        {
            get
            {
                bool v = true;
                try { ModManager.GetSetting(EqslCore.ModId, "OverwriteConfirm", ref v); } catch { }
                return v;
            }
        }

        /// <summary>删除存档前是否弹确认框(存档选择器，默认开)。</summary>
        public static bool DeleteConfirm
        {
            get
            {
                bool v = true;
                try { ModManager.GetSetting(EqslCore.ModId, "DeleteConfirm", ref v); } catch { }
                return v;
            }
        }

        // 存档栏位按「页」配置：每页固定 5 个栏位，可用页数 1..10。
        public const int SlotsPerPage = 5;
        public const int MinPageCount = 1;
        public const int MaxPageCount = 10;
        public const int DefaultPageCount = 4;

        /// <summary>可用存档页数(配置项)，夹在 [1, 10]。调小只临时隐藏靠后的页、不删除数据。</summary>
        public static int PageCount
        {
            get
            {
                int v = DefaultPageCount;
                try { ModManager.GetSetting(EqslCore.ModId, "PageCount", ref v); } catch { }
                if (v < MinPageCount) v = MinPageCount;
                if (v > MaxPageCount) v = MaxPageCount;
                return v;
            }
        }

        /// <summary>当前可操作的栏位数 = 页数 × 每页栏位。超出的旧存档暂时隐藏(不删除)。</summary>
        public static int SlotCount => PageCount * SlotsPerPage;
    }
}
