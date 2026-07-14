using System;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EasyQuickSaveLoad.Frontend
{
    [PluginConfig("EasyQuickSaveLoad", "RedContritio", "1.0.1.0")]
    public sealed class FrontendPlugin : TaiwuRemakePlugin
    {
        private SystemOptionButtonInjector _injector;
        private Harmony _harmony;

        public override void Initialize()
        {
            try
            {
                // Use the runtime mod id (matches the backend's registration key) for backend mod-method calls.
                EqslCore.ModId = ModIdStr;
                _injector = SystemOptionButtonInjector.Create();

                // Guard the game's TooltipManager against the reload-time NRE (see TooltipCrashGuardPatch).
                _harmony = new Harmony(GetGuid());
                _harmony.PatchAll(typeof(FrontendPlugin).Assembly);

                Debug.Log("[EasyQuickSaveLoad] Frontend initialized. ModId=" + ModIdStr);
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyQuickSaveLoad] Frontend initialization failed: " + ex);
            }
        }

        public override void Dispose()
        {
            try
            {
                SystemOptionButtonInjector.Destroy(_injector);
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyQuickSaveLoad] Frontend dispose failed: " + ex);
            }
            finally
            {
                _injector = null;
                _harmony = null;
            }
        }
    }
}
