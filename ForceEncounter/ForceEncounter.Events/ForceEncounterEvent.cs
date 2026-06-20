using System;
using System.Collections.Generic;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterEvent : TaiwuEventItem
    {
        private static readonly HashSet<string> LoggedDebugStates = new HashSet<string>();

        public ForceEncounterEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.外层入口);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = string.Empty;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.打开入口.Key,
                    OptionGuid = ForceEncounterEventIds.选项.打开入口.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionVisibleCheck = IsVisible,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = StayOnEntryEvent
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.情难自已.Key,
                    OptionGuid = ForceEncounterEventIds.选项.情难自已.Guid,
                    Behavior = EventOptionBehavior.BehaviorEgoistic,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildPreviewCosts(string.Empty),
                    OnOptionVisibleCheck = IsVisible,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = Execute
                }
            };
            EventOptions[0].SetContent(string.Empty);
            EventOptions[1].SetContent("（情难自已……）");
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            EventOptions[1].OptionConsumeInfos = ForceEncounterEventCosts.BuildPreviewCosts(GetRuntimeModId());
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }

        private bool IsEnabled()
        {
            string modId = GetRuntimeModId();
            bool enabled = ForceEncounterSettings.GetBool(modId, ForceEncounterConstants.Settings.Enabled, true);
            DebugLogOnce(
                "enabled:" + modId + ":" + enabled,
                "IsEnabled packageSet=" + (Package != null) +
                ", modId='" + modId +
                ", enabled=" + enabled);
            return enabled;
        }

        private bool CanExecute()
        {
            string reason = GetCanExecuteFailureReason(out int actorId, out int targetId);
            bool canExecute = reason == ForceEncounterConstants.Reasons.Ok;
            DebugLogOnce(
                "can:" + actorId + ":" + targetId + ":" + reason,
                "CanExecute actor=" + actorId +
                ", target=" + targetId +
                ", result=" + canExecute +
                ", reason=" + reason);
            return canExecute;
        }

        private bool IsVisible()
        {
            return IsEnabled() && IsSpecialAgeVisible();
        }

        private bool IsSpecialAgeVisible()
        {
            string modId = GetRuntimeModId();
            bool allowSpecialAge = ForceEncounterSettings.GetBool(modId, ForceEncounterConstants.Settings.AllowSpecialAge, true);

            if (allowSpecialAge)
            {
                return true;
            }

            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return true;
            }

            int actorId = ForceEncounterEventRuntime.GetActorId();
            return !ForceEncounterEventRuntime.RequiresSpecialAgeNotice(actorId, targetId);
        }

        private string GetCanExecuteFailureReason(out int actorId, out int targetId)
        {
            actorId = -1;
            targetId = -1;

            if (!IsEnabled())
            {
                return ForceEncounterConstants.Reasons.Disabled;
            }

            if (ArgBox == null)
            {
                return ForceEncounterConstants.Reasons.NoArgBox;
            }

            if (!ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return ForceEncounterConstants.Reasons.MissingCharacterId;
            }

            actorId = ForceEncounterEventRuntime.GetActorId();
            if (actorId == targetId)
            {
                return ForceEncounterConstants.Reasons.SameActorAndTarget;
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor))
            {
                return ForceEncounterConstants.Reasons.ActorNotFound;
            }

            if (!DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return ForceEncounterConstants.Reasons.TargetNotFound;
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId))
            {
                return ForceEncounterConstants.Reasons.ActorNotAlive;
            }

            if (!DomainManager.Character.IsCharacterAlive(targetId))
            {
                return ForceEncounterConstants.Reasons.TargetNotAlive;
            }

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.BabyAgeGroup)
            {
                return ForceEncounterConstants.Reasons.ActorBaby;
            }

            if (target.GetAgeGroup() == ForceEncounterConstants.Gameplay.BabyAgeGroup)
            {
                return ForceEncounterConstants.Reasons.TargetBaby;
            }

            string modId = GetRuntimeModId();
            bool allowSpecialAge = ForceEncounterSettings.GetBool(modId, ForceEncounterConstants.Settings.AllowSpecialAge, true);

            if (!allowSpecialAge &&
                (actor.GetAgeGroup() != ForceEncounterConstants.Gameplay.AdultAgeGroup ||
                 target.GetAgeGroup() != ForceEncounterConstants.Gameplay.AdultAgeGroup))
            {
                return ForceEncounterConstants.Reasons.SpecialAgeNotAllowed;
            }

            return ForceEncounterConstants.Reasons.Ok;
        }

        private string Execute()
        {
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return string.Empty;
            }

            int actorId = ForceEncounterEventRuntime.GetActorId();
            ForceEncounterEventRuntime.StoreInteractionArgs(ArgBox, actorId, targetId);
            ArgBox.Set(EventArgBox.OptionWaitConfirmKey, ForceEncounterEventIds.等待确认.外层预览);

            return ProbeAndRouteToChoice(actorId, targetId);
        }

        private string ProbeAndRouteToChoice(int actorId, int targetId)
        {
            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.探测);

            if (result != null &&
                result.Get(ForceEncounterEventIds.后端.结算结果, out int resolution) &&
                (resolution == ForceEncounterEventIds.探测结果.亲密通过 ||
                 resolution == ForceEncounterEventIds.探测结果.需要战斗选择))
            {
                ArgBox.Set(ForceEncounterEventIds.后端.结算结果, resolution);
                return ForceEncounterEventIds.事件.内层选择;
            }

            LogProbeFailure(result);
            return string.Empty;
        }

        private string StayOnEntryEvent()
        {
            return ForceEncounterEventIds.事件.外层入口;
        }

        private void LogProbeFailure(SerializableModData result)
        {
            bool ok = false;
            if (result != null && result.Get(ForceEncounterConstants.Response.Ok, out ok) && ok)
            {
                return;
            }

            if (!ForceEncounterSettings.GetBool(GetRuntimeModId(), ForceEncounterConstants.Settings.DebugMode, true))
            {
                return;
            }

            string reason = ForceEncounterConstants.Reasons.NoResult;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Reason, out reason);
            }

            EventHelper.Log("[ForceEncounter] Probe did not start a follow-up event: " + reason);
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }

        private void DebugLogOnce(string key, string message)
        {
            if (!IsDebugModeEnabled())
            {
                return;
            }

            lock (LoggedDebugStates)
            {
                if (!LoggedDebugStates.Add(key))
                {
                    return;
                }
            }

            EventHelper.Log("[ForceEncounter] " + message);
        }

        private bool IsDebugModeEnabled()
        {
            return ForceEncounterSettings.GetBool(GetRuntimeModId(), ForceEncounterConstants.Settings.DebugMode, true);
        }
    }
}
