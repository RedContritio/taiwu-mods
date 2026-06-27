using System.Collections.Generic;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Holds ("pins") fields/properties at a fixed value by re-applying them every frame (from
    /// <see cref="MainThreadDispatcher.Update"/>). This is the clean way to neutralise a per-frame
    /// self-incrementing counter — e.g. a catch minigame's countdown <c>_timer</c> that auto-times-out —
    /// WITHOUT pausing the engine: time keeps flowing, animations (DOTween ripples) play normally, only the
    /// pinned field is held constant. The setter is resolved once and re-applied each tick.
    /// </summary>
    internal static class PinController
    {
        private sealed class Pin
        {
            public string Key;
            public ReflectionInspector.SetterHandle Handle;
        }

        private static readonly List<Pin> _pins = new List<Pin>();
        private static readonly object _lock = new object();

        public static Dictionary<string, object> Add(string requestBody)
        {
            var h = ReflectionInspector.ResolveSetter(requestBody);
            if (!h.Ok) return new Dictionary<string, object> { ["ok"] = false, ["error"] = h.Error };
            h.Apply();
            lock (_lock)
            {
                _pins.RemoveAll(p => p.Key == h.Descr); // replace any existing pin on the same target
                _pins.Add(new Pin { Key = h.Descr, Handle = h });
                return new Dictionary<string, object> { ["ok"] = true, ["pinned"] = h.Descr, ["count"] = _pins.Count };
            }
        }

        public static Dictionary<string, object> Clear()
        {
            lock (_lock)
            {
                int n = _pins.Count;
                _pins.Clear();
                return new Dictionary<string, object> { ["ok"] = true, ["cleared"] = n };
            }
        }

        public static Dictionary<string, object> List()
        {
            lock (_lock)
            {
                var keys = new List<object>();
                foreach (var p in _pins) keys.Add(p.Key);
                return new Dictionary<string, object> { ["ok"] = true, ["count"] = _pins.Count, ["pins"] = keys };
            }
        }

        public static void Tick()
        {
            lock (_lock)
            {
                for (int i = _pins.Count - 1; i >= 0; i--)
                {
                    try { _pins[i].Handle.Apply(); }
                    catch { _pins.RemoveAt(i); } // target gone (destroyed) → drop the pin
                }
            }
        }
    }
}
