using System;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventText
    {
        public static class 按钮
        {
            public const string 空 = "";
            public const string 情难自已 = "（情难自已……）";
            public const string 正常发生关系 = "（正常发生关系……）";
            public const string 强制关系 = "（强制关系……）";
            public const string 其他话题 = "其他话题";
            public const string 离开 = "离开";
            public const string 继续 = "（继续……）";
            public const string 公开关押擒获目标 = "（公开关押！）";
            public const string 秘密关押擒获目标 = "（秘密关押……）";
            public const string 放其离开擒获目标 = "（放其离开……）";
        }

        public static class 过场
        {
            public const string 护卫出面 = "对方的护卫挺身拦在你面前。";
        }

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

            if (succeeded && reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetCapturedInCombat)
            {
                return SelectSuccessContent(
                    未成年,
                    targetIsTaiwuVillager,
                    强制反馈.成年擒获后成功,
                    强制反馈.成年擒获后成功村民,
                    强制反馈.未成年擒获后成功,
                    强制反馈.未成年擒获后成功村民);
            }

            if (succeeded && reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetAlreadyPrisoner)
            {
                return SelectSuccessContent(
                    未成年,
                    targetIsTaiwuVillager,
                    强制反馈.成年已关押成功,
                    强制反馈.成年已关押成功村民,
                    强制反馈.未成年已关押成功,
                    强制反馈.未成年已关押成功村民);
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

        public static string BuildCapturedTargetDispositionContent()
        {
            return 擒获处置.选择;
        }

        public static string BuildCapturedTargetKeptContent()
        {
            return 擒获处置.关押;
        }

        public static string BuildCapturedTargetKeptSecretlyContent()
        {
            return 擒获处置.秘密关押;
        }

        public static string BuildCapturedTargetReleasedContent()
        {
            return 擒获处置.释放;
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
            public const string 成年成功 = "良久，<Character key=CharacterId str=Name/>才慢慢平复气息，仍倚在<Character key=RoleTaiwu str=Name/>身侧。二人虽一时无话，方才之事却已不必再言。";
            [异常兜底]
            public const string 成年结算失败 = "<Character key=RoleTaiwu str=Name/>与<Character key=CharacterId str=Name/>情意虽近，临到此时却忽然生出几分迟疑。二人相顾无言，只好暂且止住。";
            public const string 未成年成功 = "过了许久，<Character key=CharacterId str=Name/>仍低着头，耳根红意未褪，只轻轻攥着衣袖。方才之事既已发生，二人一时都不知该如何开口。";
            [异常兜底]
            public const string 未成年结算失败 = "<Character key=CharacterId str=Name/>低着头退开半步，神色惶惑不安。<Character key=RoleTaiwu str=Name/>一时也不知该如何继续，只得暂且停下。";
        }

        private static class 强制反馈
        {
            private const string 村民成功余波 = "对方身在太吾村中，仍将这份怨惧暂且压在心底。";
            private const string 村民失败余波 = "对方身在太吾村中，虽未当场决裂，此后只怕也难再坦然相对。";

            [异常兜底]
            public const string 成年结算失败 = "战斗虽已止息，事态却未能照预想收束。<Character key=RoleTaiwu str=Name/>与<Character key=CharacterId str=Name/>相对无言，一时竟不知该如何了结此事。";
            [异常兜底]
            public const string 未成年结算失败 = "争斗后的混乱很快散去，尚未成年的<Character key=CharacterId str=Name/>惊惶未定。<Character key=RoleTaiwu str=Name/>一时无从继续，只得暂且停下。";

            public const string 成年成功 = "尘埃落定，<Character key=CharacterId str=Name/>再无力抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 成年成功村民 = "尘埃落定，<Character key=CharacterId str=Name/>再无力抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;
            public const string 未成年成功 = "争斗止息，尚未成年的<Character key=CharacterId str=Name/>再无力挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 未成年成功村民 = "争斗止息，尚未成年的<Character key=CharacterId str=Name/>再无力挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;

            public const string 成年战死后成功 = "尘埃落定，<Character key=CharacterId str=Name/>再无力抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头；只是事后不久，对方便因伤势过重而气绝。";
            public const string 成年战死后成功村民 = "尘埃落定，<Character key=CharacterId str=Name/>再无力抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头；只是事后不久，对方便因伤势过重而气绝。" + 村民成功余波;
            public const string 未成年战死后成功 = "争斗止息，尚未成年的<Character key=CharacterId str=Name/>再无力挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头；只是事后不久，对方便因伤势过重而气绝。";
            public const string 未成年战死后成功村民 = "争斗止息，尚未成年的<Character key=CharacterId str=Name/>再无力挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头；只是事后不久，对方便因伤势过重而气绝。" + 村民成功余波;

            public const string 成年擒获后成功 = "绳索收紧，<Character key=CharacterId str=Name/>已被牢牢缚住，再难抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 成年擒获后成功村民 = "绳索收紧，<Character key=CharacterId str=Name/>已被牢牢缚住，再难抗拒。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;
            public const string 未成年擒获后成功 = "绳索收紧，尚未成年的<Character key=CharacterId str=Name/>已被牢牢缚住，再难挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 未成年擒获后成功村民 = "绳索收紧，尚未成年的<Character key=CharacterId str=Name/>已被牢牢缚住，再难挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;

            public const string 成年已关押成功 = "<Character key=CharacterId str=Name/>本已落在<Character key=RoleTaiwu str=Name/>掌中，纵有不甘，也无从抵抗。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 成年已关押成功村民 = "<Character key=CharacterId str=Name/>本已落在<Character key=RoleTaiwu str=Name/>掌中，纵有不甘，也无从抵抗。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;
            public const string 未成年已关押成功 = "尚未成年的<Character key=CharacterId str=Name/>本已落在<Character key=RoleTaiwu str=Name/>掌中，纵有不甘，也无从挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 未成年已关押成功村民 = "尚未成年的<Character key=CharacterId str=Name/>本已落在<Character key=RoleTaiwu str=Name/>掌中，纵有不甘，也无从挣开。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;

            public const string 成年无力应战成功 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 成年无力应战成功村民 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;
            public const string 未成年无力应战成功 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。";
            public const string 未成年无力应战成功村民 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。<Character key=RoleTaiwu str=Name/>终究遂了自己的念头。" + 村民成功余波;

            public const string 成年普通失败结仇 = "一场争斗过后，<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。此事未成，却已使双方结下怨仇。";
            public const string 成年普通失败不结仇 = "一场争斗过后，<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。此事只能暂且作罢。";
            public const string 成年普通失败村民 = "一场争斗过后，<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。" + 村民失败余波;
            public const string 未成年普通失败结仇 = "一场争斗过后，尚未成年的<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。此事未成，却已使双方结下怨仇。";
            public const string 未成年普通失败不结仇 = "一场争斗过后，尚未成年的<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。此事只能暂且作罢。";
            public const string 未成年普通失败村民 = "一场争斗过后，尚未成年的<Character key=CharacterId str=Name/>终究没有被<Character key=RoleTaiwu str=Name/>压服。" + 村民失败余波;

            public const string 成年无力应战结仇 = "<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。此事未成，却已使双方结下怨仇。";
            public const string 成年无力应战不结仇 = "<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。此事只能暂且作罢。";
            public const string 成年无力应战村民 = "<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。" + 村民失败余波;
            public const string 未成年无力应战结仇 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。此事未成，却已使双方结下怨仇。";
            public const string 未成年无力应战不结仇 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。此事只能暂且作罢。";
            public const string 未成年无力应战村民 = "尚未成年的<Character key=CharacterId str=Name/>已无力应战，这场争斗已无法再给<Character key=RoleTaiwu str=Name/>想要的结果。" + 村民失败余波;

            public const string 成年护卫拦截结仇 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。此事未成，却已使双方结下怨仇。";
            public const string 成年护卫拦截不结仇 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。此事只能暂且作罢。";
            public const string 成年护卫拦截村民 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。" + 村民失败余波;
            public const string 未成年护卫拦截结仇 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近尚未成年的<Character key=CharacterId str=Name/>的身。此事未成，却已使双方结下怨仇。";
            public const string 未成年护卫拦截不结仇 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近尚未成年的<Character key=CharacterId str=Name/>的身。此事只能暂且作罢。";
            public const string 未成年护卫拦截村民 = "护卫横在前面，<Character key=RoleTaiwu str=Name/>终究未能近尚未成年的<Character key=CharacterId str=Name/>的身。" + 村民失败余波;

            public const string 成年目标逃走结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去。此事未成，却已使双方结下怨仇。";
            public const string 成年目标逃走不结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去。此事只能暂且作罢。";
            public const string 成年目标逃走村民 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去。" + 村民失败余波;
            public const string 未成年目标逃走结仇 = "混乱之中，尚未成年的<Character key=CharacterId str=Name/>趁隙脱身而去。此事未成，却已使双方结下怨仇。";
            public const string 未成年目标逃走不结仇 = "混乱之中，尚未成年的<Character key=CharacterId str=Name/>趁隙脱身而去。此事只能暂且作罢。";
            public const string 未成年目标逃走村民 = "混乱之中，尚未成年的<Character key=CharacterId str=Name/>趁隙脱身而去。" + 村民失败余波;

            public const string 成年太吾撤退结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退，此事未成，却已使双方结下怨仇。";
            public const string 成年太吾撤退不结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退，此事只能暂且作罢。";
            public const string 成年太吾撤退村民 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。" + 村民失败余波;
            public const string 未成年太吾撤退结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退，此事未成，却已使双方结下怨仇。";
            public const string 未成年太吾撤退不结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退，此事只能暂且作罢。";
            public const string 未成年太吾撤退村民 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。" + 村民失败余波;
        }

        private static class 擒获处置
        {
            public const string 选择 = "<Character key=Prisoner str=Name/>已被绳索缚住，暂时无法脱身。此事既已了结，接下来便只看你要如何处置。";
            public const string 关押 = "<Character key=RoleTaiwu str=Name/>将<Character key=Prisoner str=Name/>押在身边，不许其离去。";
            public const string 秘密关押 = "<Character key=RoleTaiwu str=Name/>避开旁人耳目，将<Character key=Prisoner str=Name/>押在身边。";
            public const string 释放 = "<Character key=RoleTaiwu str=Name/>替<Character key=Prisoner str=Name/>解开束缚，任其离去。";
        }

        [AttributeUsage(AttributeTargets.Field)]
        private sealed class 异常兜底Attribute : Attribute
        {
        }
    }
}
