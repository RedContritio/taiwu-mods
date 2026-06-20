using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;
using TaiwuMod.Common;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterConsentChoiceEvent : TaiwuEventItem
    {
        public ForceEncounterConsentChoiceEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.内层选择);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.其他话题.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.正常发生关系.Key,
                    OptionGuid = ForceEncounterEventIds.选项.正常发生关系.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(string.Empty),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(string.Empty),
                    OnOptionVisibleCheck = IsAcceptedResolution,
                    OnOptionAvailableCheck = CanCommitAcceptedResolution,
                    OnOptionSelect = NormalEncounter
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.强制关系.Key,
                    OptionGuid = ForceEncounterEventIds.选项.强制关系.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = true,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(string.Empty),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(string.Empty),
                    OnOptionVisibleCheck = IsForcedResolution,
                    OnOptionSelect = ForceCombat
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.其他话题.Key,
                    OptionGuid = ForceEncounterEventIds.选项.其他话题.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Abandon
                }
            };
            EventOptions[0].SetContent("（正常发生关系……）");
            EventOptions[1].SetContent("（强制关系……）");
            EventOptions[2].SetContent("其他话题");
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            string modId = GetRuntimeModId();
            EventOptions[0].OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(modId);
            EventOptions[0].OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(modId);
            EventOptions[1].OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(modId);
            EventOptions[1].OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(modId);
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            int resolution = -1;
            ArgBox?.Get(ForceEncounterEventIds.后端.结算结果, ref resolution);
            ForceEncounterEventRuntime.DebugLog(
                modId,
                "ConsentChoice enter actor=" + actorId +
                ", target=" + targetId +
                ", resolution=" + resolution +
                ", acceptedVisible=" + IsAcceptedResolution() +
                ", forcedVisible=" + IsForcedResolution() +
                ", minor=" + ForceEncounterEventRuntime.是未成年分支(ArgBox) +
                ", targetHasGuard=" + ForceEncounterEventRuntime.TargetHasGuard(ArgBox, modId) +
                ", commitCostDays=" + TaiwuModSettings.GetInt(modId, ForceEncounterConstants.Settings.ActionTimeCostDays, ForceEncounterConstants.Costs.DefaultActionTimeDays));
        }

        public override void OnEventExit()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "ConsentChoice exit actor=" + actorId +
                ", target=" + targetId);
        }

        public override string GetReplacedContentString()
        {
            bool 亲密通过 = IsAcceptedResolution();
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);
            bool 有护卫 = IsForcedResolution() && ForceEncounterEventRuntime.TargetHasGuard(ArgBox, GetRuntimeModId());
            string content = ForceEncounterEventText.构造内层说明(
                亲密通过,
                未成年,
                有护卫);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private bool IsAcceptedResolution()
        {
            int resolution = -1;
            return ArgBox != null &&
                   ArgBox.Get(ForceEncounterEventIds.后端.结算结果, ref resolution) &&
                   resolution == ForceEncounterEventIds.探测结果.亲密通过;
        }

        private bool IsForcedResolution()
        {
            int resolution = -1;
            return ArgBox != null &&
                   ArgBox.Get(ForceEncounterEventIds.后端.结算结果, ref resolution) &&
                   resolution == ForceEncounterEventIds.探测结果.需要战斗选择;
        }

        private bool CanCommitAcceptedResolution()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                return false;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.探测);
            if (result != null &&
                result.Get(ForceEncounterEventIds.后端.结算结果, out int resolution) &&
                resolution == ForceEncounterEventIds.探测结果.亲密通过)
            {
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "Accepted option recheck passed actor=" + actorId +
                    ", target=" + targetId);
                return true;
            }

            ArgBox.Set(ForceEncounterEventIds.后端.结算结果, ForceEncounterEventIds.探测结果.需要战斗选择);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "Accepted option recheck failed; reroute to force choice actor=" + actorId +
                ", target=" + targetId);
            return false;
        }

        private string NormalEncounter()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.亲密提交);
            bool ok = false;
            bool succeeded = false;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.Reason, out reason);
            }

            if (reason == ForceEncounterConstants.Reasons.NeedCombatChoice)
            {
                ArgBox.Set(ForceEncounterEventIds.后端.结算结果, ForceEncounterEventIds.探测结果.需要战斗选择);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "NormalEncounter commit rerouted actor=" + actorId +
                    ", target=" + targetId +
                    ", reason=" + reason);
                return ForceEncounterEventIds.事件.内层选择;
            }

            ForceEncounterEventRuntime.StoreAcceptedSettlement(ArgBox, ok, succeeded, reason);
            if (ok && succeeded)
            {
                ForceEncounterEventRuntime.ConfirmOuterWaitOption(ArgBox);
            }

            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "NormalEncounter committed actor=" + actorId +
                ", target=" + targetId +
                ", ok=" + ok +
                ", succeeded=" + succeeded +
                ", reason=" + reason +
                ", waitConfirm=" + (ok && succeeded ? "confirmed" : "not-confirmed"));
            return ForceEncounterEventIds.事件.亲密反馈;
        }

        private string ForceCombat()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            ForceEncounterEventRuntime.ConfirmOuterWaitOption(ArgBox);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "ForceCombat selected actor=" + actorId +
                ", target=" + targetId +
                ", waitConfirm=confirmed");
            return ForceEncounterCombatStarter.StartForcedCombat(ArgBox, GetRuntimeModId());
        }

        private string Abandon()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            string nextEvent = ArgBox?.GetString(ForceEncounterConstants.ArgBox.NativeMainInteractionHeadEvent) ?? ForceEncounterEventIds.事件.原生敌对菜单;
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "ConsentChoice abandon actor=" + actorId +
                ", target=" + targetId +
                ", waitConfirm=not-confirmed" +
                ", nextEvent=" + nextEvent);
            return ArgBox?.GetString(ForceEncounterConstants.ArgBox.NativeMainInteractionHeadEvent) ?? ForceEncounterEventIds.事件.原生敌对菜单;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
