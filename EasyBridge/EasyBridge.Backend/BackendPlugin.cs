using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using GameData.Utilities;
using HarmonyLib;
using TaiwuMod.Common;
using TaiwuModdingLib.Core.Plugin;

namespace EasyBridge.Backend
{
    /// <summary>
    /// EasyBridge 后端插件：在后端进程内开一个命名管道（easybridge-state），
    /// 让自动化测试 agent 读取/构造游戏内角色与关系状态，用于验证 ForceEncounter 等 mod 的各分支。
    /// 作为调试桥分享给他人，方便其用 agent 调试自己的 mod。
    /// </summary>
    [PluginConfig("EasyBridge", "RedContritio", "0.2.0.0")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        public const string ModId = "EasyBridge";
        public const string Version = "0.2.0.0";

        internal static string RuntimeModId;
        private PipeServer _server;
        private Harmony _harmony;

        private static bool _resolverHooked;
        private static bool _roslynLoaded;

        public override void Initialize()
        {
            RuntimeModId = ModIdStr;

            try
            {
                _harmony = new Harmony(ModId + ".tick");
                _harmony.PatchAll(typeof(BackendPlugin).Assembly);
                AdaptableLog.Info("[EasyBridge] Harmony tick patched");
            }
            catch (System.Exception ex)
            {
                AdaptableLog.Info("[EasyBridge] Harmony patch failed: " + ex.Message);
            }

            StartFromSettings();
        }

        internal static void EnsureAssemblyResolver()
        {
            if (_resolverHooked) return;
            _resolverHooked = true;
            // 游戏插件加载器把插件及其直接引用用 Assembly.Load(byte[]) 加载（匿名、未按名注册到默认 ALC），
            // 且其 ResolvePluginDependency 是死代码。Roslyn 的【传递】依赖（Microsoft.CodeAnalysis.CSharp.dll）
            // 解析时，byte 加载/LoadFrom 都会因 ALC 上下文与强名称绑定不统一而报 0x80131044。
            // 正确做法：挂默认 ALC 的 Resolving，用 LoadFromAssemblyPath 把缺失依赖按【路径】加载进默认 ALC
            //（按名注册、保留强名称），从本 mod 的 Plugins 目录取。
            AssemblyLoadContext.Default.Resolving += ResolveFromPluginDir;
        }

        internal static void EnsureRoslynAssembliesLoaded()
        {
            if (_roslynLoaded) return;
            EnsureAssemblyResolver();
            string dir = PluginDir();
            string[] names =
            {
                "Microsoft.CodeAnalysis.dll",
                "Microsoft.CodeAnalysis.Scripting.dll",
                "Microsoft.CodeAnalysis.CSharp.dll",
                "Microsoft.CodeAnalysis.CSharp.Scripting.dll",
            };
            foreach (var name in names)
            {
                try
                {
                    string path = Path.Combine(dir, name);
                    if (File.Exists(path))
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }
                catch { }
            }
            _roslynLoaded = true;
        }

        private static Assembly ResolveFromPluginDir(AssemblyLoadContext context, AssemblyName name)
        {
            // 脚本程序集会按名引用本 mod 程序集（EasyBridge.Backend，字节加载、Location 空）。返回【已加载的那一份】，
            // 而不是从路径再加载一份新副本，否则跨 ALC 的 Globals/GameOps 类型身份不一致 → InvalidCastException。
            if (name.Name == typeof(BackendPlugin).Assembly.GetName().Name)
                return typeof(BackendPlugin).Assembly;
            try
            {
                string dir = PluginDir();
                string path = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            catch { }
            return null;
        }

        private static string PluginDir()
            => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Mod", "EasyBridge", "Plugins"));

        public override void OnModSettingUpdate()
        {
            Router.ReloadRuntimeOptions();
        }

        public override void Dispose()
        {
            StopServer();
            MainThreadPump.Shutdown();
            try { _harmony?.UnpatchSelf(); } catch { }
            _harmony = null;
        }

        private void StartFromSettings()
        {
            if (_server != null) return;
            Router.ReloadRuntimeOptions();
            _server = new PipeServer();
            _server.Start();
            AdaptableLog.Info("[EasyBridge] listening on \\\\.\\pipe\\" + PipeServer.PipeName);
        }

        private void StopServer()
        {
            _server?.Stop();
            _server = null;
        }
    }
}
