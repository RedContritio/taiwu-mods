using System;

namespace ForceEncounter.Events
{
    internal static class ForceEncounterEventText
    {
        public static class 按钮
        {
            public const string 空 = "";
            public const string 情难自已 = "（情难自已……）";
            public const string 正常发生关系 = "（半推半就……）";
            public const string 强制关系 = "（更进一步……）";
            public const string 其他话题 = "其他话题";
            public const string 离开 = "（念头通达……）";
            public const string 继续 = "（继续……）";
            public const string 公开关押擒获目标 = "（公开关押！）";
            public const string 秘密关押擒获目标 = "（秘密关押……）";
            public const string 放其离开擒获目标 = "（放其离开……）";
        }

        public static class 过场
        {
            public const string 护卫出面 = "忽有一人挺身而出，拦在了前面。";
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
            public const string 成年成功 = "也不知过了多久，二人才渐渐安顿下来。<Character key=CharacterId str=Name/>鬓发散乱，面上红潮未褪，只侧身倚在<Character key=RoleTaiwu str=Name/>肩头，半晌不曾开口。\n\n<Character key=RoleTaiwu str=Name/>伸手替<Character key=CharacterId str=Name/>拢了拢碎发，指节擦过耳际时，<Character key=CharacterId str=Name/>微微一缩，随即又松弛下来，连呼吸都轻了。";
            [异常兜底]
            public const string 成年结算失败 = "孰料临到此时，<Character key=CharacterId str=Name/>却忽然偏过头去，呼吸急促了几分，一只手轻轻抵在<Character key=RoleTaiwu str=Name/>胸前。<Character key=RoleTaiwu str=Name/>怔了怔，到底不忍勉强，只握住那只手，低低说了句什么。二人便这么依偎着，许久没有再进一步。";
            public const string 未成年成功 = "过了许久，<Character key=CharacterId str=Name/>仍蜷着身子不敢动，耳根的红一路蔓延到颈侧。方才的一切像场突如其来的暴雨，<Character key=CharacterId str=Name/>还没想明白，却已浑身湿透。\n\n<Character key=RoleTaiwu str=Name/>替<Character key=CharacterId str=Name/>理了理揉皱的衣领，<Character key=CharacterId str=Name/>这才抬起眼飞快地看了一眼，又迅速垂下目光，手指却悄悄勾住了<Character key=RoleTaiwu str=Name/>的袖角。";
            [异常兜底]
            public const string 未成年结算失败 = "<Character key=CharacterId str=Name/>忽然往后退了半步，眼眶微微泛红，呼吸又急又浅，像被什么吓着了。<Character key=RoleTaiwu str=Name/>伸出的手顿在半空，见<Character key=CharacterId str=Name/>整个人都快缩成一团，心中忽生不忍。半晌，<Character key=RoleTaiwu str=Name/>叹了口气，替<Character key=CharacterId str=Name/>把滑落的衣带重新系好，不再迫近。";
        }

        private static class 强制反馈
        {
            private const string 村民成功余波 = "";
            private const string 村民失败余波 = "";

            [异常兜底]
            public const string 成年结算失败 = "一番争斗过后，<Character key=CharacterId str=Name/>仍强撑着没有倒下，衣衫凌乱，喘息未定，只死死盯着<Character key=RoleTaiwu str=Name/>。\n\n<Character key=RoleTaiwu str=Name/>虽仍不肯就此罢手，却终究没能再逼近一步。";
            [异常兜底]
            public const string 未成年结算失败 = "一番争斗过后，<Character key=CharacterId str=Name/>缩在角落里，抱紧双臂，浑身仍在发颤。\n\n<Character key=RoleTaiwu str=Name/>虽仍不肯就此罢手，却终究没能再逼近一步。";

