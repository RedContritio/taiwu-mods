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

        // Clone templates captured from the live native menu each rebuild, handed to the slot picker.
        private GameObject _frameTemplate;   // native MAINWINDOW frame to clone
        private CButton _srcButton;          // native CButton to clone
        private Transform _canvasRootTf;     // the SystemOption canvas root

        // The self-built manual-slot picker (存档/读档). Created lazily, closed on menu teardown.
        private SlotPickerPanel _slotPicker;

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

        // Synchronous in-flight guard for the quick buttons (parity with the picker's _busy). Reset on a
        // realtime timer so it never sticks if the game is paused (timeScale=0) during the ESC menu.
        private bool _quickBusy;
        private float _quickBusyUntil;

        private void MarkQuickBusy()
        {
            _quickBusy = true;
            _quickBusyUntil = Time.realtimeSinceStartup + 1.5f;
        }

        private void Update()
        {
            if (_quickBusy && Time.realtimeSinceStartup >= _quickBusyUntil) _quickBusy = false;

            object uiBase = GetSystemOptionUiBaseIfFocused();

            if (uiBase == null)
            {
                // A native dialog (Rename / Dialog) shown OVER our slot picker steals focus from
                // SystemOption. Do NOT tear the picker down for that: keep everything alive as long as
                // SystemOption still exists and the picker is open. Only clean up once the ESC menu is gone.
                if (UIElement.SystemOption.Exist && _slotPicker != null && _slotPicker.IsOpen) return;

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
        /// null in every other case.
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
        private const float ButtonHeight      = 62f;   // actual native CButton height (measured in-game)
        private const float VerticalPadding   = 132f;  // title band + content top-pad + bottom margin (4*62+132 = 380 tall, fits all 4)
        private const float PanelWidth        = 320f;  // frame width — wide enough to visibly contain the buttons + margin
        private const float ButtonWidth       = 170f;  // explicit button width (narrower than native, generous side margin in the frame)
        private const float GroupGap          = 22f;   // vertical gap separating the quick-SL group from the manual-SL group
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

            // Capture live clone templates for the on-demand slot picker (存档/读档).
            _frameTemplate = mainWindow.gameObject;
            _srcButton = srcButton;
            _canvasRootTf = canvasRoot;

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

            // Two groups, quick group on TOP: [快速存档, 快速读档] — gap — [存档, 读档].
            // Clone the native button (from the LIVE source, which is intact) into the empty CONTENT.
            _quickSave = BuildButton(srcButton, clonedContent, "快速存档", OnQuickSaveClick);
            _quickLoad = BuildButton(srcButton, clonedContent, "快速读档", OnQuickLoadClick);
            AddSpacer(clonedContent, GroupGap);
            _save      = BuildButton(srcButton, clonedContent, "存档",     OnSaveClick);
            _load      = BuildButton(srcButton, clonedContent, "读档",     OnLoadClick);

            // Fixed narrower width, vertically centered with SYMMETRIC top/bottom padding: drop the cloned
            // native CONTENT's asymmetric 50px top pad so the space above the first button matches the space
            // below the last one. Stop the layout group from controlling/expanding child width.
            var vlg = clonedContent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childForceExpandWidth = false;
                vlg.childControlWidth = false;
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.padding = new RectOffset(0, 0, 0, 0);
            }
            foreach (var btn in new[] { _quickSave, _quickLoad, _save, _load })
            {
                if (btn?.transform is RectTransform brt)
                {
                    brt.sizeDelta = new Vector2(ButtonWidth, brt.sizeDelta.y);
                }
            }

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
                // Frame width wide enough to contain the buttons + margin. The native frame CImage is
                // 9-sliced, so resizing keeps the border crisp.
                size.x = PanelWidth;
                rt.sizeDelta = size;

                // 竖直居中对齐 ESC 主菜单：面板锚在画布竖直中心，但主菜单并不居于画布中心，按世界坐标把
                // 面板中心对到主菜单(MainWindow)中心。
                try
                {
                    Canvas.ForceUpdateCanvases();
                    var mwRt = mainWindow.transform as RectTransform;
                    if (mwRt != null)
                    {
                        var mw = new Vector3[4];
                        var pn = new Vector3[4];
                        mwRt.GetWorldCorners(mw);
                        rt.GetWorldCorners(pn);
                        float mwCenterY = (mw[0].y + mw[1].y) * 0.5f;
                        float pnCenterY = (pn[0].y + pn[1].y) * 0.5f;
                        float scaleY = rt.lossyScale.y;
                        if (Mathf.Abs(scaleY) > 0.0001f)
                            rt.anchoredPosition += new Vector2(0f, (mwCenterY - pnCenterY) / scaleY);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[EasyQuickSaveLoad] 面板竖直居中对齐失败: " + ex.Message);
                }
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
        /// Adds an invisible fixed-height spacer as the next child of a VerticalLayoutGroup, to separate
        /// button groups. Uses a LayoutElement so the layout reserves the gap regardless of childControlHeight.
        /// </summary>
        private static void AddSpacer(Transform parent, float height)
        {
            var go = new GameObject("EQSL_GroupGap", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            // The VLG has ChildControlHeight=false, so it lays children out by their ACTUAL rect height,
            // NOT LayoutElement.preferredHeight — force the rect height to the desired gap (point anchors so
            // sizeDelta == rect size). LayoutElement added too as a belt-and-suspenders for the group's own calc.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ButtonWidth, height);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
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
        internal static void SetLabelViaReflection(GameObject root, string label)
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
            _slotPicker?.Close();
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
            if (!EqslCore.CanOperate(out _)) return;
            EnsurePicker()?.Open(SlotPickerPanel.PickMode.Save);
        }

        public void OnLoadClick()
        {
            if (!EqslCore.CanOperate(out _)) return;
            EnsurePicker()?.Open(SlotPickerPanel.PickMode.Load);
        }

        /// <summary>Creates (or refreshes the templates of) the slot picker from the current live clones.</summary>
        private SlotPickerPanel EnsurePicker()
        {
            if (_frameTemplate == null || _srcButton == null || _canvasRootTf == null) return null;
            if (_slotPicker == null)
            {
                _slotPicker = new SlotPickerPanel();
            }
            _slotPicker.SetTemplates(_frameTemplate, _srcButton, _canvasRootTf);
            return _slotPicker;
        }

        public void OnQuickSaveClick()
        {
            if (_quickBusy || !EqslCore.CanOperate(out _)) return;
            MarkQuickBusy();

            // No CanOperate re-check inside doSave: it runs from the native Dialog's Yes callback, where the
            // open Dialog makes CanOperate false (界面中). The ESC menu + confirm are modal, so no month/combat/
            // event can start between the entry check and 确认 — the entry gate is sufficient.
            Action doSave = () => EqslCore.CallSave(QuickSlot);

            if (EqslSettings.QuickSaveConfirm)
            {
                EqslCore.RequestSlots(res =>
                {
                    if (GetSystemOptionUiBaseIfFocused() == null) return; // ESC closed while listing — no stray dialog
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
            if (_quickBusy || !EqslCore.CanOperate(out _)) return;
            MarkQuickBusy();

            EqslCore.RequestSlots(res =>
            {
                if (GetSystemOptionUiBaseIfFocused() == null) return; // ESC closed while listing — no stray dialog
                var wi = QuickWorldInfo(res);
                if (wi == null) return; // quick slot empty — no-op

                Action doLoad = () =>
                {
                    // No CanOperate re-check here: doLoad runs from the native Dialog's Yes callback where the
                    // open Dialog makes CanOperate false. The modal already prevents any state change.
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
