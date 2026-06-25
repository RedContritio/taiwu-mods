return {
    Title = "梦中情人",
    Description = "NPC主动对太吾产生爱慕、表白与求婚。",
    Version = "2.1.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "1.0.20",
    BackendPlugins = {
        "DreamLover.Backend.dll",
    },
    DefaultSettings = {
        -- 功能开关
        { SettingType = "Toggle", Key = "EnableEnamor", DisplayName = "主动爱慕", Description = "NPC主动对太吾产生倾心爱慕", DefaultValue = true },
        { SettingType = "Toggle", Key = "EnablePursued", DisplayName = "主动表白", Description = "爱慕太吾的NPC主动表白，成为两情相悦", DefaultValue = true },
        { SettingType = "Toggle", Key = "EnableMarry", DisplayName = "主动求婚", Description = "两情相悦的NPC主动求婚", DefaultValue = true },
        { SettingType = "Toggle", Key = "ForgetMe", DisplayName = "心去难留", Description = "襄王有意，神女无心。不被太吾回应的单恋者将断绝爱慕", DefaultValue = false },
        -- 性别
        { SettingType = "Toggle", Key = "AcceptSameGender", DisplayName = "接受同性", Description = "允许与太吾同性别的NPC追求（默认仅异性）", DefaultValue = false },
        { SettingType = "Toggle", Key = "IgnoreDistance", DisplayName = "爱慕不受地理限制", DefaultValue = false },
        -- 年龄
        { SettingType = "Slider", Key = "MinAge", DisplayName = "年龄下限", MinValue = 16, MaxValue = 100, StepSize = 1, DefaultValue = 16 },
        { SettingType = "Slider", Key = "MaxAge", DisplayName = "年龄上限", MinValue = 16, MaxValue = 100, StepSize = 1, DefaultValue = 60 },
        -- 好感度（只追求好感在[下限,上限]区间内的NPC；两端取到底=不限好感，两端同档=仅该档）
        { SettingType = "Dropdown", Key = "FavorMin", DisplayName = "好感-下限", Description = "只追求好感在[下限,上限]区间内的NPC（含两端）；与上限取到两端(血仇~不渝)=不限好感", Options = { "血仇", "痛恨", "憎恨", "仇视", "敌视", "鄙视", "陌路", "冷淡", "融洽", "热忱", "喜爱", "亲密", "不渝" }, DefaultValue = 8 },
        { SettingType = "Dropdown", Key = "FavorMax", DisplayName = "好感-上限", Options = { "血仇", "痛恨", "憎恨", "仇视", "敌视", "鄙视", "陌路", "冷淡", "融洽", "热忱", "喜爱", "亲密", "不渝" }, DefaultValue = 12 },
        -- 立场
        { SettingType = "Dropdown", Key = "GoodMin", DisplayName = "立场-下限", Description = "只追求立场在[下限,上限]区间内的NPC（含两端）；取到两端(刚正~唯我)=不限立场", Options = { "刚正", "仁善", "中庸", "叛逆", "唯我" }, DefaultValue = 1 },
        { SettingType = "Dropdown", Key = "GoodMax", DisplayName = "立场-上限", Options = { "刚正", "仁善", "中庸", "叛逆", "唯我" }, DefaultValue = 2 },
        -- 魅力
        { SettingType = "Dropdown", Key = "CharmMin", DisplayName = "魅力-下限", Description = "只追求魅力在[下限,上限]区间内的NPC（含两端）；取到两端(非人~天人)=不限魅力", Options = { "非人", "可憎", "不扬", "寻常", "出众", "瑶碧·瑾瑜", "凤仪·龙姿", "出尘·绝世", "天人" }, DefaultValue = 4 },
        { SettingType = "Dropdown", Key = "CharmMax", DisplayName = "魅力-上限", Options = { "非人", "可憎", "不扬", "寻常", "出众", "瑶碧·瑾瑜", "凤仪·龙姿", "出尘·绝世", "天人" }, DefaultValue = 8 },
        -- 阶层（品级，九品最低、一品最高）
        { SettingType = "Dropdown", Key = "RankMin", DisplayName = "阶层-下限", Description = "只追求品级在[下限,上限]区间内的NPC（含两端）；取到两端(九品~一品)=不限阶层", Options = { "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品" }, DefaultValue = 2 },
        { SettingType = "Dropdown", Key = "RankMax", DisplayName = "阶层-上限", Options = { "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品" }, DefaultValue = 8 },
        -- 入魔状态
        { SettingType = "Dropdown", Key = "InfectMin", DisplayName = "入魔-下限", Description = "只追求入魔程度在[下限,上限]区间内的NPC（含两端）；取到两端(未入邪~相枢化魔)=不限入魔", Options = { "未入邪", "相枢入邪", "相枢化魔" }, DefaultValue = 0 },
        { SettingType = "Dropdown", Key = "InfectMax", DisplayName = "入魔-上限", Options = { "未入邪", "相枢入邪", "相枢化魔" }, DefaultValue = 0 },
        -- 关系过滤（勾选=允许与太吾有该关系的NPC追求太吾；按太吾视角命名）
        { SettingType = "Toggle", Key = "Rel_BloodParent", DisplayName = "关系-太吾亲生父母", Description = "允许太吾的亲生父母追求太吾", DefaultValue = false },
        { SettingType = "Toggle", Key = "Rel_BloodChild", DisplayName = "关系-太吾亲生子女", Description = "允许太吾的亲生子女追求太吾", DefaultValue = false },
        { SettingType = "Toggle", Key = "Rel_BloodSibling", DisplayName = "关系-太吾亲生兄妹", Description = "允许太吾的亲生兄妹追求太吾", DefaultValue = false },
        { SettingType = "Toggle", Key = "Rel_AdoptiveParent", DisplayName = "关系-太吾义父义母", Description = "允许太吾的义父义母追求太吾", DefaultValue = false },
        { SettingType = "Toggle", Key = "Rel_AdoptiveChild", DisplayName = "关系-太吾义子义女", Description = "允许太吾的义子义女追求太吾", DefaultValue = false },
        { SettingType = "Toggle", Key = "Rel_SwornSibling", DisplayName = "关系-义结金兰", DefaultValue = true },
        { SettingType = "Toggle", Key = "Rel_Mentor", DisplayName = "关系-授业恩师", DefaultValue = true },
        { SettingType = "Toggle", Key = "Rel_Mentee", DisplayName = "关系-弟子传人", DefaultValue = true },
        { SettingType = "Toggle", Key = "Rel_Friend", DisplayName = "关系-知心之交", DefaultValue = true },
        { SettingType = "Toggle", Key = "Rel_Adored", DisplayName = "关系-已有爱慕", DefaultValue = true },
        { SettingType = "Toggle", Key = "Rel_Enemy", DisplayName = "关系-敌对", DefaultValue = true },
        -- 求婚选项
        { SettingType = "Toggle", Key = "IgnoreGang", DisplayName = "无视门派规章", Description = "允许门派禁婚的NPC求婚", DefaultValue = false },
        { SettingType = "Toggle", Key = "MarriedKiller", DisplayName = "已婚人士", Description = "保留项：正式版婚姻规则仍会阻止已婚NPC结婚", DefaultValue = false },
        { SettingType = "Toggle", Key = "Polygynous", DisplayName = "太吾多配偶制", Description = "保留项：正式版婚姻规则仍会阻止太吾重婚", DefaultValue = false },
        { SettingType = "Toggle", Key = "MonkKiller", DisplayName = "出家人", Description = "允许出家身份的NPC求婚", DefaultValue = false },
        { SettingType = "Toggle", Key = "CharmingBonze", DisplayName = "太吾出家也要求婚", DefaultValue = false },
        -- 调试
        { SettingType = "Toggle", Key = "DebugMode", DisplayName = "调试模式", DefaultValue = false },
    },
    TagList = { "Extensions" },
    HasArchive = true,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
