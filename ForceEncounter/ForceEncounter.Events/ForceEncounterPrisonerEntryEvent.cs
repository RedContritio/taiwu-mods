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
    internal sealed class ForceEncounterPrisonerEntryEvent : TaiwuEventItem
    {
        public ForceEncounterPrisonerEntryEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.关押外层入口);
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
                    OptionKey = ForceEncounterEventIds.选项.情难自已关押.Key,
                    OptionGuid = ForceEncounterEventIds.选项.情难自已关押.Guid,
                    Behavior = EventOptionBehavior.BehaviorEgoistic,
                    DefaultState = EventOptionState.Normal,
                    Important = false,
                    OptionConsumeInfos = new List<OptionConsumeInfo>(),
                    OnOptionVisibleCheck = IsVisible,
                    OnOptionAvailableCheck = CanExecute,
                    OnOptionSelect = Execute
                }
            };
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.情难自已);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            RefreshPreviewCosts();
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            return string.Empty;
        }

        private bool IsVisible()
        {
            RefreshPreviewCosts();
            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);
            if (允许未成年)
            {
                return true;
            }

            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return true;
            }

            return !ForceEncounterEventRuntime.需要未成年提示(ForceEncounterEventRuntime.GetActorId(), targetId);
        }

        private bool CanExecute()
        {
            RefreshPreviewCosts();
            int targetId = -1;
            if (ArgBox == null || !ArgBox.Get(EventTriggerParameter.DefValue.CharacterId, ref targetId))
            {
                return false;
            }

            int actorId = ForceEncounterEventRuntime.GetActorId();
            if (actorId == targetId)
            {
                return false;
            }

            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                return false;
            }

            if (!DomainManager.Character.IsCharacterAlive(actorId) || !DomainManager.Character.IsCharacterAlive(targetId))
            {
                return false;
            }

            if (actor.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组 ||
                target.GetAgeGroup() == ForceEncounterConstants.Gameplay.婴儿年龄组)
            {
                return false;
            }

            string modId = GetRuntimeModId();
            bool 允许未成年 = TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.允许未成年, true);
            if (!允许未成年 &&
                (actor.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组 ||
                 target.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组))
            {
                return false;
            }

            return true;
        }

        private int RefreshPreviewCosts()
        {
            string modId = GetRuntimeModId();
            int days = ForceEncounterEventCosts.GetActionTimeCostDays(modId);
            EventOptions[0].OptionConsumeInfos = ForceEncounterEventCosts.BuildPreviewCosts(modId);
            return days;
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
                return ForceEncounterEventIds.事件.关押内层选择;
            }

            return string.Empty;
        }

        private string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(this);
        }
    }
}
