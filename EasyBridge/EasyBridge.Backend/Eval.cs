using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameData.Common;
using GameData.Domains;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 动态执行任意 C# 代码（Roslyn CSharpScript）：在后端主线程上、用传入的 DataContext ctx 运行，
    /// 可直接调用 DomainManager.* / EventHelper.* / GameOps.*（无需为每个新操作重编译重启）。
    /// 脚本用 `return ...;` 返回值，结果序列化为 JSON。
    /// </summary>
    public static class Eval
    {
        /// <summary>脚本可直接访问的全局：ctx（主线程写上下文）、taiwuId。</summary>
        public class Globals
        {
            public DataContext ctx;
            public int taiwuId;
        }

        private static readonly object _gate = new object();
        private static readonly Dictionary<string, Script<object>> _cache = new Dictionary<string, Script<object>>();
        private static ScriptOptions _options;

        /// <summary>插件 Initialize() 时调用：趁后端加载器的解析目录仍指向本 mod，强制加载全部 Roslyn 程序集。</summary>
        public static void Warmup()
        {
            try { GetScript("0"); } catch { }
        }

        public static Dictionary<string, object> Run(DataContext ctx, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "empty code" };
            try
            {
                Script<object> script = GetScript(code);
                var globals = new Globals { ctx = ctx, taiwuId = DomainManager.Taiwu.GetTaiwuCharId() };
                ScriptState<object> state = script.RunAsync(globals).GetAwaiter().GetResult();
                return new Dictionary<string, object> { ["ok"] = true, ["result"] = Describe(state.ReturnValue) };
            }
            catch (CompilationErrorException cex)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "compile: " + string.Join(" | ", cex.Diagnostics.Select(d => d.ToString())),
                };
            }
            catch (Exception ex)
            {
                Exception e = ex.InnerException ?? ex;
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = e.GetType().Name + ": " + e.Message };
            }
        }

        private static Script<object> GetScript(string code)
        {
            lock (_gate)
            {
                if (_cache.TryGetValue(code, out Script<object> cached))
                    return cached;
                if (_options == null)
                    _options = BuildOptions();
                Script<object> script = CSharpScript.Create<object>(code, _options, typeof(Globals));
                script.Compile();
                if (_cache.Count < 256)
                    _cache[code] = script;
                return script;
            }
        }

        private static ScriptOptions BuildOptions()
        {
            var refs = new List<MetadataReference>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.IsDynamic) continue;
                string loc = null;
                try { loc = a.Location; } catch { }
                if (string.IsNullOrEmpty(loc)) continue;
                try { refs.Add(MetadataReference.CreateFromFile(loc)); } catch { }
            }

            var imports = new List<string>
            {
                "System", "System.Linq", "System.Collections.Generic", "System.Text",
                "GameData.Common", "GameData.Domains", "GameData.Domains.Character",
                "GameData.Domains.Character.Relation", "GameData.Domains.Combat",
                "GameData.Domains.Map", "GameData.Domains.Organization",
                "GameData.Domains.TaiwuEvent.EventHelper",
            };

            // 本 mod 程序集是字节加载的（Location 空），编译期引用不到，但脚本需要它的类型形状
            // （Globals、GameOps）。用磁盘 DLL 的【字节】做编译期 metadata（CreateFromImage，不带 filePath，
            // 这样运行期不会从路径再加载一份新副本）；运行期由 Resolving 钩子返回【已加载的那一份】，
            // 保证 Globals/GameOps 类型身份一致。
            string selfDll = FindSelfDll();
            if (selfDll != null)
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromImage(File.ReadAllBytes(selfDll)));
                    imports.Add("EasyBridge.Backend");
                }
                catch { }
            }

            return ScriptOptions.Default.WithReferences(refs).WithImports(imports);
        }

        private static string FindSelfDll()
        {
            try
            {
                string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Mod", "EasyBridge", "Plugins", "EasyBridge.Backend.dll"));
                if (File.Exists(p)) return p;
            }
            catch { }
            return null;
        }

        private static object Describe(object val)
        {
            if (val == null) return null;
            if (val is string || val is bool || val is int || val is long || val is short || val is sbyte
                || val is byte || val is ushort || val is uint || val is ulong || val is float || val is double)
                return val;
            if (val is IDictionary<string, object>) return val;
            if (val is IEnumerable enumerable)
            {
                var list = new List<object>();
                foreach (var x in enumerable)
                {
                    list.Add(Describe(x));
                    if (list.Count >= 200) break;
                }
                return list;
            }
            return val.ToString();
        }
    }
}
