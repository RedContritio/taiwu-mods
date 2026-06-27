using System;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EasyBridge.Frontend
{
    [PluginConfig("EasyBridge", "RedContritio", "0.0.1")]
    public class FrontendPlugin : TaiwuRemakePlugin
    {
        private PipeServer _server;
        private UnityEngine.GameObject _overlay;

        public override void Initialize()
        {
            try
            {
                MainThreadDispatcher.Create();
                StartServer();
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
            StartServer();
        }

        // No user settings — this is an agent-driven debug bridge. Router behavior uses code defaults
        // (DefaultMax / MaxReflectDepth / MaxReflectMembers / EnableReflectInvoke) and is tuned per-request
        // via query params or at runtime via POST /config.
        private void StartServer()
        {
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
