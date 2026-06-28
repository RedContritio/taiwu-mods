return {
    Title = "观音识蛐蛐",
    Description = "捕捉蛐蛐时，根据蛐蛐的叫声计算出品级，让蛐蛐叫声也有品级颜色。",
    Version = "1.0.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "cover.jpg",
    WorkshopCover = "cover.jpg",
    GameVersion = "1.0.20.0",
    FrontendPlugins = {
        "CricketSingGradeColor.Frontend.dll",
    },
    DefaultSettings = {
        { SettingType = "Dropdown", Key = "BlendMode",
          DisplayName = "声音品级权重",
          Description = "计算规则，音量优先最准确。",
          Options = { "音高优先", "均衡", "音量优先" },
          DefaultValue = 2 },
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
