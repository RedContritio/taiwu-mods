using System;
using System.Reflection;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    [PluginConfig("CricketSingGradeColor", "RedContritio", "1.0.0.0")]
    public sealed class FrontendPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;
        private string _modId;

        public override void Initialize()
        {
            try
            {
                _modId = ModIdStr;
                ReloadSettings();
                _harmony = new Harmony(GetGuid());
                _harmony.PatchAll(typeof(FrontendPlugin).Assembly);
                // The cricket card-view ripple renderers (CricketView, and the deeply-nested CricketViewNew)
                // are namespace/nesting-awkward to target with [HarmonyPatch(typeof(...))], so patch their
                // Sing(...) by name-resolved reflection with one shared Postfix.
                PatchCricketViewSing("CricketView");
                PatchCricketViewSing("CricketViewNew");
                Debug.Log("[CricketSingGradeColor] Frontend initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CricketSingGradeColor] Frontend initialization failed: " + ex);
            }
        }

        // Fired by the game when the player changes a setting in 模组管理. We re-read the weight; the
        // calculator notices the changed weight and re-calibrates on the next ripple, so it applies live.
        public override void OnModSettingUpdate()
        {
            ReloadSettings();
        }

        /// <summary>Reads the "BlendMode" dropdown (0 音高优先 / 1 均衡 / 2 音量优先) into SoundGradeConfig.</summary>
        private void ReloadSettings()
        {
            try
            {
                int index = SoundGradeConfig.DefaultBlendIndex;
                if (!string.IsNullOrEmpty(_modId))
                {
                    ModManager.GetSetting(_modId, "BlendMode", ref index);
                }
                SoundGradeConfig.ApplyBlend(index);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CricketSingGradeColor] Could not read BlendMode setting, using default: " + ex);
            }
        }

        /// <summary>Finds <paramref name="typeName"/> in Assembly-CSharp (by simple name, so it works for
        /// top-level and nested types alike) and Postfixes its <c>Sing</c> method with the colour patch.</summary>
        private void PatchCricketViewSing(string typeName)
        {
            try
            {
                Type type = FindAssemblyCSharpType(typeName);
                if (type == null)
                {
                    Debug.LogWarning("[CricketSingGradeColor] type not found, skipping: " + typeName);
                    return;
                }

                MethodInfo method = AccessTools.Method(type, "Sing", new[]
                {
                    typeof(bool), typeof(bool), typeof(bool), typeof(float), typeof(Action<float>), typeof(float),
                });
                if (method == null)
                {
                    Debug.LogWarning("[CricketSingGradeColor] Sing(...) not found on " + typeName);
                    return;
                }

                MethodInfo postfix = typeof(CricketViewColorPatch).GetMethod(
                    nameof(CricketViewColorPatch.Postfix), BindingFlags.Public | BindingFlags.Static);
                _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                Debug.Log("[CricketSingGradeColor] patched " + typeName + ".Sing");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CricketSingGradeColor] patching " + typeName + ".Sing failed: " + ex);
            }
        }

        private static Type FindAssemblyCSharpType(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name != "Assembly-CSharp")
                {
                    continue;
                }

                Type[] types;
                try { types = a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                foreach (Type t in types)
                {
                    if (t != null && t.Name == name)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        public override void Dispose()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CricketSingGradeColor] Frontend dispose failed: " + ex);
            }
            finally
            {
                _harmony = null;
            }
        }
    }
}
