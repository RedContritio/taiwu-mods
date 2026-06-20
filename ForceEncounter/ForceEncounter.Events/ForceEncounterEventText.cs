using System;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventText
    {
        public const string GuardInterceptContent = "对方的护卫挺身拦在你面前。";

        public static string 构造内层说明(bool 亲密通过, bool 未成年, bool 有护卫)
        {
            if (亲密通过)
            {
                return 未成年 ? 入口说明.未成年亲密 : 入口说明.成年亲密;
            }

            return (未成年, 有护卫) switch
            {
                (true, true) => 入口说明.未成年强制有护卫,
                (true, false) => 入口说明.未成年强制,
                (false, true) => 入口说明.成年强制有护卫,
                _ => 入口说明.成年强制
            };
        }

        public static string BuildCombatResultContent(bool ok, bool succeeded, bool targetIsTaiwuVillager, bool appliedEnmity, string reason, bool 未成年)
        {
            if (!ok)
            {
                return 未成年 ? 强制反馈.未成年结算失败 : 强制反馈.成年结算失败;
            }

            if (succeeded && reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetDiedInCombat)
            {
                return SelectSuccessContent(
                    未成年,
                    targetIsTaiwuVillager,
                    强制反馈.成年战死后成功,
                    强制反馈.成年战死后成功村民,
                    强制反馈.未成年战死后成功,
                    强制反馈.未成年战死后成功村民);
            }

            if (succeeded && reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetDirectFallen)
            {
                return SelectSuccessContent(
                    未成年,
                    targetIsTaiwuVillager,
                    强制反馈.成年无力应战成功,
                    强制反馈.成年无力应战成功村民,
                    强制反馈.未成年无力应战成功,
                    强制反馈.未成年无力应战成功村民);
            }

            if (succeeded)
            {
                return SelectSuccessContent(
                    未成年,
                    targetIsTaiwuVillager,
                    强制反馈.成年成功,
                    强制反馈.成年成功村民,
                    强制反馈.未成年成功,
                    强制反馈.未成年成功村民);
            }

            if (reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetDirectFallen)
            {
                return SelectFailureContent(
                    未成年,
                    targetIsTaiwuVillager,
                    appliedEnmity,
                    强制反馈.成年无力应战村民,
                    强制反馈.成年无力应战结仇,
                    强制反馈.成年无力应战不结仇,
                    强制反馈.未成年无力应战村民,
                    强制反馈.未成年无力应战结仇,
                    强制反馈.未成年无力应战不结仇);
            }

            if (reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.GuardIntercepted)
            {
                return SelectFailureContent(
                    未成年,
                    targetIsTaiwuVillager,
                    appliedEnmity,
                    强制反馈.成年护卫拦截村民,
                    强制反馈.成年护卫拦截结仇,
                    强制反馈.成年护卫拦截不结仇,
                    强制反馈.未成年护卫拦截村民,
                    强制反馈.未成年护卫拦截结仇,
                    强制反馈.未成年护卫拦截不结仇);
            }

            if (reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetEscaped)
            {
                return SelectFailureContent(
                    未成年,
                    targetIsTaiwuVillager,
                    appliedEnmity,
                    强制反馈.成年目标逃走村民,
                    强制反馈.成年目标逃走结仇,
                    强制反馈.成年目标逃走不结仇,
                    强制反馈.未成年目标逃走村民,
                    强制反馈.未成年目标逃走结仇,
                    强制反馈.未成年目标逃走不结仇);
            }

            if (reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.ActorEscaped)
            {
                return SelectFailureContent(
                    未成年,
                    targetIsTaiwuVillager,
                    appliedEnmity,
                    强制反馈.成年太吾撤退村民,
                    强制反馈.成年太吾撤退结仇,
                    强制反馈.成年太吾撤退不结仇,
                    强制反馈.未成年太吾撤退村民,
                    强制反馈.未成年太吾撤退结仇,
                    强制反馈.未成年太吾撤退不结仇);
            }

            return SelectFailureContent(
                未成年,
                targetIsTaiwuVillager,
                appliedEnmity,
                强制反馈.成年普通失败村民,
                强制反馈.成年普通失败结仇,
                强制反馈.成年普通失败不结仇,
                强制反馈.未成年普通失败村民,
                强制反馈.未成年普通失败结仇,
                强制反馈.未成年普通失败不结仇);
        }

        public static string BuildAcceptedResultContent(bool ok, bool succeeded, bool 未成年)
        {
            if (!ok || !succeeded)
            {
                return 未成年 ? 亲密反馈.未成年结算失败 : 亲密反馈.成年结算失败;
            }

            return 未成年 ? 亲密反馈.未成年成功 : 亲密反馈.成年成功;
        }

        private static string SelectSuccessContent(
            bool 未成年,
            bool targetIsTaiwuVillager,
            string 成年普通Content,
            string 成年村民Content,
            string 未成年普通Content,
            string 未成年村民Content)
        {
            if (未成年)
            {
                return targetIsTaiwuVillager ? 未成年村民Content : 未成年普通Content;
            }

            return targetIsTaiwuVillager ? 成年村民Content : 成年普通Content;
        }

        private static string SelectFailureContent(
            bool 未成年,
            bool targetIsTaiwuVillager,
            bool appliedEnmity,
            string 成年村民Content,
            string 成年结仇Content,
            string 成年不结仇Content,
            string 未成年村民Content,
            string 未成年结仇Content,
            string 未成年不结仇Content)
        {
            if (未成年)
            {
                if (targetIsTaiwuVillager)
                {
                    return 未成年村民Content;
                }

                return appliedEnmity ? 未成年结仇Content : 未成年不结仇Content;
            }

            if (targetIsTaiwuVillager)
            {
                return 成年村民Content;
            }

            return appliedEnmity ? 成年结仇Content : 成年不结仇Content;
        }

        private static class 入口说明
        {
            public const string 成年亲密 = "<Character key=RoleTaiwu str=Name/>本与<Character key=CharacterId str=Name/>情好甚笃，二人平时耳鬓厮磨，几无顾忌。\n\n这一日<Character key=RoleTaiwu str=Name/>情动难耐，不由分说便将<Character key=CharacterId str=Name/>揽入怀中。<Character key=CharacterId str=Name/>怔了怔，到底不曾挣脱。";
            public const string 成年强制 = "<Character key=RoleTaiwu str=Name/>本与<Character key=CharacterId str=Name/>素无暧昧之情。孰料<Character key=RoleTaiwu str=Name/>忽生歹念，竟欲用强。<Character key=CharacterId str=Name/>惊怒交迸，奋力抵抗。<Character key=RoleTaiwu str=Name/>哪里肯收手，局面一触即发。";
            public const string 成年强制有护卫 = "<Character key=RoleTaiwu str=Name/>本与<Character key=CharacterId str=Name/>素无暧昧之情。孰料<Character key=RoleTaiwu str=Name/>忽生歹念，竟欲用强。<Character key=CharacterId str=Name/>惊怒交迸，奋力抵抗。\n\n正要动手，忽觉暗中似有旁人守着。若是继续动强，免不了要先与其交手。";
            public const string 未成年亲密 = "<Character key=RoleTaiwu str=Name/>这日寻着<Character key=CharacterId str=Name/>，言笑间越凑越近。<Character key=CharacterId str=Name/>并不十分闪避，<Character key=RoleTaiwu str=Name/>便轻轻握住了<Character key=CharacterId str=Name/>的手。\n\n<Character key=CharacterId str=Name/>一颤，抽了抽手，却没多少力气，只垂着头，连耳根都红透了。";
            public const string 未成年强制 = "<Character key=RoleTaiwu str=Name/>渐渐凑近<Character key=CharacterId str=Name/>，言语间已不甚规矩。<Character key=CharacterId str=Name/>觉出不对，直往后缩。<Character key=RoleTaiwu str=Name/>却不肯收手，仍只管逼上前去。\n\n<Character key=CharacterId str=Name/>退无可退，急得声音都变了。";
            public const string 未成年强制有护卫 = "<Character key=RoleTaiwu str=Name/>渐渐凑近<Character key=CharacterId str=Name/>，言语间已不甚规矩。<Character key=CharacterId str=Name/>觉出不对，直往后缩。<Character key=RoleTaiwu str=Name/>却不肯收手，仍只管逼上前去。\n\n<Character key=CharacterId str=Name/>退无可退，急得声音都变了。\n\n<Character key=RoleTaiwu str=Name/>正要再往前逼，忽然察觉暗中似有人照应。若是再上前一步，免不了要先与其交手。";
        }

        private static class 亲密反馈
        {
            public const string 成年成功 = "<Character key=RoleTaiwu str=Name/>与<Character key=CharacterId str=Name/>情意相投，此番亲密之事已经成就。";
            [异常兜底]
            public const string 成年结算失败 = "<Character key=RoleTaiwu str=Name/>与<Character key=CharacterId str=Name/>情意相投，但亲密结算未能完成。";
            public const string 未成年成功 = "<Character key=RoleTaiwu str=Name/>对尚未成年的<Character key=CharacterId str=Name/>情难自已；对方未作抗拒，此事已经按亲密路线结算。";
            [异常兜底]
            public const string 未成年结算失败 = "<Character key=RoleTaiwu str=Name/>对尚未成年的<Character key=CharacterId str=Name/>情难自已，但亲密结算未能完成。";
        }

        private static class 强制反馈
        {
            [异常兜底]
            public const string 成年结算失败 = "战斗已经结束，但成年强制路线的情难自已结算未能完成。";
            [异常兜底]
            public const string 未成年结算失败 = "战斗已经结束，但未成年强制路线的情难自已结算未能完成。";

            public const string 成年成功 = "战斗已经结束。你以武力压服了<Character key=CharacterId str=Name/>，成年强制路线的情难自已结果已经生效。";
            public const string 成年成功村民 = "战斗已经结束。你以武力压服了<Character key=CharacterId str=Name/>，成年强制路线的情难自已结果已经生效。对方身为太吾村民，未因此立刻与你结为仇敌。";
            public const string 未成年成功 = "战斗已经结束。你以武力压服了尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已结果已经生效。";
            public const string 未成年成功村民 = "战斗已经结束。你以武力压服了尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已结果已经生效。对方身为太吾村民，未因此立刻与你结为仇敌。";

            public const string 成年战死后成功 = "战斗已经结束。你压服了<Character key=CharacterId str=Name/>，成年强制路线的情难自已结果已经生效；随后对方伤重身死。";
            public const string 成年战死后成功村民 = "战斗已经结束。你压服了<Character key=CharacterId str=Name/>，成年强制路线的情难自已结果已经生效；随后对方伤重身死。对方身为太吾村民，未因此立刻与你结为仇敌。";
            public const string 未成年战死后成功 = "战斗已经结束。你压服了尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已结果已经生效；随后对方伤重身死。";
            public const string 未成年战死后成功村民 = "战斗已经结束。你压服了尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已结果已经生效；随后对方伤重身死。对方身为太吾村民，未因此立刻与你结为仇敌。";

            public const string 成年无力应战成功 = "<Character key=CharacterId str=Name/>已无力应战，无法再作抵抗；成年强制路线的情难自已结果已经生效。";
            public const string 成年无力应战成功村民 = "<Character key=CharacterId str=Name/>已无力应战，无法再作抵抗；成年强制路线的情难自已结果已经生效。对方身为太吾村民，未因此立刻与你结为仇敌。";
            public const string 未成年无力应战成功 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，无法再作抵抗；未成年强制路线的情难自已结果已经生效。";
            public const string 未成年无力应战成功村民 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，无法再作抵抗；未成年强制路线的情难自已结果已经生效。对方身为太吾村民，未因此立刻与你结为仇敌。";

            public const string 成年普通失败结仇 = "战斗已经结束。你未能压服<Character key=CharacterId str=Name/>，成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 成年普通失败不结仇 = "战斗已经结束。你未能压服<Character key=CharacterId str=Name/>，成年强制路线的情难自已未能达成。";
            public const string 成年普通失败村民 = "战斗已经结束。你未能压服<Character key=CharacterId str=Name/>，成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
            public const string 未成年普通失败结仇 = "战斗已经结束。你未能压服尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 未成年普通失败不结仇 = "战斗已经结束。你未能压服尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已未能达成。";
            public const string 未成年普通失败村民 = "战斗已经结束。你未能压服尚未成年的<Character key=CharacterId str=Name/>，未成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";

            public const string 成年无力应战结仇 = "<Character key=CharacterId str=Name/>已无力应战，成年强制路线无法再以此战斗压服对方；情难自已未能达成，但此事已经使双方结怨。";
            public const string 成年无力应战不结仇 = "<Character key=CharacterId str=Name/>已无力应战，成年强制路线无法再以此战斗压服对方；情难自已未能达成。";
            public const string 成年无力应战村民 = "<Character key=CharacterId str=Name/>已无力应战，成年强制路线无法再以此战斗压服对方；情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
            public const string 未成年无力应战结仇 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，未成年强制路线无法再以此战斗压服对方；情难自已未能达成，但此事已经使双方结怨。";
            public const string 未成年无力应战不结仇 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，未成年强制路线无法再以此战斗压服对方；情难自已未能达成。";
            public const string 未成年无力应战村民 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，未成年强制路线无法再以此战斗压服对方；情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";

            public const string 成年护卫拦截结仇 = "战斗已经结束。护卫阻拦在前，你未能接近<Character key=CharacterId str=Name/>；成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 成年护卫拦截不结仇 = "战斗已经结束。护卫阻拦在前，你未能接近<Character key=CharacterId str=Name/>；成年强制路线的情难自已未能达成。";
            public const string 成年护卫拦截村民 = "战斗已经结束。护卫阻拦在前，你未能接近<Character key=CharacterId str=Name/>；成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
            public const string 未成年护卫拦截结仇 = "战斗已经结束。护卫阻拦在前，你未能接近尚未成年的<Character key=CharacterId str=Name/>；未成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 未成年护卫拦截不结仇 = "战斗已经结束。护卫阻拦在前，你未能接近尚未成年的<Character key=CharacterId str=Name/>；未成年强制路线的情难自已未能达成。";
            public const string 未成年护卫拦截村民 = "战斗已经结束。护卫阻拦在前，你未能接近尚未成年的<Character key=CharacterId str=Name/>；未成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";

            public const string 成年目标逃走结仇 = "战斗已经结束。<Character key=CharacterId str=Name/>趁乱脱身，成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 成年目标逃走不结仇 = "战斗已经结束。<Character key=CharacterId str=Name/>趁乱脱身，成年强制路线的情难自已未能达成。";
            public const string 成年目标逃走村民 = "战斗已经结束。<Character key=CharacterId str=Name/>趁乱脱身，成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
            public const string 未成年目标逃走结仇 = "战斗已经结束。尚未成年的<Character key=CharacterId str=Name/>趁乱脱身，未成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 未成年目标逃走不结仇 = "战斗已经结束。尚未成年的<Character key=CharacterId str=Name/>趁乱脱身，未成年强制路线的情难自已未能达成。";
            public const string 未成年目标逃走村民 = "战斗已经结束。尚未成年的<Character key=CharacterId str=Name/>趁乱脱身，未成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";

            public const string 成年太吾撤退结仇 = "战斗已经结束。你已抽身而退，成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 成年太吾撤退不结仇 = "战斗已经结束。你已抽身而退，成年强制路线的情难自已未能达成。";
            public const string 成年太吾撤退村民 = "战斗已经结束。你已抽身而退，成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
            public const string 未成年太吾撤退结仇 = "战斗已经结束。你已抽身而退，未成年强制路线的情难自已未能达成，但此事已经使双方结怨。";
            public const string 未成年太吾撤退不结仇 = "战斗已经结束。你已抽身而退，未成年强制路线的情难自已未能达成。";
            public const string 未成年太吾撤退村民 = "战斗已经结束。你已抽身而退，未成年强制路线的情难自已未能达成。对方身为太吾村民，虽未立刻与你结为仇敌，心中仍难免芥蒂。";
        }

        [AttributeUsage(AttributeTargets.Field)]
        private sealed class 异常兜底Attribute : Attribute
        {
        }
    }
}
