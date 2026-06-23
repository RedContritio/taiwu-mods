using System;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace Bridge.Frontend
{
    [PluginConfig("Bridge", "RedContritio", "0.0.1")]
    public class FrontendPlugin : TaiwuRemakePlugin
    {
        private static readonly Type ModManagerType = AccessTools.TypeByName("ModManager");

        private PipeServer _server;

        public override void Initialize()
        {
            try
            {
                MainThreadDispatcher.Create();
                StartServerFromSettings();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Bridge] init failed: " + ex);
            }
        }

        public override void Dispose()
        {
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
            bool enabled = GetBoolSetting("Enabled", true);
            Router.DefaultMax = Math.Max(10, GetIntSetting("DefaultMax", 60));

            if (!enabled)
            {
                Debug.Log("[Bridge] disabled by setting; pipe bridge not started.");
                return;
            }

            _server = new PipeServer();
            try
            {
                _server.Start();
                Debug.Log($"[Bridge] named pipe bridge listening on \\\\.\\pipe\\{PipeServer.PipeName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bridge] failed to start pipe server: {ex.Message}");
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
