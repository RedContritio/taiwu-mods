namespace ForceEncounter.Events
{
    internal static class ForceEncounterPrisonerText
    {
        public static string 构造内层说明(bool 亲密通过, bool 未成年)
        {
            if (亲密通过)
            {
                return 未成年 ? 入口说明.未成年亲密 : 入口说明.成年亲密;
            }

            return 未成年 ? 入口说明.未成年强制 : 入口说明.成年强制;
        }

        public static string BuildForcedResultContent(bool targetIsTaiwuVillager, bool 未成年)
        {
            if (未成年)
            {
                return targetIsTaiwuVillager ? 强制反馈.未成年成功村民 : 强制反馈.未成年成功;
            }

            return targetIsTaiwuVillager ? 强制反馈.成年成功村民 : 强制反馈.成年成功;
        }

        public static string BuildAcceptedResultContent(bool ok, bool succeeded, bool 未成年)
        {
            if (!ok || !succeeded)
            {
                return 未成年 ? 亲密反馈.未成年结算失败 : 亲密反馈.成年结算失败;
            }

            return 未成年 ? 亲密反馈.未成年成功 : 亲密反馈.成年成功;
        }

        private static class 入口说明
        {
            public const string 成年亲密 = "<Character key=CharacterId str=Name/>虽被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，对<Character key=RoleTaiwu str=Name/>却素无怨怼之心。\n\n这一日<Character key=RoleTaiwu str=Name/>情动难耐，俯身将<Character key=CharacterId str=Name/>揽入怀中。<Character key=CharacterId str=Name/>怔了怔，到底没有别过脸去。";
            public const string 未成年亲密 = "<Character key=CharacterId str=Name/>被缚着手，蜷在<Character key=RoleTaiwu str=Name/>身边，见<Character key=RoleTaiwu str=Name/>凑近，并不十分闪躲。\n\n<Character key=RoleTaiwu str=Name/>握住<Character key=CharacterId str=Name/>的手，<Character key=CharacterId str=Name/>一颤，垂着头，连耳根都红透了。";
            public const string 成年强制 = "<Character key=CharacterId str=Name/>早已被缚住手脚，押在<Character key=RoleTaiwu str=Name/>面前，纵有满腔不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>居高临下地看着，眼底渐渐起了异样的念头。";
            public const string 未成年强制 = "<Character key=CharacterId str=Name/>早已被缚住手脚，蜷在角落里。见<Character key=RoleTaiwu str=Name/>一步步逼近，<Character key=CharacterId str=Name/>吓得直往墙根缩，却退无可退，无从抗拒。";
        }

        private static class 强制反馈
        {
            public const string 成年成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续，绳索偶尔挣动，却终究挣不脱。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息。";
            public const string 成年成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从抗拒。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，欺近身前。衣料窸窣间，只听得压低的呜咽时断时续。绳索只偶尔挣动了一下，便再没了声息。待到一切停歇，绳索仍未解开，昏暗里只余细碎的喘息，连喘息都不敢太重，唯恐再生事端。";
            public const string 未成年成功 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索偶尔挣动，却终是徒劳，细弱的哭声时而被堵住似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡。";
            public const string 未成年成功村民 = "<Character key=CharacterId str=Name/>本已被缚住双手，押在<Character key=RoleTaiwu str=Name/>身边，纵有不甘，此刻也无从挣开。\n\n<Character key=RoleTaiwu str=Name/>没有就此收手，仍然逼近。绳索只偶尔挣动了一下便再没了声息，细弱的哭声时而被自己捂住了似的闷下去，久久才渐次平息。待到一切停歇，绳索仍未解开，只剩细细的抽泣在暗处回荡，连抽泣都死死压在喉咙里。";
        }

        private static class 亲密反馈
        {
            public const string 成年成功 = "也不知过了多久，二人才渐渐安顿下来。<Character key=CharacterId str=Name/>手上的绳索仍未解开，鬓发散乱，面上红潮未褪，却只侧身倚着<Character key=RoleTaiwu str=Name/>，半晌不曾开口。\n\n<Character key=RoleTaiwu str=Name/>伸手替<Character key=CharacterId str=Name/>拢了拢碎发，又把被绳索勒红的手腕轻轻揉开。<Character key=CharacterId str=Name/>微微一缩，随即又松弛下来，连呼吸都轻了。";
            public const string 成年结算失败 = "孰料临到此时，<Character key=CharacterId str=Name/>却忽然偏过头去，缚着的手不安地动了动，呼吸急促了几分。<Character key=RoleTaiwu str=Name/>怔了怔，到底不忍勉强，只替<Character key=CharacterId str=Name/>拢了拢散乱的衣裳，低低说了句什么。绳索未解，二人便这么依偎着，许久没有再进一步。";
            public const string 未成年成功 = "过了许久，<Character key=CharacterId str=Name/>仍蜷着身子，缚着的双手搁在膝上，耳根的红一路蔓延到颈侧。<Character key=RoleTaiwu str=Name/>替<Character key=CharacterId str=Name/>理了理揉皱的衣领，<Character key=CharacterId str=Name/>这才抬起眼飞快地看了一眼，又迅速垂下目光，被缚的手指却悄悄勾住了<Character key=RoleTaiwu str=Name/>的袖角。";
            public const string 未成年结算失败 = "<Character key=CharacterId str=Name/>忽然偏过头去，缚着的手往怀里缩了缩，眼眶微微泛红，呼吸又急又浅。<Character key=RoleTaiwu str=Name/>伸出的手顿在半空，半晌叹了口气，替<Character key=CharacterId str=Name/>把滑落的衣带重新系好，不再迫近。";
        }
    }
}
