return {
    Title = "情难自已",
    Description = "为玩家提供接口，主动触发角色之间的正式版强制行为。",
    Version = "2.0.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "",
    BackendPlugins = {
        "ForceEncounter.Backend.dll",
    },
    EventPackages = {
        "ForceEncounter.Events.dll",
    },
    DefaultSettings = {
        { SettingType = "Toggle", Key = "Enabled", DisplayName = "启用接口", DefaultValue = true },
        { SettingType = "Toggle", Key = "AllowTaiwuAsTarget", DisplayName = "允许太吾成为目标", Description = "默认关闭，避免误把太吾作为受害目标", DefaultValue = false },
        { SettingType = "Toggle", Key = "DebugMode", DisplayName = "调试模式", DefaultValue = false },
    },
    TagList = { "Extensions" },
    HasArchive = true,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
