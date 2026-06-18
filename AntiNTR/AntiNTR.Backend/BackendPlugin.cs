using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace AntiNTR.Backend
{
    [PluginConfig("AntiNTR", "RedContritio", "2.0.0")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;

        internal static BackendPlugin Instance;

        public override void Initialize()
        {
            Instance = this;
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
            Instance = null;
        }

        internal bool GetBoolSetting(string key)
        {
            bool val = false;
            GameData.Domains.DomainManager.Mod.GetSetting(ModIdStr, key, ref val);
            return val;
        }
    }
}