            public const string 成年成功 = "争斗结束了。<Character key=CharacterId str=Name/>伏在地上，浑身微微发抖，却没有再挣扎的气力。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，一步一步走近前去。衣料窸窣，混着断续的呜咽与破碎的喘息，在暗处响了许久。待到一切终于停歇，只余下压抑不住的低泣，断断续续。";
            public const string 成年成功村民 = "争斗结束了。<Character key=CharacterId str=Name/>伏在地上，浑身微微发抖，却没有再挣扎的气力。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，一步一步走近前去。衣料窸窣，混着断续的呜咽与破碎的喘息，在暗处响了许久。早已没了多少挣扎，只余下细碎的抽噎。待到一切终于停歇，只余下压抑不住的低泣，却死死忍着，不敢放出声来。";
            public const string 未成年成功 = "争斗止息。<Character key=CharacterId str=Name/>缩在角落里，胳膊仍在发抖，已连退避的力气都使不出来。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼上前去。昏暗里只听得见细弱的哭声，时而急促，时而被人捂住了似的闷下去，久久才渐次平息。待到一切停歇，只剩细细的抽泣，像被掐住了喉咙。";
            public const string 未成年成功村民 = "争斗止息。<Character key=CharacterId str=Name/>缩在角落里，胳膊仍在发抖，已连退避的力气都使不出来。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼上前去。昏暗里只听得见细弱的哭声，时而急促，时而自己捂住了嘴似的闷下去，久久才渐次平息。早已不敢挣动，只缩着身子发抖。待到一切停歇，只剩细细的抽泣，像被掐住了喉咙，却连抽泣都捂在掌心里，唯恐被人听见。";

            public const string 成年战死后成功 = "争斗过后，<Character key=CharacterId str=Name/>已无力再起。\n\n<Character key=RoleTaiwu str=Name/>没有就此停手，欺身而上。衣料窸窣与断续的低泣在暗处响了许久，<Character key=CharacterId str=Name/>的喘息却越来越弱，终因伤势过重而气绝。";
            public const string 成年战死后成功村民 = "争斗过后，<Character key=CharacterId str=Name/>已无力再起。\n\n<Character key=RoleTaiwu str=Name/>没有就此停手，欺身而上。衣料窸窣与断续的低泣在暗处响了许久，其间偶有微弱的挣动，却旋即便止。<Character key=CharacterId str=Name/>的喘息却越来越弱，终因伤势过重而气绝。四下寂静，无人知晓，也无人赶来。";
            public const string 未成年战死后成功 = "争斗过后，<Character key=CharacterId str=Name/>已无力再起。\n\n<Character key=RoleTaiwu str=Name/>没有就此停手，仍然逼近。昏暗里只听得见细弱的哭声时高时低，久久不停。待到哭声渐弱，<Character key=CharacterId str=Name/>的呼吸也已越来越轻，终因伤势过重而气绝。";
            public const string 未成年战死后成功村民 = "争斗过后，<Character key=CharacterId str=Name/>已无力再起。\n\n<Character key=RoleTaiwu str=Name/>没有就此停手，仍然逼近。昏暗里只听得见细弱的哭声时高时低，久久不停，其间偶有微弱的挣动，却旋即便止。待到哭声渐弱，<Character key=CharacterId str=Name/>的呼吸也已越来越轻，终因伤势过重而气绝。四下寂静，无人知晓，也无人赶来。";

