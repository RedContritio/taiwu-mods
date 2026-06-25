return {
    Title = "战斗距离目标修复",
    Description = "修复战斗中目标距离条交互状态偶尔不随角色和队友指令变化刷新的问题，并修正距离条鼠标命中检测使用的 UI 相机。",
    Version = "1.0.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "1.0.20.0",
    FrontendPlugins = {
        "CombatTargetDistanceFix.Frontend.dll",
    },
    DefaultSettings = {
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
