using System;
using System.Reflection;
using FrameWork;
using FrameWork.UISystem.UIElements;
using UnityEngine;

namespace EasyQuickSaveLoad.Frontend
{
    /// <summary>
    /// MonoBehaviour host that clones native CButtons from ViewSystemOption into the ESC system-option menu.
    /// The clone anchor is btnReturnToGame (private SerializeField), discovered by reflection at runtime.
    /// Label setting uses reflection on a "text" property to handle both UnityEngine.UI.Text and
    /// TMPro.TextMeshProUGUI without a compile-time TMP dependency.
    /// </summary>
    public sealed class SystemOptionButtonInjector : MonoBehaviour
    {
        private static SystemOptionButtonInjector _instance;

        private readonly System.Collections.Generic.List<GameObject> _injected
            = new System.Collections.Generic.List<GameObject>();

        // Typed references to the four cloned CButtons (cast from Component).
        private CButton _save;
        private CButton _load;
        private CButton _quickSave;
        private CButton _quickLoad;

        // The ViewSystemOption instance we last injected into — used to detect teardown/rebuild.
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
                // Menu not open or not focused — clean up any leftover injected objects.
                if (_boundUiBase != null) Cleanup();
                return;
            }

            if (!ReferenceEquals(uiBase, _boundUiBase))
            {
                // New (or rebuilt) ViewSystemOption instance — clean up the old injection, then inject fresh.
                Cleanup();
                try
                {
                    Inject(uiBase);
                    _boundUiBase = uiBase;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[EasyQuickSaveLoad] Button injection failed: " + ex);
                    // Graceful degradation: leave the menu unmodified, no crash.
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

        // ---- Injection core ------------------------------------------------

        private void Inject(object viewSystemOption)
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
                    + " — skipping injection.");
                return;
            }

            var parent = srcButton.transform.parent;

            // Clone 4 buttons into the same parent container.
            _save      = CloneButton(srcButton, parent, "存档",    OnSaveClick);
            _load      = CloneButton(srcButton, parent, "读档",    OnLoadClick);
            _quickSave = CloneButton(srcButton, parent, "快速存档", OnQuickSaveClick);
            _quickLoad = CloneButton(srcButton, parent, "快速读档", OnQuickLoadClick);

            // Place them contiguously right after the clone source (继续游戏).
            int baseIndex = srcButton.transform.GetSiblingIndex();
            _save.transform.SetSiblingIndex(baseIndex + 1);
            _load.transform.SetSiblingIndex(baseIndex + 2);
            _quickSave.transform.SetSiblingIndex(baseIndex + 3);
            _quickLoad.transform.SetSiblingIndex(baseIndex + 4);
        }

        /// <summary>
        /// Clones srcButton into parent, renames the object, sets the label via reflection
        /// (handles both UnityEngine.UI.Text.text and TMPro.TextMeshProUGUI.text without a TMP
        /// compile-time reference), wires the click listener, and tracks the cloned GO for cleanup.
        /// </summary>
        private CButton CloneButton(CButton src, Transform parent, string label, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
            go.name = "EQSL_" + label;
            _injected.Add(go);

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
            foreach (var go in _injected)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _injected.Clear();
            _save = _load = _quickSave = _quickLoad = null;
            _boundUiBase = null;
        }

        // ---- Click handlers (Task 5/6 will fill real implementations) ------

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
            Debug.Log("[EasyQuickSaveLoad] QuickSave clicked");
        }

        public void OnQuickLoadClick()
        {
            Debug.Log("[EasyQuickSaveLoad] QuickLoad clicked");
        }
    }
}
