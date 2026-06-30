using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FrameWork;
using GameData.Domains.Global;
using GameData.Domains.Mod;
using GameData.Domains.World;
using FrameWork.CommandSystem;
using GameData.GameDataBridge;
using GameData.Serializer;
using UnityEngine;
using UnityEngine.UI;
using MapAreaConfig = Config.MapArea;
using MapStateConfig = Config.MapState;

namespace EasyQuickSaveLoad.Frontend
{
    public sealed class EasyQuickSaveLoadOverlay : MonoBehaviour
    {
        private const float ConfirmSeconds = 4f;
        private const float ActionCooldownSeconds = 1.5f;
        private const float ArchiveInfoRefreshCooldownSeconds = 1.5f;
        private const string ModId = "EasyQuickSaveLoad";
        private const string SaveWorldWithBackupMethod = "SaveWorldWithBackup";
        private const string ListManualSavesMethod = "ListManualSaves";
        private const string LoadManualSaveMethod = "LoadManualSave";
        private const string SlotKey = "Slot";
        private const string TimestampKey = "Timestamp";
        private const string OkKey = "Ok";
        private const string CountKey = "Count";
        private const string CapacityKey = "Capacity";
        private const string ArchiveInfoKey = "ArchiveInfo";

        private static EasyQuickSaveLoadOverlay _instance;

        private enum PendingConfirmAction
        {
            None,
            QuickSave,
            QuickLoad,
            SavePoint
        }

        private GameObject _panel;
        private Button _saveButton;
        private Button _loadButton;
        private Button _savePointButton;
        private Button _loadPointButton;
        private Text _saveText;
        private Text _loadText;
        private Text _savePointText;
        private Text _loadPointText;
        private Text _archiveText;
        private Text _statusText;
        private RectTransform _panelRect;
        private Font _font;
        private Transform _currentPanelParent;

        private float _confirmUntil;
        private float _actionCooldownUntil;
        private float _nextArchiveInfoRefreshTime;
        private float _nextManualSaveInfoRefreshTime;
        private float _forceArchiveInfoRefreshAt;
        private float _statusUntil;
        private string _statusMessage = "";
        private sbyte _confirmSlot = -1;
        private sbyte _pendingSaveSlot = -1;
        private PendingConfirmAction _pendingConfirmAction = PendingConfirmAction.None;
        private bool _pendingSave;
        private bool _pendingSavePoint;
        private bool _sawSavingWorld;
        private readonly Dictionary<sbyte, int> _manualSaveCounts = new Dictionary<sbyte, int>();
        private readonly Dictionary<sbyte, int> _manualSaveCapacities = new Dictionary<sbyte, int>();

        public static EasyQuickSaveLoadOverlay Create()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject host = new GameObject("EasyQuickSaveLoad.Overlay");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EasyQuickSaveLoadOverlay>();
            return _instance;
        }

        public static void Destroy(EasyQuickSaveLoadOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (_instance == overlay)
            {
                _instance = null;
            }

            UnityEngine.Object.Destroy(overlay.gameObject);
        }

        private void Awake()
        {
            BuildUi();
        }

        private void Update()
        {
            if (_panel == null)
            {
                BuildUi();
            }

            Transform panelParent = GetEscPanelParent();
            bool shouldShow = panelParent != null;
            if (_panel.activeSelf != shouldShow)
            {
                _panel.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                DetachFromEscPanel();
                ResetConfirm();
                return;
            }

            AttachToEscPanel(panelParent);

            bool canOperate = CanOperate(out string reason, out sbyte slot);
            bool coolingDown = Time.unscaledTime < _actionCooldownUntil;
            bool interactable = canOperate && !coolingDown;
            _saveButton.interactable = interactable;
            _loadButton.interactable = interactable;
            _savePointButton.interactable = interactable;
            _loadPointButton.interactable = interactable;

            if (Time.unscaledTime > _confirmUntil)
            {
                ResetConfirm();
            }

            UpdatePendingSaveState();
            UpdateScheduledArchiveInfoRefresh();
            UpdateArchiveInfoText(canOperate, slot);
            UpdateStatus(canOperate, coolingDown, reason, slot);
        }

