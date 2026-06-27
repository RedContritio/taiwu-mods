using System;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EasyBridge.Frontend
{
    [PluginConfig("EasyBridge", "RedContritio", "0.0.1")]
    public class FrontendPlugin : TaiwuRemakePlugin
    {
        private static readonly Type ModManagerType = AccessTools.TypeByName("ModManager");

        private PipeServer _server;
        private UnityEngine.GameObject _overlay;

        public override void Initialize()
        {
            try
            {
                MainThreadDispatcher.Create();
                StartServerFromSettings();
                try { _overlay = MonitorOverlay.Create(); }
                catch (Exception ex) { Debug.LogError("[EasyBridge] monitor overlay failed: " + ex.Message); }
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyBridge] init failed: " + ex);
            }
        }

        public override void Dispose()
        {
            try { if (_overlay != null) UnityEngine.Object.Destroy(_overlay); } catch { }
            _overlay = null;
            StopServer();
            MainThreadDispatcher.Destroy();
        }

        public override void OnModSettingUpdate()
        {
            StopServer();
            StartServerFromSettings();
        }

        private void StartServerFromSettings()
        {
            Router.DefaultMax = Math.Max(10, GetIntSetting("DefaultMax", 60));
            Router.MaxReflectDepth = Math.Max(0, Math.Min(3, GetIntSetting("MaxReflectDepth", 2)));
            Router.MaxReflectMembers = Math.Max(1, Math.Min(200, GetIntSetting("MaxReflectMembers", 80)));
            Router.EnableReflectInvoke = GetBoolSetting("EnableReflectInvoke", false);

            _server = new PipeServer();
            try
            {
                _server.Start();
                Debug.Log($"[EasyBridge] named pipe bridge listening on \\\\.\\pipe\\{PipeServer.PipeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EasyBridge] failed to start pipe server: {ex.Message}");
                _server = null;
            }
        }

        private void StopServer()
        {
            try { _server?.Stop(); } catch { }
            _server = null;
        }

        private int GetIntSetting(string key, int fallback)
        {
            var m = AccessTools.Method(ModManagerType, "GetSetting",
                new[] { typeof(string), typeof(string), typeof(int).MakeByRefType() });
            if (m == null) return fallback;
            object[] args = { ModIdStr, key, fallback };
            try { return (bool)m.Invoke(null, args) ? (int)args[2] : fallback; }
            catch { return fallback; }
        }

        private bool GetBoolSetting(string key, bool fallback)
        {
            var m = AccessTools.Method(ModManagerType, "GetSetting",
                new[] { typeof(string), typeof(string), typeof(bool).MakeByRefType() });
            if (m == null) return fallback;
            object[] args = { ModIdStr, key, fallback };
            try { return (bool)m.Invoke(null, args) ? (bool)args[2] : fallback; }
            catch { return fallback; }
        }
    }
}
