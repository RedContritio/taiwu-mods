using System;
using Config;
using Config.EventConfig;
using ForceEncounter.Shared;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Combat;
using GameData.Domains.Item;
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
            EventOptions[0].SetContent(ForceEncounterEventText.按钮.离开);
        }

        public override bool OnCheckEventCondition()
        {
            return true;
        }

        public override void OnEventEnter()
        {
            EventHelper.SetStopAutoNextEvent(false);
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int debugActorId, out int debugTargetId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatResult enter actor=" + debugActorId +
                ", target=" + debugTargetId);

            bool handled = false;
            if (ArgBox != null &&
                ArgBox.Get(ForceEncounterEventIds.参数.战斗结果已处理, ref handled) &&
                handled)
            {
                ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "CombatResult already handled from pre-combat failure path");
                return;
            }

            if (!ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId))
            {
                ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "CombatResult missing actor/target");
                StoreResult(false, false, false, ForceEncounterConstants.Reasons.MissingActorOrTarget);
                return;
            }

            sbyte combatResult = CombatResultType.EnemyWin;
            if (!ArgBox.Get(ForceEncounterConstants.ArgBox.NativeCombatResult, ref combatResult))
            {
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult missing native combat result actor=" + actorId +
                    ", target=" + targetId);
                StoreResult(false, false, false, ForceEncounterConstants.Reasons.MissingBattleResult);
                return;
            }

            CombatRoute combatRoute = ResolveCombatRoute(actorId, targetId, combatResult);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatResult native actor=" + actorId +
                ", target=" + targetId +
                ", combatResult=" + combatResult +
                ", battleSucceeded=" + combatRoute.BattleSucceeded +
                ", preliminaryReason=" + combatRoute.Reason);

            if (!combatRoute.ShouldCallBackend)
            {
                StoreResult(false, false, false, combatRoute.Reason);
                return;
            }

            SerializableModData result = ForceEncounterEventRuntime.CallCombatBackend(
                GetRuntimeModId(),
                actorId,
                targetId,
                combatRoute.BattleSucceeded);

            bool ok = false;
            bool succeeded = false;
            bool targetIsTaiwuVillager = false;
            bool appliedEnmity = false;
            string reason = combatRoute.Reason;
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

            if (combatRoute.KillTargetAfterBackend && ok && succeeded)
            {
                ArgBox.Set(ForceEncounterConstants.ArgBox.KillTargetAfterCombatResult, true);
                reason = ForceEncounterConstants.Reasons.TargetDiedInCombat;
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult scheduled native kill after successful settlement actor=" + actorId +
                    ", target=" + targetId);
            }

            StoreResult(ok, succeeded, targetIsTaiwuVillager, reason, appliedEnmity);
        }

        public override void OnEventExit()
        {
            ForceEncounterEventRuntime.TryGetActorAndTarget(ArgBox, out int actorId, out int targetId);
            ApplyScheduledTargetDeath(actorId, targetId);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatResult exit actor=" + actorId +
                ", target=" + targetId);
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
            bool 未成年 = ForceEncounterEventRuntime.是未成年分支(ArgBox);

            string content = ForceEncounterEventText.BuildCombatResultContent(
                ok,
                succeeded,
                targetIsTaiwuVillager,
                appliedEnmity,
                reason,
                未成年);
            return EventHelper.HandleStringTag(content, ArgBox, TaiwuEvent);
        }

        private void StoreResult(bool ok, bool succeeded, bool targetIsTaiwuVillager, string reason, bool appliedEnmity = false)
        {
            ForceEncounterEventRuntime.StoreCombatSettlement(ArgBox, ok, succeeded, targetIsTaiwuVillager, reason, appliedEnmity);
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatResult stored ok=" + ok +
                ", succeeded=" + succeeded +
                ", targetIsTaiwuVillager=" + targetIsTaiwuVillager +
                ", appliedEnmity=" + appliedEnmity +
                ", reason=" + reason);
        }

        private CombatRoute ResolveCombatRoute(int actorId, int targetId, sbyte combatResult)
        {
            int combatType = CombatConfig.DefKey.DieNormal;
            ArgBox.Get(ForceEncounterConstants.ArgBox.NativeCombatType, ref combatType);
            bool guardTeamPrepared = false;
            ArgBox.Get(ForceEncounterConstants.ArgBox.GuardTeamPrepared, ref guardTeamPrepared);

            bool combatAgainstTarget = IsCombatAgainstTarget(targetId);
            int mainEnemyId = targetId;
            bool hasMainEnemy = ArgBox.Get(ForceEncounterConstants.ArgBox.NativeMainEnemyId, ref mainEnemyId);
            if (guardTeamPrepared && CombatResultType.IsPlayerWin(combatResult))
            {
                EventHelper.CharacterLoseGuard(targetId, combatType);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult native side effect: CharacterLoseGuard target=" + targetId +
                    ", combatType=" + combatType);
            }

            int seizedCharacterId = -1;
            ItemKey seizeItemKey = default;
            if (ArgBox.Get(ForceEncounterConstants.ArgBox.NativeSeizedCharacterId, ref seizedCharacterId) &&
                ArgBox.Get<ItemKey>(ForceEncounterConstants.ArgBox.NativeSeizeItemKey, out seizeItemKey))
            {
                EventHelper.AddPrisonerToCharacter(seizedCharacterId, actorId, seizeItemKey);
                ArgBox.Set(ForceEncounterConstants.ArgBox.NativePrisoner, seizedCharacterId);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult native side effect: AddPrisonerToCharacter prisoner=" + seizedCharacterId +
                    ", owner=" + actorId);

                if (seizedCharacterId == targetId)
                {
                    return new CombatRoute(true, ForceEncounterConstants.Reasons.TargetCapturedInCombat);
                }

                EscapeTargetAfterGuardRoute(targetId, hasNotification: guardTeamPrepared);
                return new CombatRoute(false, ForceEncounterConstants.Reasons.GuardIntercepted);
            }

            if (!combatAgainstTarget)
            {
                if (CombatResultType.IsPlayerWin(combatResult))
                {
                    EscapeTargetAfterGuardRoute(targetId, hasNotification: guardTeamPrepared);
                }

                return new CombatRoute(false, ForceEncounterConstants.Reasons.GuardIntercepted);
            }

            if (hasMainEnemy && mainEnemyId != targetId)
            {
                if (CombatResultType.IsPlayerWin(combatResult))
                {
                    EscapeTargetAfterGuardRoute(targetId, hasNotification: guardTeamPrepared);
                }

                return new CombatRoute(false, ForceEncounterConstants.Reasons.GuardIntercepted);
            }

            switch (combatResult)
            {
                case CombatResultType.PlayerWin:
                    return new CombatRoute(true, ForceEncounterConstants.Reasons.NoResult);
                case CombatResultType.EnemyFlee:
                    EscapeTargetAfterDirectRoute(targetId);
                    return new CombatRoute(false, ForceEncounterConstants.Reasons.TargetEscaped);
                case CombatResultType.PlayerFlee:
                    EscapeActorAfterDirectRoute(actorId);
                    return new CombatRoute(false, ForceEncounterConstants.Reasons.ActorEscaped);
                case CombatResultType.EnemyDie:
                    return new CombatRoute(true, ForceEncounterConstants.Reasons.TargetDiedInCombat, killTargetAfterBackend: true);
                default:
                    return new CombatRoute(false, ForceEncounterConstants.Reasons.NoResult);
            }
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
            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatAgainstTarget check target=" + targetId +
                ", guardInterceptActive=" + guardInterceptActive +
                ", hasMainEnemy=" + hasMainEnemy +
                ", mainEnemyId=" + mainEnemyId);
            if (guardInterceptActive && !hasMainEnemy)
            {
                return false;
            }

            return !hasMainEnemy || mainEnemyId == targetId;
        }

        private void EscapeTargetAfterGuardRoute(int targetId, bool hasNotification)
        {
            if (DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                EventHelper.CharacterEscapeToNearbyBlock(ArgBox, target, 1, hasNotification);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult native side effect: target escape after guard route target=" + targetId +
                    ", hasNotification=" + hasNotification);
            }
        }

        private void EscapeTargetAfterDirectRoute(int targetId)
        {
            if (DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                EventHelper.CharacterEscapeToNearbyBlock(ArgBox, target, 3);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult native side effect: target escape after direct route target=" + targetId);
            }
        }

        private void EscapeActorAfterDirectRoute(int actorId)
        {
            if (actorId == DomainManager.Taiwu.GetTaiwuCharId())
            {
                EventHelper.CharacterEscapeToNearbyBlock(ArgBox, DomainManager.Taiwu.GetTaiwu(), 1);
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult native side effect: Taiwu escape after direct route actor=" + actorId);
            }
        }

        private void ApplyScheduledTargetDeath(int actorId, int targetId)
        {
            if (ArgBox == null)
            {
                return;
            }

            bool killTarget = false;
            if (!ArgBox.Get(ForceEncounterConstants.ArgBox.KillTargetAfterCombatResult, ref killTarget) ||
                !killTarget)
            {
                return;
            }

            ArgBox.Set(ForceEncounterConstants.ArgBox.KillTargetAfterCombatResult, false);
            if (!DomainManager.Character.TryGetElement_Objects(actorId, out var actor) ||
                !DomainManager.Character.TryGetElement_Objects(targetId, out var target))
            {
                ForceEncounterEventRuntime.DebugLog(
                    GetRuntimeModId(),
                    "CombatResult scheduled native kill skipped: missing actor or target actor=" + actorId +
                    ", target=" + targetId);
                return;
            }

            ForceEncounterEventRuntime.DebugLog(
                GetRuntimeModId(),
                "CombatResult native side effect: HandleCombatResultKillEnemy after successful settlement actor=" + actorId +
                ", target=" + targetId);
            EventHelper.HandleCombatResultKillEnemy(actor, target, true);
        }

        private string Continue()
        {
            ForceEncounterEventRuntime.DebugLog(GetRuntimeModId(), "CombatResult continue selected");
            bool ok = false;
            bool succeeded = false;
            string reason = ForceEncounterConstants.Reasons.NoResult;
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗结算成功, ref ok);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗分支成功, ref succeeded);
            ArgBox?.Get(ForceEncounterEventIds.参数.战斗结算原因, ref reason);
            if (ok &&
                succeeded &&
                reason == ForceEncounterConstants.Reasons.TargetCapturedInCombat)
            {
                return ForceEncounterEventIds.事件.擒获处置;
            }

            EventHelper.ToEvent(string.Empty);
            return string.Empty;
        }

        private static string GetRuntimeModId()
        {
            return ForceEncounterEventRuntime.GetRuntimeModId(ForceEncounterEventIds.事件.战斗反馈);
        }

        private readonly struct CombatRoute
        {
            public CombatRoute(
                bool battleSucceeded,
                string reason,
                bool shouldCallBackend = true,
                bool killTargetAfterBackend = false)
            {
                BattleSucceeded = battleSucceeded;
                Reason = reason;
                ShouldCallBackend = shouldCallBackend;
                KillTargetAfterBackend = killTargetAfterBackend;
            }

            public bool BattleSucceeded { get; }
            public string Reason { get; }
            public bool ShouldCallBackend { get; }
            public bool KillTargetAfterBackend { get; }
        }
    }
}
