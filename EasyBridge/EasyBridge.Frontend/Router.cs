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
        public static int MaxReflectDepth = 2;
        public static int MaxReflectMembers = 80;
        public static bool EnableReflectInvoke = false;
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

                if (method == "GET" && path == "/log")
                    return (200, RequestLog.Snapshot(GetInt(query, "since", 0), GetInt(query, "max", 200)));

                // Backend pipe forwards its handled requests here so one overlay shows both pipes.
                if (method == "POST" && path == "/monitor/push")
                {
                    var p = Json.Parse(requestBody);
                    RequestLog.Add(
                        Json.GetString(p, "source") ?? "backend",
                        Json.GetString(p, "method") ?? "",
                        Json.GetString(p, "path") ?? "",
                        Json.GetString(p, "query") ?? "",
                        Json.GetString(p, "body") ?? "",
                        Json.GetString(p, "note") ?? "",
                        GetBodyInt(p, "status", 200),
                        Json.GetString(p, "result") ?? "",
                        GetBodyInt(p, "ms", 0));
                    return (200, new Dictionary<string, object> { ["ok"] = true });
                }

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

                if (method == "GET" && path == "/inspect")
                {
                    var element = GetStr(query, "element") ?? "";
                    var id = GetStr(query, "id") ?? "";
                    var maxMembers = Clamp(GetInt(query, "max", MaxReflectMembers), 1, MaxReflectMembers);
                    return MainStatus(() => ReflectionInspector.Inspect(element, id, maxMembers));
                }

                if (method == "GET" && path == "/pointer")
                {
                    float? x = TryGetFloat(query, "x");
                    float? y = TryGetFloat(query, "y");
                    var origin = GetStr(query, "origin") ?? "bottom-left";
                    var max = GetInt(query, "max", 20);
                    return Main(() => PointerInspector.Snapshot(x, y, origin, max));
                }

                if (method == "GET" && path == "/reflect")
                {
                    var element = GetStr(query, "element") ?? "";
                    var id = GetStr(query, "id") ?? "";
                    var component = GetStr(query, "component") ?? "";
                    var member = GetStr(query, "member") ?? "";
                    var depth = Clamp(GetInt(query, "depth", 1), 0, MaxReflectDepth);
                    var maxMembers = Clamp(GetInt(query, "max", MaxReflectMembers), 1, MaxReflectMembers);
                    return MainStatus(() => ReflectionInspector.Reflect(element, id, component, member, depth, maxMembers));
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

                if (method == "POST" && path == "/wait/actions")
                {
                    var parsed = Json.Parse(requestBody);
                    var result = WaitForActions(parsed, query);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
                }

                if (method == "POST" && path == "/reflect/invoke")
                {
                    if (!EnableReflectInvoke)
                    {
                        return (403, new Dictionary<string, object>
                        {
                            ["ok"] = false,
                            ["error"] = "reflect invoke disabled by setting",
                        });
                    }
                    var depth = Clamp(GetInt(query, "depth", 1), 0, MaxReflectDepth);
                    var maxMembers = Clamp(GetInt(query, "max", MaxReflectMembers), 1, MaxReflectMembers);
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => ReflectionInspector.Invoke(requestBody, depth, maxMembers), MainThreadTimeoutMs);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
                }

                if (method == "POST" && path == "/reflect/set")
                {
                    if (!EnableReflectInvoke)
                        return (403, new Dictionary<string, object>
                        {
                            ["ok"] = false,
                            ["error"] = "set disabled; POST /config {\"enableInvoke\":true} first",
                        });
                    var depth = Clamp(GetInt(query, "depth", 1), 0, MaxReflectDepth);
                    var maxMembers = Clamp(GetInt(query, "max", MaxReflectMembers), 1, MaxReflectMembers);
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => ReflectionInspector.Set(requestBody, depth, maxMembers), MainThreadTimeoutMs);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
                }

                if (method == "POST" && path == "/pin")
                {
                    if (!EnableReflectInvoke)
                        return (403, new Dictionary<string, object>
                        {
                            ["ok"] = false,
                            ["error"] = "pin disabled; POST /config {\"enableInvoke\":true} first",
                        });
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => PinController.Add(requestBody), MainThreadTimeoutMs);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
                }

                if (method == "POST" && path == "/unpin")
                    return (200, MainThreadDispatcher.Run(() => PinController.Clear(), MainThreadTimeoutMs));

                if (method == "GET" && path == "/pins")
                    return (200, MainThreadDispatcher.Run(() => PinController.List(), MainThreadTimeoutMs));

                if (method == "POST" && path == "/time")
                {
                    var parsed = Json.Parse(requestBody);
                    var op = (Json.GetString(parsed, "op") ?? "").ToLowerInvariant();
                    if (op == "pause")
                        return (200, MainThreadDispatcher.Run(() => TimeController.Pause(), MainThreadTimeoutMs));
                    if (op == "resume" || op == "play")
                        return (200, MainThreadDispatcher.Run(() => TimeController.Resume(), MainThreadTimeoutMs));
                    if (op == "step")
                    {
                        float seconds = (float)GetBodyDouble(parsed, "seconds", 1.0);
                        MainThreadDispatcher.Run(() => TimeController.BeginStep(seconds), MainThreadTimeoutMs);
                        // The step ends on game-time accounting; bound the real-time wait generously.
                        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(2000, (int)(seconds * 1000) + 5000));
                        while (TimeController.IsStepping && DateTime.UtcNow < deadline)
                            System.Threading.Thread.Sleep(16);
                        return (200, MainThreadDispatcher.Run(() => TimeController.State(), MainThreadTimeoutMs));
                    }
                    return (400, new Dictionary<string, object> { ["ok"] = false, ["error"] = "op must be pause|resume|step" });
                }

                if (method == "POST" && path == "/config")
                {
                    var parsed = Json.Parse(requestBody);
                    if (parsed is IDictionary<string, object> m && m.TryGetValue("enableInvoke", out var ev) && ev != null)
                        EnableReflectInvoke = ev is bool b ? b : (bool.TryParse(ev.ToString(), out var bb) && bb);
                    return (200, new Dictionary<string, object> { ["ok"] = true, ["enableInvoke"] = EnableReflectInvoke });
                }

                if (method == "POST" && path == "/static")
                {
                    if (!EnableReflectInvoke)
                        return (403, new Dictionary<string, object>
                        {
                            ["ok"] = false,
                            ["error"] = "static invoke disabled; POST /config {\"enableInvoke\":true} first",
                        });
                    var depth = Clamp(GetInt(query, "depth", 1), 0, MaxReflectDepth);
                    var maxMembers = Clamp(GetInt(query, "max", MaxReflectMembers), 1, MaxReflectMembers);
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => ReflectionInspector.InvokeStatic(requestBody, depth, maxMembers), MainThreadTimeoutMs);
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

                if (method == "POST" && path == "/screenshot")
                {
                    var parsed = Json.Parse(requestBody);
                    var shotPath = Json.GetString(parsed, "path");
                    var superSize = GetBodyInt(parsed, "superSize", 1);
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(
                        () => Screenshotter.Capture(shotPath, superSize), MainThreadTimeoutMs);
                    bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
                    return (ok ? 200 : 400, result);
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

        private static (int, object) MainStatus(Func<Dictionary<string, object>> fn)
        {
            var result = (Dictionary<string, object>)MainThreadDispatcher.Run(fn, MainThreadTimeoutMs);
            bool ok = result.TryGetValue("ok", out var okv) && okv is bool b && b;
            return (ok ? 200 : 400, result);
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

        private static Dictionary<string, object> WaitForActions(object parsed, Dictionary<string, string> query)
        {
            var waitElement = Json.GetString(parsed, "waitElement") ?? Json.GetString(parsed, "element") ?? "";
            if (string.IsNullOrEmpty(waitElement))
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "missing waitElement" };

            int timeoutSec = GetInt(query, "timeout", GetBodyInt(parsed, "timeout", 30));
            timeoutSec = Math.Max(1, Math.Min(timeoutSec, 120));
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
            string lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var result = (Dictionary<string, object>)MainThreadDispatcher.Run(() =>
                    {
                        UiElementCatalog.EnsureBuilt();
                        var elem = UiElementCatalog.ElementByName(waitElement);
                        if (elem == null || !UiElementCatalog.IsShowing(elem))
                            return new Dictionary<string, object> { ["ok"] = false, ["error"] = "waiting for element" };

                        var actionResults = new List<object>();
                        foreach (var action in ActionsOf(parsed))
                        {
                            string element = ActionString(action, "element") ?? waitElement;
                            string id = ActionString(action, "id") ?? "";
                            string kind = ActionString(action, "action");
                            object value = ActionValue(action);
                            var actionResult = UiActuator.Perform(element, id, kind, value);
                            actionResults.Add(actionResult);
                            if (!actionResult.TryGetValue("ok", out var okv) || !(okv is bool b) || !b)
                            {
                                return new Dictionary<string, object>
                                {
                                    ["ok"] = false,
                                    ["element"] = waitElement,
                                    ["error"] = "action failed",
                                    ["actions"] = actionResults,
                                };
                            }
                        }

                        return new Dictionary<string, object>
                        {
                            ["ok"] = true,
                            ["element"] = waitElement,
                            ["actions"] = actionResults,
                        };
                    }, MainThreadTimeoutMs);

                    if (result.TryGetValue("ok", out var okv) && okv is bool ok && ok)
                        return result;
                    if (result.TryGetValue("error", out var err) && err != null)
                        lastError = err.ToString();
                }
                catch (Exception ex)
                {
                    lastError = ex.GetType().Name + ": " + ex.Message;
                }
                System.Threading.Thread.Sleep(100);
            }

            return new Dictionary<string, object>
            {
                ["ok"] = false,
                ["element"] = waitElement,
                ["error"] = "timeout",
                ["lastError"] = lastError,
            };
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

        private static List<object> ActionsOf(object parsed)
        {
            if (parsed is IDictionary<string, object> m &&
                m.TryGetValue("actions", out var actionsObj) &&
                actionsObj is System.Collections.IEnumerable seq &&
                !(actionsObj is string))
            {
                var result = new List<object>();
                foreach (var item in seq) result.Add(item);
                if (result.Count > 0) return result;
            }

            return new List<object> { parsed };
        }

        private static string ActionString(object action, string key)
        {
            if (action is IDictionary<string, object> m && m.TryGetValue(key, out var v) && v != null)
                return v as string ?? v.ToString();
            return null;
        }

        private static object ActionValue(object action)
        {
            if (action is IDictionary<string, object> m && m.TryGetValue("value", out var v))
                return v;
            return null;
        }

        private static int GetBodyInt(object parsed, string key, int fallback)
        {
            if (parsed is IDictionary<string, object> m && m.TryGetValue(key, out var v) && v != null)
            {
                if (v is double d) return (int)Math.Round(d);
                if (int.TryParse(v.ToString(), out var parsedInt)) return parsedInt;
            }
            return fallback;
        }

        private static double GetBodyDouble(object parsed, string key, double fallback)
        {
            if (parsed is IDictionary<string, object> m && m.TryGetValue(key, out var v) && v != null)
            {
                if (v is double d) return d;
                if (double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedDouble)) return parsedDouble;
            }
            return fallback;
        }

        private static string GetStr(Dictionary<string, string> q, string key)
            => q != null && q.TryGetValue(key, out var v) ? v : null;

        private static int GetInt(Dictionary<string, string> q, string key, int fallback)
            => int.TryParse(GetStr(q, key), out var v) ? v : fallback;

        private static float? TryGetFloat(Dictionary<string, string> q, string key)
            => float.TryParse(GetStr(q, key), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (float?)null;

        private static int Clamp(int value, int min, int max)
            => Math.Max(min, Math.Min(max, value));
    }
}
