using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using GameData.Utilities;
using HarmonyLib;
using TaiwuMod.Common;
using TaiwuModdingLib.Core.Plugin;

namespace Bridge.Backend
{
    /// <summary>
    /// Bridge 后端插件：在后端进程内开一个命名管道（taiwu-testbridge），
    /// 让自动化测试 agent 读取/构造游戏内角色与关系状态，用于验证 ForceEncounter 等 mod 的各分支。
    /// 仅供开发/测试使用，不随正式 mod 发布。
    /// </summary>
    [PluginConfig("Bridge", "RedContritio", "0.0.1")]
    public class BackendPlugin : TaiwuRemakePlugin
    {
        public const string ModId = "Bridge";
        public const string Version = "0.0.1";

        internal static string RuntimeModId;
        private PipeServer _server;
        private Harmony _harmony;

        private static bool _resolverHooked;

        public override void Initialize()
        {
            RuntimeModId = ModIdStr;

            // 后端加载器只预加载插件的【直接】引用程序集（一层），且其 ResolvePluginDependency 是死代码
            // （从未订阅 AssemblyResolve）。所以 Roslyn 的【传递】依赖（如 Microsoft.CodeAnalysis.CSharp.dll）
            // 不会被解析。这里自己挂一个 AssemblyResolve，从本 mod 的 Plugins 目录按名加载缺失程序集；
            // 然后趁此刻预热 Roslyn，把全部 Roslyn 程序集加载进来，/eval 冷启动不再卡 pump 超时。
            HookAssemblyResolver();
            Eval.Warmup();

            try
            {
                _harmony = new Harmony(ModId + ".tick");
                _harmony.PatchAll(typeof(BackendPlugin).Assembly);
                AdaptableLog.Info("[Bridge] Harmony tick patched");
            }
            catch (System.Exception ex)
            {
                AdaptableLog.Info("[Bridge] Harmony patch failed: " + ex.Message);
            }

            StartFromSettings();
        }

        private static void HookAssemblyResolver()
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

        private static Assembly ResolveFromPluginDir(AssemblyLoadContext context, AssemblyName name)
        {
            // 脚本程序集会按名引用本 mod 程序集（Bridge.Backend，字节加载、Location 空）。返回【已加载的那一份】，
            // 而不是从路径再加载一份新副本，否则跨 ALC 的 Globals/GameOps 类型身份不一致 → InvalidCastException。
            if (name.Name == typeof(BackendPlugin).Assembly.GetName().Name)
                return typeof(BackendPlugin).Assembly;
            try
            {
                string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Mod", "Bridge", "Plugins"));
                string path = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            catch { }
            return null;
        }

        public override void OnModSettingUpdate()
        {
            StopServer();
            StartFromSettings();
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
            if (!TaiwuModSettings.GetBool(RuntimeModId, "Enabled", true))
            {
                AdaptableLog.Info("[Bridge] disabled by setting");
                return;
            }

            _server = new PipeServer();
            _server.Start();
            AdaptableLog.Info("[Bridge] listening on \\\\.\\pipe\\" + PipeServer.PipeName);
        }

        private void StopServer()
        {
            _server?.Stop();
            _server = null;
        }
    }
}