        private void BuildUi()
        {
            _font = CreateFont();

            _panel = new GameObject("Panel");
            _panel.transform.SetParent(transform, false);
            _panelRect = _panel.AddComponent<RectTransform>();
            _panelRect.sizeDelta = new Vector2(300f, 260f);
            LayoutElement layoutElement = _panel.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            Image panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.06f, 0.055f, 0.74f);

            _savePointButton = CreateButton("SavePointButton", new Vector2(-96f, 84f), "存档", out _savePointText);
            _loadPointButton = CreateButton("LoadPointButton", new Vector2(-96f, 28f), "读档", out _loadPointText);
            _saveButton = CreateButton("SaveButton", new Vector2(-96f, -28f), "快速存档", out _saveText);
            _loadButton = CreateButton("LoadButton", new Vector2(-96f, -84f), "快速读档", out _loadText);
            _saveButton.onClick.AddListener(QuickSave);
            _loadButton.onClick.AddListener(QuickLoad);
            _savePointButton.onClick.AddListener(SavePoint);
            _loadPointButton.onClick.AddListener(OpenLoadPoint);

            _archiveText = CreateText("ArchiveInfo", new Vector2(62f, 38f), new Vector2(168f, 160f), 12);
            _archiveText.alignment = TextAnchor.MiddleLeft;
            _archiveText.color = new Color(0.96f, 0.93f, 0.84f, 0.98f);
            _archiveText.text = "";

