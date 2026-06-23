using System;
using System.Collections.Generic;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 把 HTTP 请求映射到检视/动作逻辑。涉及 Unity 对象的部分通过
    /// MainThreadDispatcher 在主线程执行。返回 (状态码, 可序列化对象)。
    /// </summary>
    internal static class Router
    {
        public const string ModName = "EasyBridge";
        public const string Version = "0.0.1";
        public static int DefaultMax = 60;
        public static int MainThreadTimeoutMs = 5000;

        public static (int status, object body) Handle(string method, string path,
            Dictionary<string, string> query, string requestBody)
        {
            try
            {
                path = (path ?? "/").TrimEnd('/');
                if (path.Length == 0) path = "/";

                if (method == "GET" && (path == "/" || path == "/ping"))
                    return (200, new Dictionary<string, object>
                    {
                        ["ok"] = true,
                        ["mod"] = ModName,
                        ["version"] = Version,
                        ["dispatcher"] = MainThreadDispatcher.Ready,
                    });

                if (method == "GET" && path == "/ui")
                    return Main(() => UiInspector.Snapshot());

                if (method == "GET" && path.StartsWith("/ui/"))
                {
                    var name = Uri.UnescapeDataString(path.Substring("/ui/".Length));
                    var opts = new UiInspector.Options
                    {
                        Full = GetStr(query, "detail") == "full",
                        Max = GetInt(query, "max", DefaultMax),
                    };
                    if (opts.Max <= 0) opts.Max = DefaultMax;
                    return Main(() => UiInspector.Element(name, opts));
                }

                if (method == "GET" && path == "/find")
                {
                    var q = GetStr(query, "q") ?? GetStr(query, "text") ?? "";
                    var limit = GetInt(query, "limit", 50);
                    return Main(() => UiInspector.Find(q, limit));
                }

                if (method == "GET" && path == "/elements")
                {
                    bool onlyActive = GetStr(query, "onlyActive") == "true";
                    return Main(() => Elements(onlyActive));
                }

                if (method == "POST" && path == "/action")
                {
                    var parsed = Json.Parse(requestBody);
                    var element = Json.GetString(parsed, "element");
                    var id = Json.GetString(parsed, "id") ?? "";
                    var action = Json.GetString(parsed, "action");
                    object value = GetRawValue(parsed);
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => UiActuator.Perform(element, id, action, value), MainThreadTimeoutMs);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
                }

                if (method == "GET" && path == "/wait")
                {
                    var target = GetStr(query, "element") ?? "";
                    var timeoutSec = GetInt(query, "timeout", 60);
                    if (string.IsNullOrEmpty(target))
                        return (400, new Dictionary<string, object> { ["error"] = "missing 'element' param" });
                    return WaitForElement(target, timeoutSec);
                }

                if (method == "POST" && path == "/quit")
                {
                    MainThreadDispatcher.Run(() => { UnityEngine.Application.Quit(); return null; }, MainThreadTimeoutMs);
                    // Application.Quit is async; schedule a hard kill fallback
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        System.Threading.Thread.Sleep(3000);
                        try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch { }
                    });
                    return (200, new Dictionary<string, object> { ["ok"] = true, ["action"] = "quit" });
                }

                return (404, new Dictionary<string, object> { ["error"] = "not found", ["path"] = path });
            }
            catch (TimeoutException)
            {
                return (504, new Dictionary<string, object> { ["error"] = "main-thread timeout" });
            }
            catch (Exception ex)
            {
                return (500, new Dictionary<string, object> { ["error"] = ex.GetType().Name + ": " + ex.Message });
            }
        }

        private static (int, object) Main(Func<object> fn)
        {
            var r = MainThreadDispatcher.Run(fn, MainThreadTimeoutMs);
            return (200, r);
        }

        private static (int, object) WaitForElement(string target, int timeoutSec)
        {
            timeoutSec = Math.Max(1, Math.Min(timeoutSec, 120));
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    bool showing = (bool)MainThreadDispatcher.Run(() =>
                    {
                        UiElementCatalog.EnsureBuilt();
                        var elem = UiElementCatalog.ElementByName(target);
                        return elem != null && UiElementCatalog.IsShowing(elem);
                    }, 10000);
                    if (showing)
                        return (200, new Dictionary<string, object> { ["ok"] = true, ["element"] = target, ["showing"] = true });
                }
                catch { }
                System.Threading.Thread.Sleep(1000);
            }
            return (408, new Dictionary<string, object> { ["ok"] = false, ["element"] = target, ["error"] = "timeout" });
        }

        private static object Elements(bool onlyActive)
        {
            UiElementCatalog.EnsureBuilt();
            var list = new List<object>();
            foreach (var kv in UiElementCatalog.AllElements())
            {
                bool showing = UiElementCatalog.IsShowing(kv.Value);
                bool exist = UiElementCatalog.Exist(kv.Value);
                if (onlyActive && !showing && !exist) continue;
                list.Add(new Dictionary<string, object>
                {
                    ["name"] = kv.Key,
                    ["showing"] = showing,
                    ["exist"] = exist,
                });
            }
            return new Dictionary<string, object> { ["count"] = list.Count, ["elements"] = list };
        }

        private static object GetRawValue(object parsed)
        {
            if (parsed is IDictionary<string, object> m && m.TryGetValue("value", out var v))
                return v;
            return null;
        }

        private static string GetStr(Dictionary<string, string> q, string key)
            => q != null && q.TryGetValue(key, out var v) ? v : null;

        private static int GetInt(Dictionary<string, string> q, string key, int fallback)
            => int.TryParse(GetStr(q, key), out var v) ? v : fallback;
    }
}
