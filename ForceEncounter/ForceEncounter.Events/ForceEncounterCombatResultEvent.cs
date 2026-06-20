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
            if (!ArgBox.Get(ForceEncounterConstants.ArgBox.NativeCombatResult, ref combatResult))
            {
                StoreResult(false, false, false, ForceEncounterConstants.Reasons.MissingBattleResult);
                return;
            }

            bool battleSucceeded = CombatResultType.IsPlayerWin(combatResult);
            string reason = ForceEncounterConstants.Reasons.NoResult;
            if (!IsCombatAgainstTarget(targetId))
            {
                battleSucceeded = false;
                reason = ForceEncounterConstants.Reasons.GuardIntercepted;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallCombatBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                battleSucceeded);

            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            if (result != null)
            {
                result.Get(ForceEncounterConstants.Response.Ok, out ok);
                result.Get(ForceEncounterConstants.Response.Succeeded, out succeeded);
                result.Get(ForceEncounterConstants.Response.TargetIsTaiwuVillager, out targetIsTaiwuVillager);
                result.Get(ForceEncounterConstants.Response.AppliedEnmity, out appliedEnmity);
                if (reason == ForceEncounterConstants.Reasons.NoResult)
                {
                    result.Get(ForceEncounterConstants.Response.Reason, out reason);
                }
            }

            StoreResult(ok, succeeded, targetIsTaiwuVillager, reason, appliedEnmity);
        }

        public override void OnEventExit()
        {
        }

        public override string GetReplacedContentString()
        {
            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗结算成功, ref ok);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗分支成功, ref succeeded);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗目标是太吾村民, ref targetIsTaiwuVillager);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗应用结仇后果, ref appliedEnmity);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗结算原因, ref reason);

            return ForceEncounterEventText.BuildCombatResultContent(ok, succeeded, targetIsTaiwuVillager, appliedEnmity, reason);
        }

        private void StoreResult(bool ok, bool succeeded, bool targetIsTaiwuVillager, string reason, bool appliedEnmity = false)
        {
            ForceEncounterEventRuntime.StoreCombatSettlement(ArgBox, ok, succeeded, targetIsTaiwuVillager, reason, appliedEnmity);
        }

        private bool IsCombatAgainstTarget(int targetId)
        {
            if (ArgBox == null)
            {
                return true;
            }

            bool guardInterceptActive = false;
            ArgBox.Get(ForceEncounterConstants.ArgBox.GuardInterceptActive, ref guardInterceptActive);

            int mainEnemyId = targetId;
            bool hasMainEnemy = ArgBox.Get(ForceEncounterConstants.ArgBox.NativeMainEnemyId, ref mainEnemyId);
            if (guardInterceptActive && !hasMainEnemy)
            {
                return false;
            }

            return !hasMainEnemy || mainEnemyId == targetId;
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
