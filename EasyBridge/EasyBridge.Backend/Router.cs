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
                    return Pump(ctx => CombatStepper.Status(ctx));
                return Pump(ctx => CombatStepper.Arm(ctx, body));
            }

            if (path == "/combat/resume")
                return Pump(ctx => CombatStepper.Resume(ctx, body));

            if (path == "/combat/watch/cancel")
                return Pump(ctx => CombatStepper.Cancel(ctx, body));

            if (path == "/sects")
            {
                int count = Json.GetInt(body, "count", 6);
                return Pump(ctx => GameOps.FindSects(ctx, count));
            }

            if (path == "/settlements")
            {
                bool civilianOnly = Json.GetBool(body, "civilianOnly", true);
                int max = Json.GetInt(body, "max", 40);
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
                return Pump(_ => GameOps.BlockChars());

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

            // 动态执行任意 C# 代码（Roslyn）。脚本可直接用 ctx / DomainManager.* / EventHelper.* / GameOps.*，
            // 用 `return ...;` 返回值。给更长超时：首次编译可能略久。
            if (path == "/eval")
            {
                string code = Json.GetString(body, "code");
                return Pump(ctx => Eval.Run(ctx, code), 30000);
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
    }
}
