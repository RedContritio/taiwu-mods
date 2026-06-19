using System;
using Config;
using Config.EventConfig;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

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
            MainRoleKey = "RoleTaiwu";
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
                    Important = true,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(),
                    OnOptionVisibleCheck = IsAcceptedResolution,
                    OnOptionSelect = NormalEncounter
                },
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.强制关系.Key,
                    OptionGuid = ForceEncounterEventIds.选项.强制关系.Guid,
                    Behavior = EventOptionBehavior.BehaviorEgoistic,
                    DefaultState = EventOptionState.Normal,
                    Important = true,
                    OptionAvailableConditions = ForceEncounterEventCosts.BuildCommitConditions(),
                    OptionConsumeInfos = ForceEncounterEventCosts.BuildCommitCosts(),
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
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return ForceEncounterEventText.BuildConsentContent(
                ForceEncounterEventRuntime.GetActionSummary(ArgBox),
                IsAcceptedResolution(),
                ForceEncounterEventRuntime.IsSpecialAge(ArgBox),
                IsForcedResolution() && ForceEncounterEventRuntime.TargetHasGuard(ArgBox));
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

        private string NormalEncounter()
        {
            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                EventHelper.ToEvent(string.Empty);
                return string.Empty;
            }

            ForceEncounterEventRuntime.CallBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                ForceEncounterEventIds.结算模式.亲密提交);

            return string.Empty;
        }

        private string ForceCombat()
        {
            return ForceEncounterCombatStarter.StartForcedCombat(ArgBox, GetRuntimeModId());
        }

        private string Abandon()
        {
            return ArgBox?.GetString("MainInteractionHeadEvent") ?? ForceEncounterEventIds.事件.原生敌对菜单;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
