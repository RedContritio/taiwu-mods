using HarmonyLib;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// Root-level self-heal for an entire class of reload-related crashes: a "zombie" UIElement being
    /// shown. Kills the bug at the one choke point (every Show) instead of patching each trigger site.
    ///
    /// Root cause (in the game, not this mod): a world-reload teardown (<c>UIManager.DestroyAll</c> →
    /// <c>DestroyUiBase</c>) frees a UIElement's <c>UiBase</c> but does NOT reset its state machine, so
    /// it is left with <c>UiBase == null</c> while the state is non-<c>Sleep</c> (Hiding / AnimateOut /
    /// Ready …). The next <c>Show()</c> then takes the "already prepared" branch
    /// (Reset → DataPrepare → <c>InitElement</c>) and dereferences the null UiBase → NullReferenceException.
    /// This bites ANY later show of that element: our own confirm dialog, and — critically — the game's
    /// own <c>GameApp.GameQuitConfirm()</c> quit dialog (crash on quitting from the save page).
    ///
    /// Our quick/slot load mirrors native <c>GameApp.LoadArchive</c> but runs it from in-world with a
    /// confirm dialog live, so it surfaces this latent fragility far more than normal play does.
    ///
    /// The guard fires ONLY on the genuine zombie state (<c>!Exist &amp;&amp; not Sleep</c>). A normal
    /// first show is <c>Sleep</c> + null UiBase and is left untouched, so healthy flows are unaffected.
    /// <c>Destroy()</c> safely routes any state back to <c>Sleep</c>; the original <c>Show()</c> then
    /// re-runs ResourcePrepare and rebuilds UiBase correctly. This supersedes (and makes belt-and-suspenders)
    /// the targeted resets in NativeDialog.ShowNow / EqslCore.OnEnterWorldLoadFinish.
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "Show")]
    internal static class UIElementShowZombieHealPatch
    {
        [HarmonyPrefix]
        private static void Prefix(UIElement __instance)
        {
            try
            {
                if (__instance != null && !__instance.Exist && !__instance.IsInState(EUiElementState.Sleep))
                {
                    __instance.Destroy(); // zombie → Sleep, so Show() rebuilds UiBase instead of NREing
                }
            }
            catch
            {
                // never let the guard itself break a Show
            }
        }
    }
}
