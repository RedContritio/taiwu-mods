using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace ExampleMod.Backend
{
    [PluginConfig("ExampleMod", "RedContritio", "1.0.0")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;

        public override void Initialize()
        {
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(BackendPlugin).Assembly);
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