            _statusText = CreateText("Status", new Vector2(62f, -100f), new Vector2(168f, 42f), 12);
            _statusText.alignment = TextAnchor.MiddleLeft;
            _statusText.color = new Color(0.88f, 0.84f, 0.72f, 0.95f);
            _statusText.text = "";
            _panel.SetActive(false);
        }

        private Button CreateButton(string name, Vector2 position, string label, out Text labelText)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(_panel.transform, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(92f, 46f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.17f, 0.15f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.17f, 0.15f, 0.95f);
            colors.highlightedColor = new Color(0.31f, 0.27f, 0.20f, 1f);
            colors.pressedColor = new Color(0.11f, 0.10f, 0.09f, 1f);
            colors.disabledColor = new Color(0.10f, 0.095f, 0.09f, 0.38f);
            button.colors = colors;

            labelText = CreateText("Text", Vector2.zero, new Vector2(90f, 42f), 12);
            labelText.transform.SetParent(buttonObject.transform, false);
            labelText.text = label;
            labelText.color = new Color(0.96f, 0.93f, 0.84f, 1f);

            return button;
        }

        private Text CreateText(string name, Vector2 position, Vector2 size, int fontSize)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(_panel.transform, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Font CreateFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Arial" }, 16);
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private Transform GetEscPanelParent()
        {
            if (GameApp.Instance == null ||
                GameApp.Instance.GetCurrentGameStateName() != EGameState.InGame ||
                UIManager.Instance == null ||
                !UIElement.SystemOption.Exist ||
                UIElement.SystemOption.UiBase == null ||
                !UIManager.Instance.IsFocusElement(UIElement.SystemOption) ||
                UIElement.Dialog.Exist)
            {
                return null;
            }

            return UIElement.SystemOption.UiBase.transform;
        }

        private void AttachToEscPanel(Transform panelParent)
        {
            if (_panel == null || panelParent == null)
            {
                return;
            }

            if (_panel.transform.parent != panelParent)
            {
                _panel.transform.SetParent(panelParent, false);
                _currentPanelParent = panelParent;
            }

            ApplyEscPanelLayout(panelParent as RectTransform);
            _panel.transform.SetAsLastSibling();
        }

        private void DetachFromEscPanel()
        {
            if (_panel == null || _panel.transform.parent == transform)
            {
                _currentPanelParent = null;
                return;
            }

            _panel.transform.SetParent(transform, false);
            _currentPanelParent = null;
        }

        private void ApplyEscPanelLayout(RectTransform parentRect)
        {
            if (_panelRect == null)
            {
                return;
            }

            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(300f, 260f);
            _panelRect.localScale = Vector3.one;

            float x = 580f;
            if (parentRect != null && parentRect.rect.width > 0f)
            {
                x = Mathf.Min(x, parentRect.rect.width * 0.5f - 208f);
            }

            _panelRect.anchoredPosition = new Vector2(Mathf.Max(280f, x), -34f);
        }

        private void ResetConfirm()
        {
            _confirmUntil = 0f;
            _confirmSlot = -1;
            _pendingConfirmAction = PendingConfirmAction.None;
            if (_saveText != null)
            {
                _saveText.text = "快速存档";
            }

            if (_loadText != null)
            {
                _loadText.text = "快速读档";
            }

            if (_savePointText != null)
            {
                _savePointText.text = "存档";
            }

            if (_loadPointText != null)
            {
                _loadPointText.text = "读档";
            }
        }

        private static bool CanOperate(out string reason, out sbyte slot)
        {
            reason = "";
            slot = -1;

            if (GameApp.Instance == null || GameApp.Instance.GetCurrentGameStateName() != EGameState.InGame)
            {
                reason = "未进游戏";
                return false;
            }

            if (!GlobalOperations.LoadedAllArchiveData)
            {
                reason = "读取中";
                return false;
            }

            if (!TryGetCurrentSlot(out slot))
            {
                reason = "无档位";
                return false;
            }

            if (!SingletonObject.IsCreatedInstance<BasicGameData>())
            {
                reason = "数据未就绪";
                return false;
            }

            BasicGameData data = SingletonObject.getInstance<BasicGameData>();
            if (data.SavingWorld)
            {
                reason = "保存中";
                return false;
            }

            if (GameApp.AdvancingMonth || data.AdvancingMonthState != 0)
            {
                reason = "月结中";
                return false;
            }

            if (UIElement.Loading.Exist)
            {
                reason = "载入中";
                return false;
            }

            if (UIElement.Combat.Exist || UIElement.CombatBegin.Exist)
            {
                reason = "战斗中";
                return false;
            }

            if (UIElement.EventWindow.Exist)
            {
                reason = "事件中";
                return false;
            }

            if (SingletonObject.IsCreatedInstance<EventModel>() &&
                SingletonObject.getInstance<EventModel>().HasListeningEvent)
            {
                reason = "事件中";
                return false;
            }

            if (SingletonObject.IsCreatedInstance<DisplayTriggerModel>() &&
                SingletonObject.getInstance<DisplayTriggerModel>().HandlingMonthlyEventBlock)
            {
                reason = "月结中";
                return false;
            }

            if (SingletonObject.IsCreatedInstance<CommandManager>() && CommandManager.IsRunning)
            {
                reason = "指令中";
                return false;
            }

            if (UIManager.Instance != null && UIManager.Instance.BlockHotKey)
            {
                reason = "界面中";
                return false;
            }

            return true;
        }

        private static bool TryGetCurrentSlot(out sbyte slot)
        {
            slot = -1;
            if (!SingletonObject.IsCreatedInstance<GlobalSettings>())
            {
                return false;
            }

            slot = SingletonObject.getInstance<GlobalSettings>().LastEnterWorldIndex;
            return slot >= 0 && slot < GlobalOperations.ArchiveSlotsCount;
        }

        private void QuickSave()
        {
            if (!CanOperate(out string reason, out sbyte slot))
            {
                SetStatus(reason, 2f);
                return;
            }

            if (HasActiveConfirm(PendingConfirmAction.QuickSave, slot))
            {
                _actionCooldownUntil = Time.unscaledTime + ActionCooldownSeconds;
                BeginPendingSave(false, slot);
                GlobalOperations.SaveWorld();
                SetStatus("快速存档中 " + (slot + 1), 3f);
                ResetConfirm();
                return;
            }

            if (!TryBuildArchiveSummary(slot, "将覆盖档位 " + (slot + 1), out string summary))
            {
                RequestArchiveInfoRefresh();
                SetArchiveText("档位 " + (slot + 1) + "\n存档信息读取中");
                SetStatus("请稍候", 2f);
                return;
            }

            SetConfirm(PendingConfirmAction.QuickSave, slot, _saveText, "确认快速存档", summary, "再点快速存档");
        }

        private void QuickLoad()
        {
            if (!CanOperate(out string reason, out sbyte slot))
            {
                SetStatus(reason, 2f);
                return;
            }

            if (HasActiveConfirm(PendingConfirmAction.QuickLoad, slot))
            {
                _actionCooldownUntil = Time.unscaledTime + ActionCooldownSeconds;
                SetStatus("快速读档中 " + (slot + 1), 2f);
                HideSystemOption();
                GameApp.LoadArchive(slot);
                ResetConfirm();
                return;
            }

            if (!TryBuildArchiveSummary(slot, "将读取档位 " + (slot + 1), out string summary))
            {
                RequestArchiveInfoRefresh();
                SetArchiveText("档位 " + (slot + 1) + "\n存档信息读取中");
                SetStatus("请稍候", 2f);
                return;
            }

            SetConfirm(PendingConfirmAction.QuickLoad, slot, _loadText, "确认快速读档", summary, "再点快速读档");
        }

        private void SavePoint()
        {
            if (!CanOperate(out string reason, out sbyte slot))
            {
                SetStatus(reason, 2f);
                return;
            }

            if (HasActiveConfirm(PendingConfirmAction.SavePoint, slot))
            {
                _actionCooldownUntil = Time.unscaledTime + ActionCooldownSeconds;
                BeginPendingSave(true, slot);
                ModDomainMethod.Call.CallModMethod(ModId, SaveWorldWithBackupMethod);
                SetStatus("存档中 " + (slot + 1), 3f);
                ResetConfirm();
                return;
            }

            RequestManualSaves(slot, OnManualSavesForSaveConfirmReady);
            SetArchiveText("档位 " + (slot + 1) + "\n手动存档信息读取中");
            SetStatus("请稍候", 2f);
        }

        private void OpenLoadPoint()
        {
            if (!CanOperate(out string reason, out sbyte slot))
            {
                SetStatus(reason, 2f);
                return;
            }

            ResetConfirm();
            RequestManualSaves(slot, OnManualSavesForLoadReady);
            SetArchiveText("档位 " + (slot + 1) + "\n手动存档信息读取中");
            SetStatus("请稍候", 2f);
        }

        private void RequestManualSaves(sbyte slot, Action<sbyte, SerializableModData> onReady)
        {
            SerializableModData parameter = new SerializableModData();
            parameter.Set(SlotKey, (int)slot);

            try
            {
                ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(
                    null,
                    ModId,
                    ListManualSavesMethod,
                    parameter,
                    (offset, pool) =>
                    {
                        SerializableModData result = null;
                        try
                        {
                            SerializerHolder<SerializableModData>.Deserialize(pool, offset, ref result);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[EasyQuickSaveLoad] Failed to deserialize manual saves: " + ex);
                        }

                        onReady?.Invoke(slot, result);
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to request manual saves: " + ex);
                onReady?.Invoke(slot, null);
            }
        }

        private void OnManualSavesForSaveConfirmReady(sbyte slot, SerializableModData result)
        {
            if (!TryGetManualSavesResult(result, out ArchiveInfo archiveInfo, out int count, out int capacity))
            {
                SetArchiveText("档位 " + (slot + 1) + "\n手动存档信息读取失败");
                SetStatus("稍后再试", 2f);
                return;
            }

            RememberManualSaveCount(slot, count, capacity);
            string summary = BuildManualSaveConfirmSummary(slot, archiveInfo, count, capacity);
            SetConfirm(PendingConfirmAction.SavePoint, slot, _savePointText, "确认存档", summary, "再点存档");
        }

        private void OnManualSavesForLoadReady(sbyte slot, SerializableModData result)
        {
            if (!TryGetManualSavesResult(result, out ArchiveInfo archiveInfo, out int count, out int capacity))
            {
                SetArchiveText("档位 " + (slot + 1) + "\n手动存档信息读取失败");
                SetStatus("稍后再试", 2f);
                return;
            }

            RememberManualSaveCount(slot, count, capacity);
            if (archiveInfo.BackupWorldsInfo == null || archiveInfo.BackupWorldsInfo.Count <= 0)
            {
                SetArchiveText("档位 " + (slot + 1) + "\n暂无手动存档");
                SetStatus("先存档", 2.5f);
                return;
            }

            ArgumentBox args = EasyPool.Get<ArgumentBox>();
            args.SetObject("ArchiveData", archiveInfo);
            args.Set("ArchiveIndex", slot);
            args.SetObject("OnConfirmEnter", new Action<long>(timestamp => LoadManualWorld(slot, timestamp)));

            UIElement.RevertArchive.SetOnInitArgs(args);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.MaskUI(UIElement.RevertArchive);
                StartCoroutine(ApplyManualRevertArchiveLabelNextFrame());
            }
        }

        private void RefreshManualSaveCount(sbyte slot, bool force = true)
        {
            if (slot < 0)
            {
                return;
            }

            if (!force && Time.unscaledTime < _nextManualSaveInfoRefreshTime)
            {
                return;
            }

            _nextManualSaveInfoRefreshTime = Time.unscaledTime + ArchiveInfoRefreshCooldownSeconds;
            RequestManualSaves(slot, OnManualSaveCountReady);
        }

        private void OnManualSaveCountReady(sbyte slot, SerializableModData result)
        {
            if (TryGetManualSavesResult(result, out ArchiveInfo archiveInfo, out int count, out int capacity))
            {
                RememberManualSaveCount(slot, count, capacity);
            }
        }

        private void RememberManualSaveCount(sbyte slot, int count, int capacity)
        {
            _manualSaveCounts[slot] = count;
            _manualSaveCapacities[slot] = capacity;
        }

        private static bool TryGetManualSavesResult(
            SerializableModData result,
            out ArchiveInfo archiveInfo,
            out int count,
            out int capacity)
        {
            archiveInfo = null;
            count = 0;
            capacity = 0;

            return result != null &&
                result.Get(OkKey, out bool ok) &&
                ok &&
                result.Get(CountKey, out count) &&
                result.Get(CapacityKey, out capacity) &&
                result.Get(ArchiveInfoKey, out archiveInfo) &&
                archiveInfo != null &&
                archiveInfo.BackupWorldsInfo != null;
        }

        private static string BuildManualSaveConfirmSummary(
            sbyte slot,
            ArchiveInfo archiveInfo,
            int count,
            int capacity)
        {
            string summary = "将创建手动存档 " + (slot + 1);
            if (archiveInfo.WorldInfo != null)
            {
                summary += "\n" + FormatWorldInfoBrief(archiveInfo.WorldInfo);
            }

            summary += "\n手动存档 " + count;
            if (capacity > 0)
            {
                summary += "/" + capacity;
            }

            if (capacity > 0 && count >= capacity && TryGetOldestManualSave(archiveInfo, out WorldInfo overwrittenWorldInfo))
            {
                summary += "\n将覆盖最旧\n" + FormatWorldInfoBrief(overwrittenWorldInfo);
            }

            return summary;
        }

        private static bool TryGetOldestManualSave(ArchiveInfo archiveInfo, out WorldInfo worldInfo)
        {
            worldInfo = null;
            if (archiveInfo.BackupWorldsInfo == null || archiveInfo.BackupWorldsInfo.Count <= 0)
            {
                return false;
            }

            long oldestTimestamp = long.MaxValue;
            foreach ((long timestamp, WorldInfo manualWorldInfo) in archiveInfo.BackupWorldsInfo)
            {
                if (manualWorldInfo != null && timestamp < oldestTimestamp)
                {
                    oldestTimestamp = timestamp;
                    worldInfo = manualWorldInfo;
                }
            }

            return worldInfo != null;
        }

        private void BeginPendingSave(bool savePoint, sbyte slot)
        {
            _actionCooldownUntil = Time.unscaledTime + ActionCooldownSeconds;
            _pendingSave = true;
            _pendingSavePoint = savePoint;
            _pendingSaveSlot = slot;
            _sawSavingWorld = false;
        }

        private void UpdatePendingSaveState()
        {
            if (!_pendingSave || !SingletonObject.IsCreatedInstance<BasicGameData>())
            {
                return;
            }

            bool saving = SingletonObject.getInstance<BasicGameData>().SavingWorld;
            if (saving)
            {
                _sawSavingWorld = true;
                return;
            }

            if (_sawSavingWorld)
            {
                bool savePoint = _pendingSavePoint;
                sbyte slot = _pendingSaveSlot;
                _pendingSave = false;
                if (savePoint)
                {
                    RefreshManualSaveCount(slot);
                }
                else
                {
                    RequestArchiveInfoRefresh(true);
                }

                SetStatus(savePoint ? "存档完成" : "快速存档完成", 2.5f);
                _pendingSavePoint = false;
                _pendingSaveSlot = -1;
            }
            else if (Time.unscaledTime > _actionCooldownUntil + 2.5f)
            {
                bool savePoint = _pendingSavePoint;
                sbyte slot = _pendingSaveSlot;
                _pendingSave = false;
                if (savePoint)
                {
                    RefreshManualSaveCount(slot);
                }
                else
                {
                    RequestArchiveInfoRefresh(true);
                }

                SetStatus(savePoint ? "存档已发送" : "快速存档已发送", 2.5f);
                _pendingSavePoint = false;
                _pendingSaveSlot = -1;
            }
        }

        private void UpdateScheduledArchiveInfoRefresh()
        {
            if (_forceArchiveInfoRefreshAt <= 0f || Time.unscaledTime < _forceArchiveInfoRefreshAt)
            {
                return;
            }

            _forceArchiveInfoRefreshAt = 0f;
            RequestArchiveInfoRefresh(true);
        }

        private void UpdateArchiveInfoText(bool canOperate, sbyte slot)
        {
            if (!canOperate || _archiveText == null)
            {
                return;
            }

            if (HasAnyActiveConfirm(slot))
            {
                return;
            }

            if (TryBuildArchiveSummary(slot, "当前档位 " + (slot + 1), out string summary))
            {
                if (_manualSaveCounts.TryGetValue(slot, out int manualCount))
                {
                    summary += "\n手动存档 " + manualCount;
                    if (_manualSaveCapacities.TryGetValue(slot, out int capacity) && capacity > 0)
                    {
                        summary += "/" + capacity;
                    }
                }
                else
                {
                    RefreshManualSaveCount(slot, false);
                }

                SetArchiveText(summary);
                return;
            }

            RequestArchiveInfoRefresh();
            SetArchiveText("档位 " + (slot + 1) + "\n存档信息读取中");
        }

        private void UpdateStatus(bool canOperate, bool coolingDown, string reason, sbyte slot)
        {
            if (Time.unscaledTime < _statusUntil)
            {
                _statusText.text = _statusMessage;
                return;
            }

            if (coolingDown)
            {
                _statusText.text = "稍候";
            }
            else if (!canOperate)
            {
                _statusText.text = reason;
            }
            else
            {
                _statusText.text = _manualSaveCounts.TryGetValue(slot, out int manualCount) && manualCount > 0
                    ? "档位 " + (slot + 1) + " / 手动存档 " + manualCount
                    : "档位 " + (slot + 1);
            }
        }

        private void SetStatus(string message, float seconds)
        {
            _statusMessage = message;
            _statusUntil = Time.unscaledTime + seconds;
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void SetArchiveText(string message)
        {
            if (_archiveText != null)
            {
                _archiveText.text = message;
            }
        }

        private bool HasActiveConfirm(PendingConfirmAction action, sbyte slot)
        {
            return Time.unscaledTime < _confirmUntil && _confirmSlot == slot && _pendingConfirmAction == action;
        }

        private bool HasAnyActiveConfirm(sbyte slot)
        {
            return Time.unscaledTime < _confirmUntil && _confirmSlot == slot &&
                _pendingConfirmAction != PendingConfirmAction.None;
        }

        private void SetConfirm(
            PendingConfirmAction action,
            sbyte slot,
            Text buttonText,
            string buttonLabel,
            string summary,
            string status)
        {
            ResetConfirm();
            _confirmUntil = Time.unscaledTime + ConfirmSeconds;
            _confirmSlot = slot;
            _pendingConfirmAction = action;
            if (buttonText != null)
            {
                buttonText.text = buttonLabel;
            }

            SetArchiveText(summary);
            SetStatus(status, ConfirmSeconds);
        }

        private static bool TryBuildArchiveSummary(sbyte slot, string title, out string summary)
        {
            summary = "";

            if (!TryGetArchiveInfo(slot, out ArchiveInfo archiveInfo))
            {
                return false;
            }

            WorldInfo worldInfo = archiveInfo.WorldInfo;
            summary = title + "\n" +
                FormatSaveTime(worldInfo.SavingTimestamp) + "\n" +
                FormatTaiwuName(worldInfo) + "  " + FormatYear(worldInfo) + "\n" +
                FormatLocation(worldInfo);
            return true;
        }

        private static bool TryGetArchiveInfo(sbyte slot, out ArchiveInfo archiveInfo)
        {
            archiveInfo = null;
            ArchiveInfo[] archivesInfo = GlobalOperations.ArchivesInfo;
            if (archivesInfo == null || slot < 0 || slot >= archivesInfo.Length)
            {
                return false;
            }

            archiveInfo = archivesInfo[slot];
            if (archiveInfo == null || archiveInfo.Status != ArchiveStatus.Good || archiveInfo.WorldInfo == null)
            {
                archiveInfo = null;
                return false;
            }

            return true;
        }

        private static void LoadManualWorld(sbyte slot, long manualTimestamp)
        {
            if (GameApp.Instance == null)
            {
                return;
            }

            HideSystemOption();
            GameApp.Instance.ChangeGameState(
                EGameState.Loading,
                EasyPool.Get<ArgumentBox>()
                    .SetObject("OnLoadingFinish", new Action(OnEnterWorldLoadFinish))
                    .SetObject("OnLoadingStart", new Action(OnEnterWorldLoadStart)));

            static void OnEnterWorldLoadFinish()
            {
                PoolManager.CleanPool();
                UIElement worldMap = UIElement.WorldMap;
                worldMap.OnShowed = (Action)Delegate.Combine(worldMap.OnShowed, (Action)delegate
                {
                    ArgumentBox argumentBox = EasyPool.Get<ArgumentBox>();
                    argumentBox.Set("AnimToShowMask", false);
                    argumentBox.Set("AnimTime", 4.3f);
                    argumentBox.Set("HideAfterShow", true);
                    UIElement.BlackMask.SetOnInitArgs(argumentBox);
                    UIElement.BlackMask.Show();
                });
                GameApp.Instance.ChangeGameState(EGameState.InGame);
            }

            void OnEnterWorldLoadStart()
            {
                UIManager.Instance.HideAll();
                SingletonObject.RemoveInstance<CharacterMonitorModel>();
                GlobalOperations.PackCrossArchiveGameData();
                GlobalOperations.LeaveWorld();
                GlobalOperations.OnLeaveWorld();
                UIManager.Instance.DestroyAll(new List<UIElement>
                {
                    UIElement.Loading,
                    UIElement.MainMenu,
                    UIElement.Combat,
                    UIElement.PermanentTips
                });
                GameApp.ResetGameSubPageState();
                SingletonObject.ClearInstances();
                GEvent.OnEvent(EEvents.LoadingProgress, EasyPool.Get<ArgumentBox>().Set("Progress", 50));
                CallLoadManualSave(slot, manualTimestamp);
            }
        }

        private static void CallLoadManualSave(sbyte slot, long manualTimestamp)
        {
            GlobalOperations.LoadedAllArchiveData = false;

            SerializableModData parameter = new SerializableModData();
            parameter.Set(SlotKey, (int)slot);
            parameter.Set(TimestampKey, manualTimestamp.ToString());
            ModDomainMethod.Call.CallModMethodWithParam(ModId, LoadManualSaveMethod, parameter);
            AddLoadedAllArchiveDataMonitor();
        }

        private static void AddLoadedAllArchiveDataMonitor()
        {
            try
            {
                FieldInfo listenerField = typeof(GlobalOperations).GetField(
                    "_listenerId",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (listenerField == null)
                {
                    return;
                }

                int listenerId = (int)listenerField.GetValue(null);
                if (listenerId >= 0)
                {
                    GameDataBridge.AddDataMonitor(listenerId, 0, 1, ulong.MaxValue);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to monitor manual load state: " + ex);
            }
        }

        private IEnumerator ApplyManualRevertArchiveLabelNextFrame()
        {
            yield return null;
            ApplyManualRevertArchiveLabel();
        }

        private static void ApplyManualRevertArchiveLabel()
        {
            try
            {
                if (!UIElement.RevertArchive.Exist || UIElement.RevertArchive.UiBase == null)
                {
                    return;
                }

                object uiBase = UIElement.RevertArchive.UiBase;
                FieldInfo titleField = uiBase.GetType().GetField(
                    "revertTitle",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object title = titleField?.GetValue(uiBase);
                PropertyInfo textProperty = title?.GetType().GetProperty("text");
                textProperty?.SetValue(title, "EasyQuickSaveLoad 手动读档", null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to label manual load UI: " + ex);
            }
        }

        private static string FormatWorldInfoBrief(WorldInfo worldInfo)
        {
            return FormatSaveTime(worldInfo.SavingTimestamp) + "\n" +
                FormatTaiwuName(worldInfo) + "  " + FormatYear(worldInfo) + "\n" +
                FormatLocation(worldInfo);
        }

        private static string FormatSaveTime(long savingTimestamp)
        {
            if (savingTimestamp <= 0L)
            {
                return "保存时间未知";
            }

            try
            {
                long ticks = savingTimestamp > DateTime.MaxValue.Ticks ? DateTime.MaxValue.Ticks : savingTimestamp;
                return DateTime.MinValue.AddTicks(ticks).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "保存时间未知";
            }
        }

        private static string FormatTaiwuName(WorldInfo worldInfo)
        {
            string givenName = worldInfo.TaiwuGivenName ?? "";
            string surname = worldInfo.TaiwuSurname ?? "";
            try
            {
                if (SingletonObject.IsCreatedInstance<GlobalSettings>() &&
                    SingletonObject.getInstance<GlobalSettings>().HideTaiwuOriginalSurname &&
                    WorldFunctionType.Get(worldInfo.WorldFunctionStatuses, 26))
                {
                    return NameCenter.FormatName(LocalStringManager.Get(LanguageKey.LK_Taiwu), givenName);
                }

                return NameCenter.FormatName(surname, givenName);
            }
            catch
            {
                // Fall back to the raw archive name if localized settings are unavailable.
            }

            string name = surname + givenName;
            return string.IsNullOrEmpty(name) ? "未知太吾" : name;
        }

        private static string FormatYear(WorldInfo worldInfo)
        {
            int year = worldInfo.CurrDate / 12 + 1;
            try
            {
                return LocalStringManager.GetFormat(LanguageKey.UI_RecordSelect_Year, year, worldInfo.TaiwuGenerationsCount);
            }
            catch
            {
                return "第" + year + "年";
            }
        }

        private static string FormatLocation(WorldInfo worldInfo)
        {
            string stateName = "";
            string areaName = "";

            try
            {
                stateName = MapStateConfig.Instance[worldInfo.MapStateTemplateId].Name;
            }
            catch
            {
#pragma warning disable CS0612
                stateName = worldInfo.MapStateName ?? "";
#pragma warning restore CS0612
            }

            try
            {
                areaName = MapAreaConfig.Instance[worldInfo.MapAreaTemplateId].Name;
            }
            catch
            {
#pragma warning disable CS0612
                areaName = worldInfo.MapAreaName ?? "";
#pragma warning restore CS0612
            }

            if (string.IsNullOrEmpty(stateName) && string.IsNullOrEmpty(areaName))
            {
                return "未知地点";
            }

            if (string.IsNullOrEmpty(stateName))
            {
                return areaName;
            }

            if (string.IsNullOrEmpty(areaName))
            {
                return stateName;
            }

            return stateName + " / " + areaName;
        }

        private void RequestArchiveInfoRefresh(bool force = false)
        {
            if (!force && Time.unscaledTime < _nextArchiveInfoRefreshTime)
            {
                return;
            }

            _nextArchiveInfoRefreshTime = Time.unscaledTime + ArchiveInfoRefreshCooldownSeconds;
            try
            {
                GlobalOperations.GetArchivesInfo();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to refresh archive info: " + ex);
            }
        }

        private static void HideSystemOption()
        {
            if (UIManager.Instance != null && UIElement.SystemOption.Exist)
            {
                UIManager.Instance.HideUI(UIElement.SystemOption);
            }
        }
    }
}
