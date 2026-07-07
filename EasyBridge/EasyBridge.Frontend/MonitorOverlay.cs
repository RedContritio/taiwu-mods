using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Native in-game monitor — a draggable, resizable IMGUI window showing the bridge's live activity
    /// (every request through either pipe, with its note + source/method/path/status). Lives on a
    /// DontDestroyOnLoad GameObject in the game process, so it appears with the game and dies with it.
    ///
    /// Rendered with legacy IMGUI (<see cref="OnGUI"/>) because a mod-added runtime uGUI Canvas can
    /// silently fail to composite in this game. Drag the title bar to move; drag the bottom-right grip to
    /// resize; content scrolls. F9 shows/hides, F8 pauses. The window's font size follows the game's own
    /// 正文字号 setting (the EventWindow's <c>s_savedContentFontSize</c> slider, read by reflection) on top
    /// of a resolution (DPI) scale, so it matches the game's text.
    ///
    /// Note: IMGUI input is consumed for our own drag/resize, but uGUI reads OS input separately, so a click
    /// on the window can also reach the game underneath. We deliberately do NOT disable the game's
    /// EventSystem to prevent that — doing so made the game's own per-frame hotkey code NRE. The minor
    /// click-through is an acceptable trade for a watch-mostly window; move it over empty space if needed.
    /// </summary>
    internal sealed class MonitorOverlay : MonoBehaviour
    {
        private const int MaxEntries = 60;          // scrollable window, so show more than the old fixed panel
        private const KeyCode ToggleKey = KeyCode.F9;
        private const KeyCode PauseKey = KeyCode.F8;
        private const string StateFile = "monitor-state.json";   // persisted window rect + visibility, kept in the mod folder

        private bool _visible = false;
        private bool _paused;
        private bool _built;
        private float _timer;

        private string _headerStr = "EasyBridge";
        private string _bodyStr = "";

        private Rect _win;
        private bool _winInit;
        private WinState _saved;
        private Vector2 _scroll;
        private bool _dragging, _resizing;
        private Vector2 _grabMouse;
        private Rect _grabWin;

        private float _scale = 1f;
        private float _appliedScale = -1f;
        private int _headSize = 15;

        private Font _font;
        private bool _ownsFont;
        private Texture2D _bgTex;
        private GUIStyle _headerStyle, _bodyStyle;

        // Game's 正文字号 setting (EventWindow.s_savedContentFontSize): scanned once, by field name so a class
        // rename doesn't break us. Default 24; the panel scales proportionally to gameFont/24.
        private static FieldInfo _gameFontField;
        private static bool _gameFontSearched;

        private static MonitorOverlay _instance;

        public static GameObject Create()
        {
            if (_instance != null) return _instance.gameObject;
            var go = new GameObject("EasyBridgeMonitor");
            DontDestroyOnLoad(go);
            go.AddComponent<MonitorOverlay>();
            return go;
        }

        public static void DestroyInstance()
        {
            if (_instance == null) return;
            Destroy(_instance.gameObject);
        }

        public static void Show()
        {
            if (_instance == null) return;
            _instance._visible = true;
            _instance.SaveState();
        }

        /// <summary>Diagnostic snapshot of the monitor's own state, callable over the bridge via
        /// <c>POST /static {type:"EasyBridge.Frontend.MonitorOverlay", method:"DebugState"}</c> — lets a
        /// headless agent confirm the in-game window is built, sized, and tracking the game's font setting
        /// without a screenshot.</summary>
        public static string DebugState()
        {
            var o = _instance;
            if (o == null) return "overlay: no instance (not created)";
            return string.Format(
                "built={0} visible={1} paused={2} scale={3:F2} gameFont={4:F0} fontFieldFound={5} " +
                "win=({6:F0},{7:F0} {8:F0}x{9:F0}) bodyLen={10} screen={11}x{12} state={13} exists={14}",
                o._built, o._visible, o._paused, o._scale, ReadGameFontSize(), _gameFontField != null,
                o._win.x, o._win.y, o._win.width, o._win.height, o._bodyStr != null ? o._bodyStr.Length : 0,
                Screen.width, Screen.height, StatePath(), File.Exists(StatePath()));
        }

        private void Awake()
        {
            _instance = this;
            _saved = LoadState();
            if (_saved != null) _visible = _saved.visible;   // restore last run's show/hide
            _font = GetFont(out _ownsFont);
            if (_font == null)
                Debug.LogWarning("[EasyBridge] monitor: no usable CJK font found — overlay text may show as boxes.");
            else if (_ownsFont)
                _font.hideFlags = HideFlags.HideAndDontSave;

            _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _bgTex.SetPixel(0, 0, Color.white);
            _bgTex.Apply();
            _bgTex.hideFlags = HideFlags.HideAndDontSave;

            _built = true;
            RefreshScaleAndStrings();
        }

        private void Update()
        {
            if (!_built) return;
            if (Input.GetKeyDown(ToggleKey)) { _visible = !_visible; SaveState(); }
            if (Input.GetKeyDown(PauseKey)) { _paused = !_paused; RebuildHeader(); }

            _timer += Time.unscaledDeltaTime;
            if (_timer >= 0.4f) { _timer = 0f; RefreshScaleAndStrings(); }
        }

        private void RefreshScaleAndStrings()
        {
            float gameFont = ReadGameFontSize();
            float s = Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f) * Mathf.Clamp(gameFont / 24f, 0.6f, 1.7f);
            _scale = Mathf.Clamp(s, 1f, 3.2f);
            RebuildHeader();
            if (_visible && !_paused) _bodyStr = RequestLog.RenderRecent(MaxEntries);
        }

        private void RebuildHeader()
        {
            int hintSize = Mathf.Max(10, Mathf.RoundToInt(15 * _scale * 0.66f));
            string paused = _paused ? "  <color=#e0896a>已暂停</color>" : "";
            _headerStr = "<b>EasyBridge</b>" + paused
                + "\n<size=" + hintSize + "><color=#9a917a>F9 隐藏 · F8 暂停 · 拖动标题栏移动</color></size>";
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null && Mathf.Approximately(_appliedScale, _scale)) return;
            _appliedScale = _scale;
            _headSize = Mathf.RoundToInt(15 * _scale);
            int bodySize = Mathf.RoundToInt(14 * _scale);

            _headerStyle = new GUIStyle { richText = true, wordWrap = false, alignment = TextAnchor.UpperLeft, fontSize = _headSize, clipping = TextClipping.Clip };
            _bodyStyle = new GUIStyle { richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = bodySize };
            if (_font != null) { _headerStyle.font = _font; _bodyStyle.font = _font; }
            _headerStyle.normal.textColor = new Color(0.85f, 0.65f, 0.34f);
            _bodyStyle.normal.textColor = new Color(0.92f, 0.88f, 0.81f);
        }

        private void EnsureWinInit()
        {
            if (_winInit) return;
            _winInit = true;
            if (_saved != null && _saved.w > 1f && _saved.h > 1f)   // restore last run's position + size
            {
                _win = new Rect(_saved.x, _saved.y, _saved.w, _saved.h);
                ClampWin();   // resolution may differ from last run — keep it grabbable on-screen
            }
            else
            {
                float w = Mathf.Min(Screen.width * 0.5f, 460f * _scale);
                float h = Screen.height * 0.55f;
                _win = new Rect(Screen.width - w - 20f, 20f, w, h);
            }
        }

        private class WinState { public float x, y, w, h; public bool visible = true; }

        // Mod-private state file: <game>/Mod/EasyBridge/monitor-state.json (next to the mod, not the registry).
        private static string StatePath()
        {
            try
            {
                // Application.dataPath = <game>/The Scroll of Taiwu_Data → parent is <game>; mod lives at <game>/Mod/EasyBridge.
                var gameRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(gameRoot))
                    return Path.Combine(gameRoot, "Mod", "EasyBridge", StateFile);
            }
            catch { }
            return Path.Combine(Application.persistentDataPath, "EasyBridge." + StateFile);
        }

        private static WinState LoadState()
        {
            try
            {
                var p = StatePath();
                if (!File.Exists(p)) return null;
                var o = Json.Parse(File.ReadAllText(p));
                if (!(o is System.Collections.Generic.IDictionary<string, object>)) return null;
                var s = new WinState();
                if (Json.TryGetDouble(o, "x", out var x)) s.x = (float)x;
                if (Json.TryGetDouble(o, "y", out var y)) s.y = (float)y;
                if (Json.TryGetDouble(o, "w", out var w)) s.w = (float)w;
                if (Json.TryGetDouble(o, "h", out var h)) s.h = (float)h;
                if (!Json.TryGetBool(o, "visible", out s.visible)) s.visible = false;
                return s;
            }
            catch { }
            return null;
        }

        private void SaveState()
        {
            try
            {
                var d = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["x"] = _win.x, ["y"] = _win.y, ["w"] = _win.width, ["h"] = _win.height, ["visible"] = _visible,
                };
                File.WriteAllText(StatePath(), Json.Write(d));
            }
            catch { }
        }

        private void OnGUI()
        {
            if (!_built || !_visible) return;
            EnsureStyles();
            EnsureWinInit();

            float ui = _scale;
            float titleH = _headSize * 2.5f + 8f;   // two lines: title + dim hint
            float pad = 8f * ui;
            float grip = 18f * ui;

            Event e = Event.current;
            Rect titleRect = new Rect(_win.x, _win.y, _win.width, titleH);
            Rect gripRect = new Rect(_win.xMax - grip, _win.yMax - grip, grip, grip);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (gripRect.Contains(e.mousePosition)) { _resizing = true; _grabMouse = e.mousePosition; _grabWin = _win; e.Use(); }
                else if (titleRect.Contains(e.mousePosition)) { _dragging = true; _grabMouse = e.mousePosition; _grabWin = _win; e.Use(); }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_resizing)
                {
                    _win.width = _grabWin.width + (e.mousePosition.x - _grabMouse.x);
                    _win.height = _grabWin.height + (e.mousePosition.y - _grabMouse.y);
                    e.Use();
                }
                else if (_dragging)
                {
                    _win.x = _grabWin.x + (e.mousePosition.x - _grabMouse.x);
                    _win.y = _grabWin.y + (e.mousePosition.y - _grabMouse.y);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (_dragging || _resizing) { _dragging = _resizing = false; SaveState(); }   // persist new position/size
            }
            ClampWin();

            if (e.type == EventType.Repaint && (_dragging || _resizing) && !Input.GetMouseButton(0))
            { _dragging = _resizing = false; SaveState(); } // safety: recover if a MouseUp was missed

            titleRect = new Rect(_win.x, _win.y, _win.width, titleH);
            gripRect = new Rect(_win.xMax - grip, _win.yMax - grip, grip, grip);

            GUI.color = new Color(0.05f, 0.045f, 0.035f, 0.95f);
            GUI.DrawTexture(_win, _bgTex);
            GUI.color = new Color(0.13f, 0.11f, 0.085f, 0.98f);
            GUI.DrawTexture(titleRect, _bgTex);
            GUI.color = new Color(0.85f, 0.65f, 0.34f, 0.55f);
            GUI.DrawTexture(new Rect(_win.x, _win.y, _win.width, 2f), _bgTex);
            GUI.color = Color.white;

            DrawLabelSafe(new Rect(titleRect.x + pad, titleRect.y, titleRect.width - 2 * pad, titleRect.height), _headerStr, _headerStyle);

            Rect view = new Rect(_win.x + pad, _win.y + titleH, _win.width - 2 * pad, _win.height - titleH - pad);
            if (view.width > 30 && view.height > 12)
            {
                float innerW = view.width - 18f;
                float contentH = Mathf.Max(view.height, _bodyStyle.CalcHeight(new GUIContent(_bodyStr), innerW));
                _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, innerW, contentH));
                DrawLabelSafe(new Rect(0, 0, innerW, contentH), _bodyStr, _bodyStyle);
                GUI.EndScrollView();
            }

            // resize grip marks
            GUI.color = new Color(0.85f, 0.65f, 0.34f, 0.5f);
            GUI.DrawTexture(new Rect(gripRect.x + grip * 0.4f, gripRect.yMax - 4f, grip * 0.45f, 2f), _bgTex);
            GUI.DrawTexture(new Rect(gripRect.xMax - 4f, gripRect.y + grip * 0.4f, 2f, grip * 0.45f), _bgTex);
            GUI.color = Color.white;
        }

        private void ClampWin()
        {
            _win.width = Mathf.Clamp(_win.width, 240f, Screen.width);
            _win.height = Mathf.Clamp(_win.height, 120f, Screen.height);
            _win.x = Mathf.Clamp(_win.x, -_win.width + 90f, Screen.width - 90f); // keep a grabbable strip on-screen
            _win.y = Mathf.Clamp(_win.y, 0f, Screen.height - 40f);
        }

        private static void DrawLabelSafe(Rect r, string text, GUIStyle style)
        {
            try { GUI.Label(r, text, style); }
            catch
            {
                bool prev = style.richText;
                style.richText = false;
                try { GUI.Label(r, text, style); } catch { }
                style.richText = prev;
            }
        }

        private static float ReadGameFontSize()
        {
            try
            {
                if (!_gameFontSearched)
                {
                    _gameFontSearched = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "Assembly-CSharp") continue;
                        Type[] types;
                        try { types = asm.GetTypes(); }
                        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                        foreach (var t in types)
                        {
                            if (t == null) continue;
                            var f = t.GetField("s_savedContentFontSize", BindingFlags.NonPublic | BindingFlags.Static);
                            if (f != null && f.FieldType == typeof(float)) { _gameFontField = f; break; }
                        }
                        break;
                    }
                }
                if (_gameFontField != null)
                {
                    float v = (float)_gameFontField.GetValue(null);
                    if (v >= 10f && v <= 60f) return v;
                }
            }
            catch { }
            return 24f;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_bgTex != null) Destroy(_bgTex);
            if (_font != null && _ownsFont) Destroy(_font);
            _font = null;
        }

        private static Font GetFont(out bool owns)
        {
            owns = false;
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun" }, 16);
                if (f != null) { owns = true; return f; }
            }
            catch { }
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { var f = Resources.GetBuiltinResource<Font>(name); if (f != null) return f; } catch { }
            }
            try { var f = Font.CreateDynamicFontFromOSFont("Arial", 16); if (f != null) { owns = true; return f; } } catch { }
            return null;
        }
    }
}
