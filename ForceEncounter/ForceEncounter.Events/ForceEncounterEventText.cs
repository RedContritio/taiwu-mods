namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventText
    {
        private const string NormalAgeAcceptedContent = "{0}情难自已。\n\n你已经表明了意向，对方似乎也有此意。";
        private const string NormalAgeForcedContent = "{0}情难自已。\n\n你已经表明了意向，但对方似乎并无意向。";
        private const string NormalAgeGuardedForcedContent = "{0}情难自已。\n\n你已经表明了意向，但对方似乎并无意向。\n\n你隐约察觉到，对方身边有护卫照应；若继续强行推进，可能会先遭护卫阻拦。";
        private const string SpecialAgeAcceptedContent = "{0}情难自已。\n\n此人与寻常情形不同，尚处特殊年龄段。你已经表明了意向，对方似乎也有此意。若你继续，情难自已仍会照常进入后续判定与结算。";
        private const string SpecialAgeForcedContent = "{0}情难自已。\n\n此人与寻常情形不同，尚处特殊年龄段。你已经表明了意向，但对方似乎并无意向。若你继续，情难自已仍会照常进入后续判定与结算。";
        private const string SpecialAgeGuardedForcedContent = "{0}情难自已。\n\n此人与寻常情形不同，尚处特殊年龄段。你已经表明了意向，但对方似乎并无意向。若你继续，情难自已仍会照常进入后续判定与结算。\n\n你隐约察觉到，对方身边有护卫照应；若继续强行推进，可能会先遭护卫阻拦。";

        public const string GuardInterceptContent = "对方的护卫挺身拦在你面前。";
        public const string CombatSettleFailedContent = "战斗已经结束，但情难自已的结算未能完成。";
        public const string CombatSuccessContent = "战斗已经结束。你压服了对方，情难自已的结果已经生效。";
        public const string CombatSuccessTaiwuVillagerContent = "战斗已经结束。你压服了对方，情难自已的结果已经生效。对方身为太吾村民，未因此立刻与你结为仇敌。";
        public const string CombatFailureContent = "战斗已经结束。你未能压服对方，情难自已未能达成，但此事已经使双方结怨。";
        public const string CombatFailureTaiwuVillagerContent = "战斗已经结束。你未能压服对方，情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";

        public static string BuildConsentContent(string actionSummary, bool accepted, bool specialAge, bool guarded)
        {
            string template;
            if (accepted)
            {
                template = specialAge ? SpecialAgeAcceptedContent : NormalAgeAcceptedContent;
            }
            else
            {
                template = (specialAge, guarded) switch
                {
                    (true, true) => SpecialAgeGuardedForcedContent,
                    (true, false) => SpecialAgeForcedContent,
                    (false, true) => NormalAgeGuardedForcedContent,
                    _ => NormalAgeForcedContent
                };
            }

            return string.Format(template, actionSummary);
        }

        public static string BuildCombatResultContent(bool ok, bool succeeded, bool targetIsTaiwuVillager)
        {
            if (!ok)
            {
                return CombatSettleFailedContent;
            }

            if (succeeded)
            {
                return targetIsTaiwuVillager ? CombatSuccessTaiwuVillagerContent : CombatSuccessContent;
            }

            return targetIsTaiwuVillager ? CombatFailureTaiwuVillagerContent : CombatFailureContent;
        }
    }
}
