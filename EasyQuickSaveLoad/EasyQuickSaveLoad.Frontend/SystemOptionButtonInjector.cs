using System;
using System.Reflection;
using FrameWork;
using FrameWork.UISystem.UIElements;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using GameData.Domains.World;
using GameData.Serializer;
using UnityEngine;
using UnityEngine.UI;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// MonoBehaviour host that builds a SEPARATE, far-right panel reusing the native
    /// ViewSystemOption window frame style, holding only the four EQSL buttons
    /// (存档/读档/快速存档/快速读档). It does NOT modify or insert into the native ESC menu.
    /// The panel is cloned from the native styled window (MAINWINDOW) discovered via the
    /// private [SerializeField] CButton btnReturnToGame anchor, appears/disappears with the
    /// ESC menu (same focus lifecycle), and is destroyed on menu close / rebuild.
    /// Label setting uses reflection on a "text" property to handle both UnityEngine.UI.Text and
    /// TMPro.TextMeshProUGUI without a compile-time TMP dependency.
    /// </summary>
    public sealed class SystemOptionButtonInjector : MonoBehaviour
    {
        private static SystemOptionButtonInjector _instance;

        // The single cloned panel GameObject (native-styled frame + 4 buttons). Destroyed on cleanup.
        private GameObject _panel;

        // Typed references to the four cloned CButtons (cast from Component).
        private CButton _save;
        private CButton _load;
        private CButton _quickSave;
        private CButton _quickLoad;

        // The ViewSystemOption instance we last built the panel for — used to detect teardown/rebuild.
        private object _boundUiBase;

        // ---- Factory -------------------------------------------------------

        public static SystemOptionButtonInjector Create()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("EasyQuickSaveLoad.Injector");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SystemOptionButtonInjector>();
            return _instance;
        }

        public static void Destroy(SystemOptionButtonInjector injector)
        {
            if (injector == null) return;
            if (_instance == injector) _instance = null;
            UnityEngine.Object.Destroy(injector.gameObject);
        }

        // ---- Unity lifecycle -----------------------------------------------

        private void Update()
        {
            object uiBase = GetSystemOptionUiBaseIfFocused();

            if (uiBase == null)
            {
                // Menu not open or not focused — destroy any leftover panel.
                if (_boundUiBase != null) Cleanup();
                return;
            }

            if (!ReferenceEquals(uiBase, _boundUiBase))
            {
                // New (or rebuilt) ViewSystemOption instance — destroy the old panel, then build fresh.
                Cleanup();
                try
                {
                    BuildPanel(uiBase);
                    _boundUiBase = uiBase;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[EasyQuickSaveLoad] Panel build failed: " + ex);
                    // Graceful degradation: leave the native menu unmodified, no crash.
                }
            }

            UpdateInteractable();
        }

        // ---- Focus guard ---------------------------------------------------

        /// <summary>
        /// Returns the live ViewSystemOption UiBase when the ESC panel is open and focused,
        /// null in every other case. Pattern mirrors EasyQuickSaveLoadOverlay.GetEscPanelParent.
        /// </summary>
        private static object GetSystemOptionUiBaseIfFocused()
        {
            try
            {
                if (GameApp.Instance == null ||
                    GameApp.Instance.GetCurrentGameStateName() != EGameState.InGame)
                    return null;

                if (UIManager.Instance == null ||
                    !UIElement.SystemOption.Exist ||
                    UIElement.SystemOption.UiBase == null)
                    return null;

                if (!UIManager.Instance.IsFocusElement(UIElement.SystemOption))
                    return null;

                return UIElement.SystemOption.UiBase;
            }
            catch
            {
                return null;
            }
        }

        // ---- Panel construction --------------------------------------------

        // Layout tuning constants for the far-right panel.
        private const float RightMargin       = 0f;    // flush to the right edge (水平贴右)
        private const float ButtonHeight      = 30f;   // approx native CButton height
        private const float VerticalPadding   = 180f;  // frame chrome incl. the title, above/below the 4 buttons
        private const float SlicedWidth       = 240f;  // narrowed width when the frame sprite is Sliced
        private const string PanelTitle       = "快速 SL";

        private void BuildPanel(object viewSystemOption)
        {
            // Reflect the private [SerializeField] CButton field from ViewSystemOption.
            var type = viewSystemOption.GetType();
            var srcField = type.GetField(
                "btnReturnToGame",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var srcButton = srcField?.GetValue(viewSystemOption) as CButton;
            if (srcButton == null)
            {
                Debug.LogWarning(
                    "[EasyQuickSaveLoad] Clone anchor btnReturnToGame not found on " + type.FullName
                    + " — building nothing.");
                return;
            }

            // Structure (verified in-game):
            //   btnReturnToGame.parent        = CONTENT  (VerticalLayoutGroup)
            //   btnReturnToGame.parent.parent = MAINWINDOW (CImage frame)
            //   .parent.parent.parent         = CANVAS ROOT (full-screen Canvas)
            var content    = srcButton.transform.parent;
            var mainWindow = content?.parent;
            var canvasRoot = mainWindow?.parent;
            if (mainWindow == null || canvasRoot == null)
            {
                Debug.LogWarning(
                    "[EasyQuickSaveLoad] Native window/canvas hierarchy not found — building nothing.");
                return;
            }

            // Clone the whole native-styled frame (frame + a CONTENT child with native buttons).
            _panel = UnityEngine.Object.Instantiate(mainWindow.gameObject, canvasRoot);
            _panel.name = "EQSL_Panel";

            // Locate the cloned CONTENT (the child with a VerticalLayoutGroup) and empty it.
            var clonedContent = FindContent(_panel.transform);
            if (clonedContent == null)
            {
                Debug.LogWarning(
                    "[EasyQuickSaveLoad] Cloned CONTENT (VerticalLayoutGroup) not found in panel clone.");
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
                return;
            }

            // Destroy ALL existing children of CONTENT (the cloned native buttons) so it starts empty.
            // Detach first so index math during Instantiate below is unaffected by pending destroys.
            for (int i = clonedContent.childCount - 1; i >= 0; i--)
            {
                var child = clonedContent.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            // Keep the native title element (so the panel has a header like 系统选项) and relabel it
            // to PanelTitle. Strip any OTHER decorations, keeping only the frame + title + CONTENT.
            var title = _panel.transform.Find("Title");
            if (title != null)
            {
                SetLabelViaReflection(title.gameObject, PanelTitle);
            }
            for (int i = _panel.transform.childCount - 1; i >= 0; i--)
            {
                var child = _panel.transform.GetChild(i);
                if (child == clonedContent || child == title) continue;
                UnityEngine.Object.Destroy(child.gameObject);
            }

            // Clone the native button (from the LIVE source, which is intact) 4× into the empty CONTENT.
            _save      = BuildButton(srcButton, clonedContent, "存档",     OnSaveClick);
            _load      = BuildButton(srcButton, clonedContent, "读档",     OnLoadClick);
            _quickSave = BuildButton(srcButton, clonedContent, "快速存档", OnQuickSaveClick);
            _quickLoad = BuildButton(srcButton, clonedContent, "快速读档", OnQuickLoadClick);

            // Position far-right + size for 4 buttons.
            var rt = _panel.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin       = new Vector2(1f, 0.5f);
                rt.anchorMax       = new Vector2(1f, 0.5f);
                rt.pivot           = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-RightMargin, 0f);

                var size = rt.sizeDelta;
                size.y = 4f * ButtonHeight + VerticalPadding;

                // Only narrow width if the frame sprite is Sliced (safe); otherwise keep native width
                // to avoid distorting the frame sprite.
                var frameImg = _panel.GetComponent<Image>();
                if (frameImg != null && frameImg.type == Image.Type.Sliced)
                    size.x = SlicedWidth;

                rt.sizeDelta = size;
            }
        }

        /// <summary>
        /// Finds the CONTENT transform within the cloned panel: the first descendant carrying a
        /// <see cref="VerticalLayoutGroup"/>.
        /// </summary>
        private static Transform FindContent(Transform panelRoot)
        {
            var vlg = panelRoot.GetComponentInChildren<VerticalLayoutGroup>(true);
            return vlg != null ? vlg.transform : null;
        }

        /// <summary>
        /// Clones srcButton into parent, renames the object, sets the label via reflection
        /// (handles both UnityEngine.UI.Text.text and TMPro.TextMeshProUGUI.text without a TMP
        /// compile-time reference), and wires the click listener.
        /// </summary>
        private CButton BuildButton(CButton src, Transform parent, string label, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
            go.name = "EQSL_" + label;

            // Set label: search all child components for one that has a public settable "text" string property.
            // This handles both UnityEngine.UI.Text and TMPro.TextMeshProUGUI without TMP dependency.
            SetLabelViaReflection(go, label);

            var btn = go.GetComponent<CButton>();
            btn.ClearAndAddListener(onClick);
            return btn;
        }

        /// <summary>
        /// Searches every Component on root and its children for one with a public settable
        /// <c>string</c> property named <c>text</c>, then sets it. Handles Text and TMP without
        /// a compile-time TMP dependency.
        /// </summary>
        private static void SetLabelViaReflection(GameObject root, string label)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null) continue;
                var prop = component.GetType().GetProperty(
                    "text",
                    BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(string)) continue;
                try
                {
                    prop.SetValue(component, label, null);
                    return; // First match wins — the button's label component.
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[EasyQuickSaveLoad] Failed to set label via reflection on "
                        + component.GetType().Name + ": " + ex.Message);
                }
            }
            Debug.LogWarning(
                "[EasyQuickSaveLoad] No text-bearing component found on cloned button for label: " + label);
        }

        // ---- Per-frame interactable update ---------------------------------

        private void UpdateInteractable()
        {
            bool ok = EqslCore.CanOperate(out _);
            if (_save      != null) _save.interactable      = ok;
            if (_load      != null) _load.interactable      = ok;
            if (_quickSave != null) _quickSave.interactable = ok;
            if (_quickLoad != null) _quickLoad.interactable = ok;
        }

        // ---- Cleanup -------------------------------------------------------

        private void Cleanup()
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            _panel = null;
            _save = _load = _quickSave = _quickLoad = null;
            _boundUiBase = null;
        }

        // ---- Quick slot constant --------------------------------------------

        /// <summary>Quick-save/load uses slot index -1 (the dedicated quick slot).</summary>
        private const int QuickSlot = -1;

        // ---- Click handlers ------------------------------------------------

        public void OnSaveClick()
        {
            Debug.Log("[EasyQuickSaveLoad] Save clicked");
        }

        public void OnLoadClick()
        {
            Debug.Log("[EasyQuickSaveLoad] Load clicked");
        }

        public void OnQuickSaveClick()
        {
            if (!EqslCore.CanOperate(out _)) return;

            Action doSave = () => EqslCore.CallSave(QuickSlot);

            if (EqslSettings.QuickSaveConfirm)
            {
                EqslCore.RequestSlots(res =>
                {
                    string summary = QuickSummary(res);
                    NativeDialog.Confirm("确认快速存档", "将覆盖快捷存档\n" + summary, doSave);
                });
            }
            else
            {
                doSave();
            }
        }

        public void OnQuickLoadClick()
        {
            if (!EqslCore.CanOperate(out _)) return;

            EqslCore.RequestSlots(res =>
            {
                var wi = QuickWorldInfo(res);
                if (wi == null) return; // quick slot empty — no-op

                Action doLoad = () =>
                {
                    HideSystemOption();
                    EqslCore.LoadSlotWorld(QuickSlot);
                };

                if (EqslSettings.QuickLoadConfirm)
                    NativeDialog.Confirm("确认快速读档", EqslCore.FormatWorldInfoBrief(wi), doLoad);
                else
                    doLoad();
            });
        }

        // ---- Quick-slot helpers --------------------------------------------

        /// <summary>
        /// Returns a one-line summary of the quick slot's current save, or "（空）" when empty.
        /// </summary>
        private static string QuickSummary(SerializableModData res)
        {
            var wi = QuickWorldInfo(res);
            return wi != null ? EqslCore.FormatWorldInfoBrief(wi) : "（空）";
        }

        /// <summary>
        /// Extracts the <see cref="GameData.Domains.World.WorldInfo"/> stored in the quick slot
        /// from a ListSlots result, or null if the slot is empty or the result is unavailable.
        /// </summary>
        private static WorldInfo QuickWorldInfo(SerializableModData res)
        {
            if (res == null) return null;
            if (!res.Get("QuickInfo", out ArchiveInfo qi)) return null;
            return qi?.WorldInfo;
        }

        // ---- System-option hide helper -------------------------------------

        private static void HideSystemOption()
        {
            if (UIManager.Instance != null && UIElement.SystemOption.Exist)
            {
                UIManager.Instance.HideUI(UIElement.SystemOption);
            }
        }
    }
}
