using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace ExampleMod.Frontend
{
    [PluginConfig("ExampleMod", "RedContritio", "1.0.0")]
    public class FrontendPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;

        public override void Initialize()
        {
            _harmony = new Harmony(GetGuid());
            _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
            UnityEngine.Debug.Log("[ExampleMod] Frontend initialized.");
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
