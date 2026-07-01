using System;
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
using MapAreaConfig = Config.MapArea;
using MapStateConfig = Config.MapState;

namespace EasyQuickSaveLoad.Frontend
{
    internal static class EqslCore
    {
        // The runtime mod id used to dispatch backend mod methods. Injected by FrontendPlugin via SetModId.
        public static string ModId = "EasyQuickSaveLoad";

        public static void SetModId(string modId)
        {
            if (!string.IsNullOrEmpty(modId))
            {
                ModId = modId;
            }
        }

        // ---- CanOperate ----

        public static bool CanOperate(out string reason)
        {
            reason = "";

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

            if (!TryGetCurrentSlot(out sbyte slot))
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

        // ---- Formatters (copied verbatim from EasyQuickSaveLoadOverlay) ----

        public static string FormatWorldInfoBrief(WorldInfo worldInfo)
        {
            return FormatSaveTime(worldInfo.SavingTimestamp) + "\n" +
                FormatTaiwuName(worldInfo) + "  " + FormatYear(worldInfo) + "\n" +
                FormatLocation(worldInfo);
        }

        public static string FormatSaveTime(long savingTimestamp)
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

        public static string FormatTaiwuName(WorldInfo worldInfo)
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

        public static string FormatYear(WorldInfo worldInfo)
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

        public static string FormatLocation(WorldInfo worldInfo)
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

        // ---- Backend dispatch helpers ----

        public static void CallSave(int slot)
        {
            var p = new SerializableModData();
            p.Set("Slot", slot);
            ModDomainMethod.Call.CallModMethodWithParam(ModId, "SaveToSlot", p);
        }

        public static void CallDelete(int slot)
        {
            var p = new SerializableModData();
            p.Set("Slot", slot);
            ModDomainMethod.Call.CallModMethodWithParam(ModId, "DeleteSlot", p);
        }

        public static void RequestSlots(Action<SerializableModData> onReady)
        {
            SerializableModData parameter = new SerializableModData();

            try
            {
                ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(
                    null,
                    ModId,
                    "ListSlots",
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
                            Debug.LogWarning("[EasyQuickSaveLoad] Failed to deserialize ListSlots result: " + ex);
                        }

                        onReady?.Invoke(result);
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to request slots: " + ex);
                onReady?.Invoke(null);
            }
        }

        // ---- LoadSlotWorld (migrated from LoadManualWorld; slot int, no timestamp) ----

        public static void LoadSlotWorld(int slot)
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
                    .SetObject("OnLoadingStart", new Action(() => OnEnterWorldLoadStart(slot))));

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
        }

        private static void OnEnterWorldLoadStart(int slot)
        {
            UIManager.Instance.HideAll();
            SingletonObject.RemoveInstance<CharacterMonitorModel>();
            // PackCrossArchiveGameData + LeaveWorld are performed inside the backend LoadFromSlot mod
            // method, not here: those are domain-method calls (global listener) while the load is a
            // mod-method call (listener -1), and the two streams are not ordered, so leaving here would
            // race the load and trip LoadWorldAt's IsInWorld guard. OnLeaveWorld is frontend-only bookkeeping
            // and stays here (native runs it before the backend leave is applied too).
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
            CallLoadFromSlot(slot);
        }

        private static void CallLoadFromSlot(int slot)
        {
            GlobalOperations.LoadedAllArchiveData = false;

            SerializableModData parameter = new SerializableModData();
            parameter.Set("Slot", slot);
            ModDomainMethod.Call.CallModMethodWithParam(ModId, "LoadFromSlot", parameter);
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
                Debug.LogWarning("[EasyQuickSaveLoad] Failed to monitor slot load state: " + ex);
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
