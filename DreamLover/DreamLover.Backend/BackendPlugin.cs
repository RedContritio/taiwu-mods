using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace DreamLover.Backend
{
    [PluginConfig("DreamLover", "RedContritio", "2.0.1")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        internal static string ModId;
        private Harmony _harmony;

        public override void Initialize()
        {
            ModId = ModIdStr;
            Settings.Reload(ModId);
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }

        public override void OnModSettingUpdate()
        {
            Settings.Reload(ModId);
        }
    }
}
