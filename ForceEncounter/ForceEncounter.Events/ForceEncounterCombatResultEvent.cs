using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains.Combat;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent.Enum;
using GameData.Domains.TaiwuEvent.EventHelper;
using GameData.Domains.TaiwuEvent.EventOption;

namespace ForceEncounter.Events
{
    internal sealed class ForceEncounterCombatResultEvent : TaiwuEventItem
    {
        public ForceEncounterCombatResultEvent()
        {
            Guid = Guid.Parse(ForceEncounterEventIds.事件.战斗反馈);
            EventType = EEventType.ModEvent;
            IsHeadEvent = false;
            TriggerType = -1;
            MainRoleKey = ForceEncounterConstants.RoleKeys.Taiwu;
            TargetRoleKey = EventTriggerParameter.DefValue.CharacterId.ArgBoxKey;
            EscOptionKey = ForceEncounterEventIds.选项.战斗反馈继续.Key;
            EventOptions = new[]
            {
                new TaiwuEventOption
                {
                    OptionKey = ForceEncounterEventIds.选项.战斗反馈继续.Key,
                    OptionGuid = ForceEncounterEventIds.选项.战斗反馈继续.Guid,
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
            EventHelper.SetStopAutoNextEvent(false);

            bool handled = false;
            if (ArgBox != null &&
                ArgBox.Get(ForceEncounterEventIds.参数.战斗结果已处理, ref handled) &&
                handled)
            {
                return;
            }

            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                StoreResult(false, false, false, ForceEncounterConstants.Reasons.MissingActorOrTarget);
                return;
            }

            sbyte combatResult = CombatResultType.EnemyWin;
            bool battleSucceeded = ArgBox.Get(ForceEncounterConstants.ArgBox.NativeCombatResult, ref combatResult) &&
                                   CombatResultType.IsPlayerWin(combatResult);

            SerializableModData result = ForceEncounterEventRuntime.CallCombatBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                battleSucceeded);

            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
                result.Get(ForceEncounterConstants.Response.Reason, out reason);
            }

            StoreResult(ok, succeeded, targetIsTaiwuVillager, reason);
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗结算成功, ref ok);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗分支成功, ref succeeded);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗目标是太吾村民, ref targetIsTaiwuVillager);

            return ForceEncounterEventText.BuildCombatResultContent(ok, succeeded, targetIsTaiwuVillager);
        }

        private void StoreResult(bool ok, bool succeeded, bool targetIsTaiwuVillager, string reason)
        {
            if (ArgBox == null)
            {
                return;
            }

            ArgBox.Set(ForceEncounterEventIds.参数.战斗结果已处理, true);
            ArgBox.Set(ForceEncounterEventIds.参数.战斗结算成功, ok);
            ArgBox.Set(ForceEncounterEventIds.参数.战斗分支成功, succeeded);
            ArgBox.Set(ForceEncounterEventIds.参数.战斗目标是太吾村民, targetIsTaiwuVillager);
            ArgBox.Set(ForceEncounterEventIds.参数.战斗结算原因, reason ?? string.Empty);
        }

        private string Continue()
        {
            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private static string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(ForceEncounterEventIds.事件.战斗反馈);
        }
    }
}
