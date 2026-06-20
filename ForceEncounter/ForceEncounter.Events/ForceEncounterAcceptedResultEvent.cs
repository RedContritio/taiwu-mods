using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterAcceptedResultEvent : TaiwuEventItem
    {
        public ForceEncounterAcceptedResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.亲密反馈);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.亲密反馈继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.亲密反馈继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.亲密反馈继续.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Continue
                }
            };
            EventOptions[0].SetContent("离开");
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "AcceptedResult enter actor=" + actorId +
                ", target=" + targetId);
        }

        public override void OnEventExit()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "AcceptedResult exit actor=" + actorId +
                ", target=" + targetId);
        }

        public override string GetReplacedContentString()
        {
            bool ok = false;
            bool succeeded = false;
            ArgBox?.Get(ForceEncounterEventIds.参数.亲密结算成功, ref ok);
            ArgBox?.Get(ForceEncounterEventIds.参数.亲密分支成功, ref succeeded);
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);

            string content = ForceEncounterEventText.BuildAcceptedResultContent(ok, succeeded, 未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private string Continue()
        {
            ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "AcceptedResult continue selected");
            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
