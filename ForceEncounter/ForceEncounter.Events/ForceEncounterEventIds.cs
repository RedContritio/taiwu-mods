using ForceEncounter.Shared;

namespace ForceEncounter.Events
{
    public static class ForceEncounterEventIds
    {
        public readonly struct EventOptionId
        {
            public EventOptionId(string key, string guid)
            {
                Key = key;
                Guid = guid;
            }

            public string Key { get; }
            public string Guid { get; }
        }

        public static class 事件
        {
            public const string 原生敌对菜单 = ForceEncounterConstants.EventGuids.NativeEnemyInteraction;
            public const string 外层入口 = ForceEncounterConstants.EventGuids.Entry;
            public const string 内层选择 = ForceEncounterConstants.EventGuids.ConsentChoice;
            public const string 护卫出面 = ForceEncounterConstants.EventGuids.GuardIntercept;
            public const string 战斗反馈 = ForceEncounterConstants.EventGuids.CombatResult;
            public const string 亲密反馈 = ForceEncounterConstants.EventGuids.AcceptedResult;
        }

        public static class 选项
        {
            public static readonly EventOptionId 打开入口 = new(ForceEncounterConstants.Options.OpenKey, ForceEncounterConstants.Options.OpenGuid);
            public static readonly EventOptionId 情难自已 = new(ForceEncounterConstants.Options.ExecuteKey, ForceEncounterConstants.Options.ExecuteGuid);
            public static readonly EventOptionId 正常发生关系 = new(ForceEncounterConstants.Options.NormalEncounterKey, ForceEncounterConstants.Options.NormalEncounterGuid);
            public static readonly EventOptionId 强制关系 = new(ForceEncounterConstants.Options.ForceCombatKey, ForceEncounterConstants.Options.ForceCombatGuid);
            public static readonly EventOptionId 其他话题 = new(ForceEncounterConstants.Options.AbandonKey, ForceEncounterConstants.Options.AbandonGuid);
            public static readonly EventOptionId 战斗反馈继续 = new(ForceEncounterConstants.Options.CombatResultContinueKey, ForceEncounterConstants.Options.CombatResultContinueGuid);
            public static readonly EventOptionId 亲密反馈继续 = new(ForceEncounterConstants.Options.AcceptedResultContinueKey, ForceEncounterConstants.Options.AcceptedResultContinueGuid);
            public static readonly EventOptionId 护卫继续 = new(ForceEncounterConstants.Options.GuardContinueKey, ForceEncounterConstants.Options.GuardContinueGuid);
        }

        public static class 等待确认
        {
            public const string 外层预览 = ForceEncounterConstants.WaitConfirm.OuterPreview;
        }

        public static class 参数
        {
            public const string 行为者 = ForceEncounterConstants.ArgBox.ActorId;
            public const string 目标 = ForceEncounterConstants.ArgBox.TargetId;
            public const string 未成年 = ForceEncounterConstants.ArgBox.未成年;
            public const string 战斗结果已处理 = ForceEncounterConstants.ArgBox.CombatResultHandled;
            public const string 战斗结算成功 = ForceEncounterConstants.ArgBox.CombatSettleOk;
            public const string 战斗分支成功 = ForceEncounterConstants.ArgBox.CombatBranchSucceeded;
            public const string 战斗目标是太吾村民 = ForceEncounterConstants.ArgBox.CombatTargetIsTaiwuVillager;
            public const string 战斗应用结仇后果 = ForceEncounterConstants.ArgBox.CombatAppliedEnmity;
            public const string 战斗结算原因 = ForceEncounterConstants.ArgBox.CombatSettleReason;
            public const string 亲密结果已处理 = ForceEncounterConstants.ArgBox.AcceptedResultHandled;
            public const string 亲密结算成功 = ForceEncounterConstants.ArgBox.AcceptedSettleOk;
            public const string 亲密分支成功 = ForceEncounterConstants.ArgBox.AcceptedBranchSucceeded;
            public const string 亲密结算原因 = ForceEncounterConstants.ArgBox.AcceptedSettleReason;
        }

        public static class 后端
        {
            public const string 执行方法 = ForceEncounterConstants.Backend.ExecuteMethod;
            public const string 战斗成功 = ForceEncounterConstants.Backend.BattleSucceeded;
            public const string 结算模式 = ForceEncounterConstants.Backend.ResolutionMode;
            public const string 结算结果 = ForceEncounterConstants.Backend.Resolution;
        }

        public static class 结算模式
        {
            public const int 探测 = ForceEncounterConstants.ResolutionMode.Probe;
            public const int 战斗结算 = ForceEncounterConstants.ResolutionMode.Combat;
            public const int 亲密提交 = ForceEncounterConstants.ResolutionMode.AcceptedCommit;
        }

        public static class 探测结果
        {
            public const int 需要战斗选择 = ForceEncounterConstants.ProbeResult.NeedCombatChoice;
            public const int 亲密通过 = ForceEncounterConstants.ProbeResult.Accepted;
        }
    }
}
