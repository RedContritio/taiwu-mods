using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterPrisonerForcedResultEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerForcedResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押强制反馈);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.关押强制反馈继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.关押强制反馈继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.关押强制反馈继续.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = Continue
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.离开);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool targetIsTaiwuVillager = false;
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗目标是太吾村民, ref targetIsTaiwuVillager);
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);

            string content = ForceEncounterPrisonerText.BuildForcedResultContent(targetIsTaiwuVillager, 未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private string Continue()
        {
            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