            public const string 成年擒获后成功 = "绳索缚得极紧，<Character key=CharacterId str=Name/>挣了几下便不动了。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。绳索勒动的声音混着断续的抽噎，在暗处响了许久。待到一切终于停歇，绳索仍未松开，昏暗里只余断断续续的呜咽。";
            public const string 成年擒获后成功村民 = "绳索缚得极紧，<Character key=CharacterId str=Name/>挣了几下便不动了。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。绳索勒动的声音混着断续的抽噎，在暗处响了许久。早已不再挣动，只任绳索勒着。待到一切终于停歇，绳索仍未松开，昏暗里只余断断续续的呜咽，却不敢抬高声嗓。";
            public const string 未成年擒获后成功 = "绳索缚得极紧，<Character key=CharacterId str=Name/>挣了几下便不动了。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索勒动的声音混着细弱的哭声，时高时低，在暗处响了许久。待到一切停歇，绳索仍未松开，只剩断断续续的抽泣在暗处回荡。";
            public const string 未成年擒获后成功村民 = "绳索缚得极紧，<Character key=CharacterId str=Name/>挣了几下便不动了。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索勒动的声音混着细弱的哭声，时高时低，在暗处响了许久。早已不再挣动，只任绳索勒着，缩成一团。待到一切停歇，绳索仍未松开，只剩断断续续的抽泣在暗处回荡，却连回声都散不远，被黑暗吞尽了似的。";

            public const string 成年已关押成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续，绳索偶尔挣动，却终究挣不脱。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息。";
            public const string 成年已关押成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续。绳索只偶尔挣动了一下，便再没了声息。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息，连喘息都不敢太重，唯恐再生事端。";
            public const string 未成年已关押成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索偶尔挣动，却终是徒劳，细弱的哭声时而被堵住似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡。";
            public const string 未成年已关押成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索只偶尔挣动了一下便再没了声息，细弱的哭声时而被自己捂住了似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡，连抽泣都死死压在喉咙里。";

            public const string 成年无力应战成功 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，一步一步走近前去。衣料窸窣与破碎的喘息在暗处响了许久，偶有微弱的推拒，却终是徒劳。待到一切终于停歇，只余下细碎的低泣，沉入昏暗。";
            public const string 成年无力应战成功村民 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，一步一步走近前去。衣料窸窣与破碎的喘息在暗处响了许久，其间推拒渐渐弱了下去，终至不再动弹。待到一切终于停歇，只余下细碎的低泣，沉入昏暗，却一声也不敢抬高。";
            public const string 未成年无力应战成功 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼上前去。昏暗里只听得见细弱的哭声时高时低，偶有挣动，却终是徒劳，久久才渐次平息。待到一切停歇，只剩细细的抽泣，像被掐住了喉咙。";
            public const string 未成年无力应战成功村民 = "<Character key=CharacterId str=Name/>已无力应战，连退避都显得勉强。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼上前去。昏暗里只听得见细弱的哭声时高时低，挣动渐渐弱了下去，久久才渐次平息。待到一切停歇，只剩细细的抽泣，像被掐住了喉咙，却连抽泣都捂在掌心里，唯恐被人听见。";

            public const string 成年普通失败结仇 = "争斗过后，<Character key=CharacterId str=Name/>挣脱了<Character key=RoleTaiwu str=Name/>的压制，踉跄退开数步，狠狠瞪了一眼，捂着手臂走了，再也没有回头。";
            public const string 成年普通失败不结仇 = "争斗过后，<Character key=CharacterId str=Name/>挣脱了<Character key=RoleTaiwu str=Name/>的压制，踉跄退开数步。此事只得暂且作罢。";
            public const string 成年普通失败村民 = "争斗过后，<Character key=CharacterId str=Name/>挣脱了<Character key=RoleTaiwu str=Name/>的压制，踉跄退开数步，却不敢多说什么，只低着头退开了。";
            public const string 未成年普通失败结仇 = "争斗过后，<Character key=CharacterId str=Name/>总算挣脱了<Character key=RoleTaiwu str=Name/>，缩着身子退到远处，回头看了一眼，眼里满是委屈与恨意，随即转身跑了。";
            public const string 未成年普通失败不结仇 = "争斗过后，<Character key=CharacterId str=Name/>总算挣脱了<Character key=RoleTaiwu str=Name/>，缩着身子退到远处，浑身仍在发抖。此事只得暂且作罢。";
            public const string 未成年普通失败村民 = "争斗过后，<Character key=CharacterId str=Name/>总算挣脱了<Character key=RoleTaiwu str=Name/>，缩着身子退到远处，却不敢哭出声来，只把脸埋在膝盖里。";

