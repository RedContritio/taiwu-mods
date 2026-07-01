using System;
using FrameWork;

namespace EasyQuickSaveLoad.Frontend
{
    internal static class NativeDialog
    {
        public static void Confirm(string title, string content, Action onYes)
        {
            try
            {
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
                UIElement.Dialog.SetOnInitArgs(EasyPool.Get<ArgumentBox>().SetObject("Cmd", cmd));
                UIManager.Instance.MaskUI(UIElement.Dialog);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] Confirm dialog failed: " + ex);
            }
        }
    }
}
