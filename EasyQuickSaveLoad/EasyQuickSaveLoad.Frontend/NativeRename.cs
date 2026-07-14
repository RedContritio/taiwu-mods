using System;
using FrameWork;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// Pops the game's native text-input dialog (ViewRename) via <see cref="RenameCfg"/> to let the player
    /// type a 备注 (note/name), and delivers the confirmed, trimmed string to a callback. Same style the
    /// game uses to rename a save in RecordSelect, so it carries the native rename UI + sensitive-word filter.
    /// </summary>
    internal static class NativeRename
    {
        public static void Prompt(string title, string initial, int charLimit, Action<string> onSubmit)
        {
            try
            {
                var cfg = new RenameCfg
                {
                    Title = title,
                    Description = string.Empty,
                    IsHideDescription = true,
                    EmptyDesc = string.Empty,
                    Default = initial ?? string.Empty,
                    CharCount = charLimit,
                    Submit = s =>
                    {
                        try { onSubmit?.Invoke(s); }
                        catch (Exception ex) { UnityEngine.Debug.LogError("[EasyQuickSaveLoad] rename submit failed: " + ex); }
                    }
                };
                cfg.Show();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[EasyQuickSaveLoad] rename prompt failed: " + ex);
            }
        }
    }
}
