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
            public const string 原生敌对菜单 = "7c70ce0c-577a-4049-bcad-e593c63d62d4";
            public const string 外层入口 = "dd8e7372-30c6-47e9-9e1a-0f6dd7f08919";
            public const string 内层选择 = "a5ef5a51-2c64-40d2-a936-7c9d8ff7c6c6";
            public const string 护卫出面 = "27f95f25-e7bb-470a-970e-3fb4cf12f63b";
            public const string 战斗反馈 = "ea75dd81-9054-4e54-9e6e-799669e2fb41";
        }

        public static class 选项
        {
            public static readonly EventOptionId 打开入口 = new("ForceEncounter.Open", "ee1110a7-f974-4bc4-bf84-1ddcfcc04ae1");
            public static readonly EventOptionId 情难自已 = new("ForceEncounter.Execute", "8267b6bc-3a92-4b3f-a013-a3446272866f");
            public static readonly EventOptionId 正常发生关系 = new("ForceEncounter.NormalEncounter", "8e7a9f4b-6d2c-4e0f-8c63-49ecf7de6812");
            public static readonly EventOptionId 强制关系 = new("ForceEncounter.ForceCombat", "ce18f262-9df1-46df-a604-0fd77759d8f0");
            public static readonly EventOptionId 其他话题 = new("ForceEncounter.Abandon", "8904aa49-9a92-4b82-9045-dad4f125ff8b");
            public static readonly EventOptionId 战斗反馈继续 = new("ForceEncounter.CombatResultContinue", "2bf7c96a-d482-45ce-96d8-47dd4ecb795c");
            public static readonly EventOptionId 护卫继续 = new("ForceEncounter.GuardInterceptContinue", "24d8d555-70c2-4c47-8518-1e74cfdbf2e9");
        }

        public static class 等待确认
        {
            public const string 外层预览 = "ForceEncounter.OuterPreview";
        }

        public static class 参数
        {
            public const string 行为者 = "ForceEncounter.ActorId";
            public const string 目标 = "ForceEncounter.TargetId";
            public const string 战斗结果已处理 = "ForceEncounter.CombatResultHandled";
            public const string 战斗结算成功 = "ForceEncounter.CombatResultOk";
            public const string 战斗分支成功 = "ForceEncounter.CombatResultSucceeded";
            public const string 战斗目标是太吾村民 = "ForceEncounter.CombatResultTargetTaiwuVillager";
            public const string 战斗结算原因 = "ForceEncounter.CombatResultReason";
            public const string 特殊年龄 = "ForceEncounter.SpecialAge";
        }

        public static class 后端
        {
            public const string 执行方法 = "ExecuteForcedAction";
            public const string 战斗成功 = "BattleSucceeded";
            public const string 结算模式 = "ResolutionMode";
            public const string 结算结果 = "Resolution";
        }

        public static class 结算模式
        {
            public const int 探测 = 1;
            public const int 战斗结算 = 2;
            public const int 亲密提交 = 3;
        }

        public static class 探测结果
        {
            public const int 需要战斗选择 = 1;
            public const int 亲密通过 = 2;
        }
    }
}
