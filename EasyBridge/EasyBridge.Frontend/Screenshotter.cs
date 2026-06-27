using System.Collections.Generic;
using UnityEngine;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Captures the current frame to a PNG via Unity's <see cref="ScreenCapture"/>. The write happens at
    /// end of frame (asynchronously), so callers should poll for the file before reading it.
    /// </summary>
    internal static class Screenshotter
    {
        public static Dictionary<string, object> Capture(string path, int superSize)
        {
            if (string.IsNullOrEmpty(path))
                path = System.IO.Path.Combine(Application.persistentDataPath, "easybridge_shot.png");
            if (superSize < 1) superSize = 1;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(path, superSize);
            return new Dictionary<string, object> { ["ok"] = true, ["path"] = path };
        }
    }
}
