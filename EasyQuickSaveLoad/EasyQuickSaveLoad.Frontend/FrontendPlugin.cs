using System;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EasyQuickSaveLoad.Frontend
{
    [PluginConfig("EasyQuickSaveLoad", "RedContritio", "1.0.0.0")]
    public sealed class FrontendPlugin : TaiwuRemakePlugin
    {
        private EasyQuickSaveLoadOverlay _overlay;

        public override void Initialize()
        {
            try
            {
                // Use the runtime mod id (matches the backend's registration key) for backend mod-method calls.
                EasyQuickSaveLoadOverlay.SetModId(ModIdStr);
                _overlay = EasyQuickSaveLoadOverlay.Create();
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
                EasyQuickSaveLoadOverlay.Destroy(_overlay);
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyQuickSaveLoad] Frontend dispose failed: " + ex);
            }
            finally
            {
                _overlay = null;
            }
        }
    }
}
