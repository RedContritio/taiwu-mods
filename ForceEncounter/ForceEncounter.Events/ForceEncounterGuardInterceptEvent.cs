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
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.继续);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            int partnerId = -1;
            ArgBox?.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "GuardIntercept enter actor=" + actorId +
                ", target=" + targetId +
                ", guard=" + partnerId);
        }

        public override void OnEventExit()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            int partnerId = -1;
            ArgBox?.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "GuardIntercept exit actor=" + actorId +
                ", target=" + targetId +
                ", guard=" + partnerId);
        }

        public override string GetReplacedContentString()
        {
            return ForceEncounterEventText.过场.护卫出面;
        }

        private string StartGuardCombat()
        {
            int partnerId = -1;
            ArgBox?.Get(ForceEncounterConstants.ArgBox.NativePartner, ref partnerId);
            ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "GuardIntercept continue selected guard=" + partnerId);
            return ForceEncounterCombatStarter.StartGuardCombat(ArgBox);
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
