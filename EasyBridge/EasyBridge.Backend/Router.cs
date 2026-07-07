using System;
using System.Collections.Generic;
using GameData.Common;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 把管道请求映射到 GameOps/Presets。涉及域读写的部分通过 MainThreadPump 在后端主线程执行。
    /// </summary>
    internal static class Router
    {
        public const string ModName = "EasyBridge";
        public const string Version = BackendPlugin.Version;
        public static int PumpTimeoutMs = 8000;
        public static bool EnableEval;
        public static bool EnableMonitorForward;

        public static void ReloadRuntimeOptions()
        {
            // No user Settings.Lua fields are defined yet. Keep runtime switches in memory so
            // OnModSettingUpdate never tears down a live pipe just to re-read an empty settings table.
        }

        public static (int status, object body) Handle(string method, string path,
            Dictionary<string, string> query, string requestBody)
        {
            path = (path ?? "/").TrimEnd('/');
            if (path.Length == 0) path = "/";

            if (path == "/" || path == "/ping")
                return Ok(new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["mod"] = ModName,
                    ["version"] = Version,
                    ["tickAlive"] = MainThreadPump.TickAlive,
                    ["enableEval"] = EnableEval,
                    ["enableMonitor"] = EnableMonitorForward,
                });

            object body = Json.Parse(requestBody);

            if (path == "/taiwu")
                return Pump(_ => GameOps.Taiwu());

            if (path == "/whereami")
                return Pump(_ => GameOps.WhereAmI());

            if (path == "/combat")
                return Pump(ctx => GameOps.Combat(ctx));

            if (path == "/combat/control")
            {
                bool hasPause = Json.TryGetBool(body, "pause", out bool pause);
                bool hasAutoCombat = Json.TryGetBool(body, "autoCombat", out bool autoCombat);
                bool hasAutoMove = Json.TryGetBool(body, "autoMove", out bool autoMove);
                bool hasTimeScale = Json.TryGetDouble(body, "timeScale", out double timeScale);
                return Pump(ctx => GameOps.CombatControl(ctx,
                    hasPause ? (bool?)pause : null,
                    hasAutoCombat ? (bool?)autoCombat : null,
                    hasAutoMove ? (bool?)autoMove : null,
                    hasTimeScale ? (float?)timeScale : null));
            }

            if (path == "/combat/watch")
            {
                if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    return Pump(ctx => CombatStepper.Status(ctx, QueryBool(query, "includeSnapshot", true)));
                return Pump(ctx => CombatStepper.Arm(ctx, body));
            }

            if (path == "/combat/resume")
                return Pump(ctx => CombatStepper.Resume(ctx, body));

            if (path == "/combat/watch/cancel")
                return Pump(ctx => CombatStepper.Cancel(ctx, body));

            if (path == "/sects")
            {
                bool allowLarge = IsLarge(body);
                int count = Clamp(Json.GetInt(body, "count", 6), 1, allowLarge ? 50 : 20);
                return Pump(ctx => GameOps.FindSects(ctx, count));
            }

            if (path == "/settlements")
            {
                bool allowLarge = IsLarge(body);
                bool civilianOnly = Json.GetBool(body, "civilianOnly", true);
                int max = Clamp(Json.GetInt(body, "max", 40), 1, allowLarge ? 200 : 80);
                return Pump(ctx => GameOps.Settlements(ctx, civilianOnly, max));
            }

            if (path == "/move/taiwu")
            {
                int areaId = Json.GetInt(body, "areaId", -1);
                int blockId = Json.GetInt(body, "blockId", -1);
                return Pump(ctx => GameOps.MoveTaiwu(ctx, areaId, blockId));
            }

            if (path.StartsWith("/char/"))
            {
                if (!int.TryParse(path.Substring("/char/".Length), out int cid))
                    return Bad("bad char id in path");
                return Pump(_ => GameOps.Snapshot(cid));
            }

            if (path == "/snapshot")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(_ => GameOps.Snapshot(id));
            }

            if (path == "/spawn")
            {
                sbyte gender = (sbyte)Json.GetInt(body, "gender", 0);
                short age = (short)Json.GetInt(body, "age", 25);
                short settlementId = (short)Json.GetInt(body, "settlementId", -1);
                sbyte grade = (sbyte)Json.GetInt(body, "grade", 4);
                short baseAttraction = (short)Json.GetInt(body, "baseAttraction", 500);
                bool villager = Json.GetBool(body, "villager", false);
                return Pump(ctx => GameOps.Spawn(ctx, gender, age, settlementId, grade, baseAttraction, villager));
            }

            if (path == "/spawn/closefriend")
            {
                sbyte gender = (sbyte)Json.GetInt(body, "gender", 0);
                return Pump(ctx => GameOps.SpawnCloseFriend(ctx, gender));
            }

            if (path == "/relation")
            {
                int a = Json.GetInt(body, "a", -1);
                int b = Json.GetInt(body, "b", -1);
                ushort type = ParseRelation(body);
                bool both = Json.GetBool(body, "both", false);
                bool clear = Json.GetBool(body, "clear", false);
                return Pump(ctx => GameOps.SetRelation(ctx, a, b, type, both, clear));
            }

            if (path == "/favor")
            {
                int from = Json.GetInt(body, "from", -1);
                int to = Json.GetInt(body, "to", -1);
                int type = Json.GetInt(body, "type", 0);
                int value = Json.TryGetInt(body, "value", out int v) ? v : int.MinValue;
                return Pump(ctx => GameOps.SetFavor(ctx, from, to, type, value));
            }

            if (path == "/villager")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(ctx => GameOps.MakeVillager(ctx, id));
            }

            if (path == "/nonvillager")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(ctx => GameOps.MakeNonVillager(ctx, id));
            }

            if (path == "/prisoner")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(ctx => GameOps.MakePrisoner(ctx, id));
            }

            if (path == "/injure")
            {
                int id = Json.GetInt(body, "id", -1);
                int level = Json.GetInt(body, "level", 6);
                return Pump(ctx => GameOps.Injure(ctx, id, level));
            }

            if (path == "/heal")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(ctx => GameOps.Heal(ctx, id));
            }

            if (path == "/favor/exact")
            {
                int from = Json.GetInt(body, "from", -1);
                int to = Json.GetInt(body, "to", -1);
                int value = Json.GetInt(body, "value", 0);
                return Pump(ctx => GameOps.SetFavorExact(ctx, from, to, value));
            }

            if (path == "/block/chars")
            {
                bool allowLarge = IsLarge(body);
                int max = Clamp(Json.GetInt(body, "max", 40), 1, allowLarge ? 200 : 80);
                return Pump(_ => GameOps.BlockChars(max));
            }

            if (path == "/giverope")
            {
                int template = Json.GetInt(body, "template", 90);
                return Pump(ctx => GameOps.GiveRope(ctx, template));
            }

            if (path == "/throwrope")
                return Pump(ctx => GameOps.ThrowRope(ctx));

            if (path == "/cripple")
            {
                int id = Json.GetInt(body, "id", -1);
                return Pump(ctx => GameOps.Cripple(ctx, id));
            }

            if (path == "/preset")
            {
                string name = Json.GetString(body, "name");
                sbyte gender = (sbyte)Json.GetInt(body, "gender", 0);
                short age = (short)Json.GetInt(body, "age", 25);
                return Pump(ctx => Presets.Build(ctx, name, gender, age));
            }

            if (path == "/config")
            {
                if (body is IDictionary<string, object> m)
                {
                    if (m.TryGetValue("enableEval", out var ev) && ev != null)
                        EnableEval = ToBool(ev);
                    if (m.TryGetValue("enableMonitor", out var mv) && mv != null)
                        EnableMonitorForward = ToBool(mv);
                    if (m.TryGetValue("pumpTimeoutMs", out var tv) && tv != null && int.TryParse(tv.ToString(), out int t))
                        PumpTimeoutMs = Math.Max(1000, Math.Min(t, 30000));
                }
                return Ok(new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["enableEval"] = EnableEval,
                    ["enableMonitor"] = EnableMonitorForward,
                    ["pumpTimeoutMs"] = PumpTimeoutMs,
                });
            }

            // 动态执行任意 C# 代码（Roslyn）。默认关闭；启用后才延迟加载 Roslyn 依赖。
            if (path == "/eval")
            {
                if (!EnableEval)
                    return (403, new Dictionary<string, object>
                    {
                        ["ok"] = false,
                        ["error"] = "eval disabled; POST /config {\"enableEval\":true} first",
                    });
                string code = Json.GetString(body, "code");
                int maxItems = Clamp(Json.GetInt(body, "maxItems", 40), 1, IsLarge(body) ? 200 : 60);
                BackendPlugin.EnsureRoslynAssembliesLoaded();
                return Pump(ctx => Eval.Run(ctx, code, maxItems), 30000);
            }

            return (404, new Dictionary<string, object> { ["ok"] = false, ["error"] = "not found", ["path"] = path });
        }

        private static ushort ParseRelation(object body)
        {
            string s = Json.GetString(body, "type");
            if (!string.IsNullOrEmpty(s))
            {
                switch (s.Trim().ToLowerInvariant())
                {
                    case "spouse": return GameOps.RelSpouse;
                    case "adore":
                    case "adored": return GameOps.RelAdored;
                }
                if (ushort.TryParse(s, out var parsed)) return parsed;
            }
            return (ushort)Json.GetInt(body, "type", 0);
        }

        private static (int, object) Pump(Func<DataContext, object> fn) => Pump(fn, PumpTimeoutMs);

        private static (int, object) Pump(Func<DataContext, object> fn, int timeoutMs)
        {
            var r = MainThreadPump.Run(fn, timeoutMs);
            bool ok = r is IDictionary<string, object> m && m.TryGetValue("ok", out var v) && v is bool b && b;
            return (ok ? 200 : 400, r);
        }

        private static (int, object) Ok(object body) => (200, body);
        private static (int, object) Bad(string msg) => (400, new Dictionary<string, object> { ["ok"] = false, ["error"] = msg });

        private static bool ToBool(object value)
        {
            if (value is bool b) return b;
            if (value is double d) return d != 0;
            return bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }

        private static int Clamp(int value, int min, int max)
            => Math.Max(min, Math.Min(max, value));

        private static bool IsLarge(object body)
            => Json.GetBool(body, "allowLarge", false) || Json.GetBool(body, "large", false);

        private static bool QueryBool(Dictionary<string, string> query, string key, bool fallback)
        {
            if (query == null || !query.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                return fallback;
            if (bool.TryParse(value, out bool parsed))
                return parsed;
            return value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
