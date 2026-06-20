using System;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterGuardInterceptEvent : TaiwuEventItem
    {
        public ForceEncounterGuardInterceptEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.护卫出面);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            ForceSingle = true;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = ForceEncounterConstants.ArgBox.NativePartner;
            EscOptionKey = ForceEncounterEventIds.选项.护卫继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.护卫继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.护卫继续.Guid,
                    Behavior = EventOptionBehavior.None,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OnOptionSelect = StartGuardCombat
                }
            };
            EventOptions[0].SetContent("（继续……）");
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
            return ForceEncounterEventText.GuardInterceptContent;
        }

        private string StartGuardCombat()
        {
            return ForceEncounterCombatStarter.StartGuardCombat(ArgBox);
        }
    }
}
