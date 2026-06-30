using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace FertilityControl.Backend
{
    [PluginConfig("FertilityControl", "RedContritio", "1.0.0.0")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        internal static string ModId;
        private Harmony _harmony;

        public override void Initialize()
        {
            ModId = ModIdStr;
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
