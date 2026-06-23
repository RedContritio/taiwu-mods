using GameData.Common;
using GameData.Domains.Global;
using HarmonyLib;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 通过 Harmony 给后端主循环装一个每帧钩子。GlobalDomain.OnUpdate 由后端主循环
    /// 每帧用主线程 DataContext 调用（见 GameData 主循环），是排空 MainThreadPump 的稳妥时机。
    /// </summary>
    [HarmonyPatch(typeof(GlobalDomain), "OnUpdate")]
    internal static class GlobalDomainOnUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(DataContext context)
        {
            MainThreadPump.Drain(context);
        }
    }
}
