using System;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace CombatTargetDistanceFix.Frontend
{
    [PluginConfig("CombatTargetDistanceFix", "RedContritio", "1.0.0")]
    public sealed class FrontendPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;

        public override void Initialize()
        {
            try
            {
                _harmony = new Harmony(GetGuid());
                _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
                Debug.Log("[CombatTargetDistanceFix] Frontend initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CombatTargetDistanceFix] Frontend initialization failed: " + ex);
            }
        }

        public override void Dispose()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CombatTargetDistanceFix] Frontend dispose failed: " + ex);
            }
            finally
            {
                _harmony = null;
            }
        }
    }
}
