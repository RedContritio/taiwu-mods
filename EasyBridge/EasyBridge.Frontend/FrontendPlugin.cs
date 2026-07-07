using System;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EasyBridge.Frontend
{
    [PluginConfig("EasyBridge", "RedContritio", "0.2.0.0")]
    public class FrontendPlugin : TaiwuRemakePlugin
    {
        private PipeServer _server;

        public override void Initialize()
        {
            try
            {
                MainThreadDispatcher.Create();
                StartServer();
            }
            catch (Exception ex)
            {
                Debug.LogError("[EasyBridge] init failed: " + ex);
            }
        }

        public override void Dispose()
        {
            try { MonitorOverlay.DestroyInstance(); } catch { }
            StopServer();
            MainThreadDispatcher.Destroy();
        }

        public override void OnModSettingUpdate()
        {
            Router.ReloadRuntimeOptions();
        }

        // No user settings — this is an agent-driven debug bridge. Router behavior uses code defaults
        // (DefaultMax / MaxReflectDepth / MaxReflectMembers / EnableReflectInvoke) and is tuned per-request
        // via query params or at runtime via POST /config.
        private void StartServer()
        {
            if (_server != null) return;
            Router.ReloadRuntimeOptions();
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
    }
}
