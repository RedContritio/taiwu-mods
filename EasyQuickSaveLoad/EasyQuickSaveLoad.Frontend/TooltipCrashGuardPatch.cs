using System;
using HarmonyLib;
using UnityEngine;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// Guards the game's own <c>TooltipManager.GetHitObject</c> against a latent engine race that a
    /// world reload can trip.
    ///
    /// Root cause (in the game, not this mod): <c>GetHitObject</c> reads <c>RaycastAllManager</c>'s
    /// per-frame hit cache and, in a <c>while(true)</c> walk-up loop, calls
    /// <c>result.GetComponent&lt;TooltipInvoker&gt;()</c> and <c>result.transform.parent</c> with NO
    /// destroyed-object check. During a world reload (the native LoadArchive path AND our quick/slot
    /// load, which mirrors it) the UI/world teardown <c>Object.Destroy</c>s a GameObject that is under
    /// the mouse and sitting in that frame's raycast cache; once Unity frees its native side, the next
    /// tick dereferences the dead object and throws a NullReferenceException. Because the tooltip driver
    /// is an unguarded <c>while(true){ Tick(); yield return null; }</c> coroutine on the persistent
    /// YieldHelper singleton (never stopped across a reload), that single throw permanently kills ALL
    /// tooltips for the rest of the session.
    ///
    /// The fix maps any exception escaping <c>GetHitObject</c> to its intended "nothing under the
    /// cursor" result (<c>null</c>), so the coroutine survives. It is self-healing: the next frame
    /// re-raycasts a fresh, valid scene. It also protects the native load path, which has the identical
    /// latent race. Patching by method-name string because <c>GetHitObject</c> is private.
    /// </summary>
    [HarmonyPatch(typeof(TooltipManager), "GetHitObject")]
    internal static class TooltipGetHitObjectGuardPatch
    {
        // A Harmony Finalizer runs after the original (and after any thrown patch). Returning null
        // suppresses the exception; setting __result gives GetHitObject a safe return value.
        private static Exception Finalizer(Exception __exception, ref GameObject __result)
        {
            if (__exception != null)
            {
                __result = null; // treat a destroyed / failed hit-test as "no valid hit this frame"
                return null;     // swallow so UpdateMouseOverObj's while(true) coroutine keeps running
            }
            return null;
        }
    }
}
