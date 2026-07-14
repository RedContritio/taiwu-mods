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

        // ---- Formatters (WorldInfo → localized display strings) ----

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

        /// <summary>存档位角落用的短时间：MM-dd HH:mm（去年份/秒），避免在窄格里过长。</summary>
        public static string FormatSaveTimeShort(long savingTimestamp)
        {
            if (savingTimestamp <= 0L) return "";
            try
            {
                long ticks = savingTimestamp > DateTime.MaxValue.Ticks ? DateTime.MaxValue.Ticks : savingTimestamp;
                return DateTime.MinValue.AddTicks(ticks).ToLocalTime().ToString("MM-dd HH:mm");
            }
            catch
            {
                return "";
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

        /// <summary>游戏内年月：CurrDate 为从 0 起的月计数 → 年=/12+1、月=%12+1。用于存档位副标题。</summary>
        public static string FormatYearMonth(WorldInfo worldInfo)
        {
            int year = worldInfo.CurrDate / 12 + 1;
            int month = worldInfo.CurrDate % 12 + 1;
            return "第" + year + "年" + month + "月";
        }

        /// <summary>游戏内原生"年月"显示：直接调用 TimeManager.GetDateDisplayContent(date)，与游戏顶栏/过月
        /// 提示完全一致(第X年 Y月；月为 1-12 数字，无季节/无世代/无专属配色)。取不到则回退到手算。
        /// 存档位副标题/无备注默认标题/改名预填都用它——同一世界内太吾名冗余，故只显示年月。</summary>
        public static string FormatGameDate(WorldInfo worldInfo)
        {
            try
            {
                var tm = SingletonObject.getInstance<TimeManager>();
                if (tm != null)
                {
                    string s = tm.GetDateDisplayContent(worldInfo.CurrDate);
                    if (!string.IsNullOrEmpty(s)) return s.Trim();
                }
            }
            catch { /* 取不到 TimeManager → 回退手算 */ }
            return FormatYearMonth(worldInfo);
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

        /// <summary>州名(如"云南")；取不到回退旧 MapStateName。用于列表式存档位 SetText 的 location 参数。</summary>
        public static string FormatState(WorldInfo worldInfo)
        {
            try { return MapStateConfig.Instance[worldInfo.MapStateTemplateId].Name ?? ""; }
            catch
            {
#pragma warning disable CS0612
                return worldInfo.MapStateName ?? "";
#pragma warning restore CS0612
            }
        }

        /// <summary>地区名(如"怒江")；取不到回退旧 MapAreaName。用于列表式存档位 SetText 的 location2 参数。</summary>
        public static string FormatArea(WorldInfo worldInfo)
        {
            try { return MapAreaConfig.Instance[worldInfo.MapAreaTemplateId].Name ?? ""; }
            catch
            {
#pragma warning disable CS0612
                return worldInfo.MapAreaName ?? "";
#pragma warning restore CS0612
            }
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

        /// <summary>Sets (or clears, when null/empty) the 备注 note for a slot.</summary>
        public static void CallSetNote(int slot, string note)
        {
            var p = new SerializableModData();
            p.Set("Slot", slot);
            p.Set("Note", note ?? "");
            ModDomainMethod.Call.CallModMethodWithParam(ModId, "SetSlotNote", p);
        }

        /// <summary>Parses the ListSlots "SlotNotes" blob (lines of <c>slot=base64(utf8)</c>) into a map.</summary>
        public static System.Collections.Generic.Dictionary<int, string> ParseNotes(SerializableModData res)
        {
            var map = new System.Collections.Generic.Dictionary<int, string>();
            if (res == null || !res.Get("SlotNotes", out string blob) || string.IsNullOrEmpty(blob)) return map;
            foreach (var line in blob.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (!int.TryParse(line.Substring(0, eq), out int slot)) continue;
                try
                {
                    string note = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(eq + 1)));
                    if (!string.IsNullOrEmpty(note)) map[slot] = note;
                }
                catch { }
            }
            return map;
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
                // 本次读档的 teardown(DestroyAll)会销毁原生 Dialog 的 UiBase 但状态机没回 Sleep(僵尸)。
                // 若不复位，之后游戏自己的退出确认框(GameApp.GameQuitConfirm)会撞上僵尸 Dialog → InitElement 空引用报红
                // (在存档页读档后关闭游戏就会触发)。这里复位到 Sleep，让后续任何 Show 重建 UiBase。
                NativeDialog.ResetDialogIfZombie();
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
