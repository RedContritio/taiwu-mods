using System;
using FrameWork;

namespace EasyQuickSaveLoad.Frontend
{
    internal static class NativeDialog
    {
        /// <summary>
        /// Shows a native confirm dialog (DialogCmd + MaskUI(UIElement.Dialog)).
        ///
        /// Two hazards this method guards against, both observed as
        /// "[UIManager]NullReferenceException at UIElement.InitElement" (UiBase null):
        ///
        /// 1) Async-callback context. Confirm is invoked from the async ListSlots mod-method-return
        ///    callback (EqslCore.RequestSlots). Showing UIElement.Dialog straight from that context
        ///    races the UI teardown/rebuild that a nearby world-reload drives. We defer the show one
        ///    frame via YieldHelper.DelayFrameDo so it runs in a clean frame, like a normal click.
        ///
        /// 2) Zombie element. If a prior world-reload teardown (UIManager.DestroyAll) destroyed the
        ///    Dialog's UiBase but left its state machine in a non-Sleep state, UIElement.Show() takes
        ///    its "already prepared" branch (-> Reset -> DataPrepare -> UiBase.OnInit) and NREs on the
        ///    null UiBase. Detect that (IsShowing && !Exist) and force the element back to Sleep via
        ///    Destroy() so Show() re-runs ResourcePrepare and rebuilds UiBase.
        /// </summary>
        public static void Confirm(string title, string content, Action onYes)
        {
            try
            {
                SingletonObject.getInstance<YieldHelper>()
                    .DelayFrameDo(1u, () => ShowNow(title, content, onYes));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] Confirm scheduling failed: " + ex);
            }
        }

        private static void ShowNow(string title, string content, Action onYes)
        {
            try
            {
                ResetDialogIfZombie();
                UIElement dialog = UIElement.Dialog;

                var cmd = new DialogCmd
                {
                    Title = title,
                    Content = content,
                    Type = 1,
                    Yes = () =>
                    {
                        try
                        {
                            onYes?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError("[EasyQuickSaveLoad] dialog action failed: " + ex);
                        }
                    }
                };
                dialog.SetOnInitArgs(EasyPool.Get<ArgumentBox>().SetObject("Cmd", cmd));
                UIManager.Instance.MaskUI(dialog);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] Confirm dialog failed: " + ex);
            }
        }

        /// <summary>
        /// Reset the native <c>UIElement.Dialog</c> from a "zombie" state back to Sleep.
        ///
        /// A world-reload teardown (UIManager.DestroyAll during our LoadSlotWorld) frees the Dialog's
        /// UiBase but leaves its state machine in a non-Sleep state. The next <c>Show()</c> — whether ours
        /// OR the game's own <c>GameApp.GameQuitConfirm()</c> quit dialog — then takes the "already
        /// prepared" branch (Reset -> DataPrepare -> InitElement) and NREs on the null UiBase.
        ///
        /// Our ShowNow calls this before showing, but the GAME's quit-confirm doesn't — so we ALSO call it
        /// right after a load finishes (EqslCore.OnEnterWorldLoadFinish), leaving the Dialog clean so a
        /// later quit (or any native Dialog show) rebuilds UiBase instead of crashing.
        ///
        /// Fires whenever UiBase is missing (!Exist) and we're not already Sleep — covering Hiding /
        /// AnimateOut too. Destroy() safely routes any state to Sleep.
        /// </summary>
        public static void ResetDialogIfZombie()
        {
            try
            {
                UIElement dialog = UIElement.Dialog;
                if (dialog != null && !dialog.Exist && !dialog.IsInState(EUiElementState.Sleep))
                {
                    dialog.Destroy();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] ResetDialogIfZombie failed: " + ex);
            }
        }
    }
}
