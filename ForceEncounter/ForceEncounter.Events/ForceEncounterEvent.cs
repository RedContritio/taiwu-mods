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
using TaiwuMod.Common;

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
                    OptionConsumeInfos = new List<OptionConsumeInfo>(),
                    OnOptionVisibleCheck = IsVisible,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = Execute
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.空);
            EventOptions[1].SetContent(ForceEncounterEventText.按钮.情难自已);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            int previewCostDays = RefreshPreviewCosts();
            ForceEncounterEventRuntime.TryGetTargetId(ArgBox, out int targetId);
            DebugLog(
                "EntryEvent enter actor=" + ForceEncounterEventRuntime.GetActorId() +
                ", target=" + targetId +
                ", previewCostDays=" + previewCostDays);
        }

        public override void OnEventExit()
        {
            ForceEncounterEventRuntime.TryGetTargetId(ArgBox, out int targetId);
            DebugLog(
                "EntryEvent exit actor=" + ForceEncounterEventRuntime.GetActorId() +
                ", target=" + targetId);
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }

        private bool CanExecute()
        {
            int previewCostDays = RefreshPreviewCosts();
            string reason = GetCanExecuteFailureReason(out int actorId, out int targetId);
            bool canExecute = reason == ForceEncounterConstants.Reasons.Ok;
            DebugLog(
                "CanExecute actor=" + actorId +
                ", target=" + targetId +
                ", result=" + canExecute +
                ", reason=" + reason +
                ", previewCostDays=" + previewCostDays);
            return canExecute;
        }

        private bool IsVisible()
        {
            int previewCostDays = RefreshPreviewCosts();
            bool 未成年可见 = 未成年选项可见(out string 未成年原因, out int actorId, out int targetId);
            DebugLogOnce(
                "visible:" + actorId + ":" + targetId + ":" + 未成年原因,
                "IsVisible actor=" + actorId +
                ", target=" + targetId +
                ", result=" + 未成年可见 +
                ", minorReason=" + 未成年原因 +
                ", previewCostDays=" + previewCostDays);
            return 未成年可见;
        }

        private int RefreshPreviewCosts()
        {
            string modId = GetRuntimeModId();
            int days = ForceEncounterEventCosts.GetActionTimeCostDays(modId);
            EventOptions[1].OptionConsumeInfos = ForceEncounterEventCosts.BuildPreviewCosts(modId);
            return days;
        }

        private bool 未成年选项可见(out string reason, out int actorId, out int targetId)
        {
            actorId = ForceEncounterEventRuntime.GetActorId();
            targetId = -1;
            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);

            if (允许未成年)
            {
                reason = ForceEncounterConstants.Reasons.VisibleAllowedBySetting;
                return true;
            }

            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                reason = ForceEncounterConstants.Reasons.MissingTargetForVisibleCheck;
                return true;
            }

            bool 需要未成年提示 = ForceEncounterEventRuntime.需要未成年提示(actorId, targetId);
            reason = 需要未成年提示 ? ForceEncounterConstants.Reasons.未成年不允许 : ForceEncounterConstants.Reasons.Ok;
            return !需要未成年提示;
        }

        private string GetCanExecuteFailureReason(out int actorId, out int targetId)
        {
            actorId = -1;
            targetId = -1;

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

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组)
            {
                return ForceEncounterConstants.Reasons.ActorBaby;
            }

            if (target.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组)
            {
                return ForceEncounterConstants.Reasons.TargetBaby;
            }

            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);

            if (!允许未成年 &&
                (actor.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组 ||
                 target.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组))
            {
                return ForceEncounterConstants.Reasons.未成年不允许;
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
            DebugLog(
                "Entry option selected actor=" + actorId +
                ", target=" + targetId +
                ", waitConfirm=" + ForceEncounterEventIds.等待确认.外层预览 +
                ", minor=" + ForceEncounterEventRuntime.需要未成年提示(actorId, targetId));

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
                DebugLog(
                    "Probe routed actor=" + actorId +
                    ", target=" + targetId +
                    ", resolution=" + resolution +
                    ", nextEvent=" + ForceEncounterEventIds.事件.内层选择);
                return ForceEncounterEventIds.事件.内层选择;
            }

            LogProbeFailure(result);
            return string.Empty;
        }

        private string StayOnEntryEvent()
        {
            DebugLog("Entry navigation selected; stay on entry event");
            return ForceEncounterEventIds.事件.外层入口;
        }

        private void LogProbeFailure(SerializableModData result)
        {
            bool ok = false;
            if (result != null && result.Get(ForceEncounterConstants.Response.Ok, out ok) && ok)
            {
                return;
            }

            if (!TaiwuModSettings.GetBool(GetRuntimeModId(), ForceEncounterConstants.Settings.DebugMode, true))
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

        private void DebugLog(string message)
        {
            ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), message);
        }

        private bool IsDebugModeEnabled()
        {
            return TaiwuModSettings.GetBool(GetRuntimeModId(), ForceEncounterConstants.Settings.DebugMode, true);
        }
    }
}