            public const string 成年护卫拦截结仇 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。\n\n<Character key=CharacterId str=Name/>在来人身后冷冷望了<Character key=RoleTaiwu str=Name/>一眼，转身便走。";
            public const string 成年护卫拦截不结仇 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。此事只好暂且搁下。";
            public const string 成年护卫拦截村民 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。\n\n<Character key=CharacterId str=Name/>躲在来人身后，不敢朝这边看。";
            public const string 未成年护卫拦截结仇 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。\n\n<Character key=CharacterId str=Name/>躲在来人身后，眼里却全是恨意。";
            public const string 未成年护卫拦截不结仇 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。此事只好暂且搁下。";
            public const string 未成年护卫拦截村民 = "一人挺身拦在前面，<Character key=RoleTaiwu str=Name/>终究未能近<Character key=CharacterId str=Name/>的身。\n\n<Character key=CharacterId str=Name/>躲在来人身后，死死抓着来人的衣角，不敢出声。";

            public const string 成年目标逃走结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，转眼便没了踪影，连头也不回。";
            public const string 成年目标逃走不结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，转眼便没了踪影。此事只好暂且搁下。";
            public const string 成年目标逃走村民 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，转眼便没了踪影。脚步声渐渐远了，四周复归寂静。";
            public const string 未成年目标逃走结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，头也不回地逃远了。";
            public const string 未成年目标逃走不结仇 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，头也不回地逃远了。此事只好暂且搁下。";
            public const string 未成年目标逃走村民 = "混乱之中，<Character key=CharacterId str=Name/>趁隙脱身而去，头也不回地逃远了。脚步声渐渐远了，四周复归寂静。";

            public const string 成年太吾撤退结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>盯着那道背影看了许久，才咬牙转身离去。";
            public const string 成年太吾撤退不结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>站在原地，半晌才松了口气，也转身走了。";
            public const string 成年太吾撤退村民 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>立在原地，没有出声，等那道身影彻底消失才敢挪步。";
            public const string 未成年太吾撤退结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>盯着那道背影看了许久，才咬着嘴唇转身走了。";
            public const string 未成年太吾撤退不结仇 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>站在原地，半晌才回过神来，也转身跑了。";
            public const string 未成年太吾撤退村民 = "<Character key=RoleTaiwu str=Name/>先一步抽身而退。\n\n<Character key=CharacterId str=Name/>立在原地，没有出声，等那道身影彻底消失才敢挪步。";
        }

        private static class 擒获处置
        {
            public const string 选择 = "<Character key=RoleTaiwu str=Name/>将<Character key=Prisoner str=Name/>击倒在地，用绳索五花大绑，使其无法脱身。\n\n<Character key=RoleTaiwu str=Name/>停下手来，打量着眼前的人。";
            public const string 关押 = "<Character key=RoleTaiwu str=Name/>收紧绳索，将<Character key=Prisoner str=Name/>押在身边，不许离去。\n\n<Character key=Prisoner str=Name/>低着头，一言不发地跟在了后面。";
            public const string 秘密关押 = "<Character key=RoleTaiwu str=Name/>四下环顾，见无人注意，便将<Character key=Prisoner str=Name/>押在身边。\n\n<Character key=Prisoner str=Name/>连哭都不敢出声，只默默跟着。";
            public const string 释放 = "<Character key=RoleTaiwu str=Name/>替<Character key=Prisoner str=Name/>解开绳索。\n\n<Character key=Prisoner str=Name/>愣了一瞬，随即踉跄着退开几步，头也不回地走了。";
        }

        [AttributeUsage(AttributeTargets.Field)]
        private sealed class 异常兜底Attribute : Attribute
        {
        }
    }
}
