return {
    Title = "EasyQuickSaveLoad",
    Description = "在 ESC 系统面板内提供存档、读档、快速存档和快速读档。覆盖主档前会展示保存时间和简略信息，存档可在同一太吾世界中保留多个回溯点。",
    Version = "1.0.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "cover.jpg",
    WorkshopCover = "cover.jpg",
    GameVersion = "1.0.56.0",
    FrontendPlugins = {
        "EasyQuickSaveLoad.Frontend.dll",
    },
    BackendPlugins = {
        "EasyQuickSaveLoad.Backend.dll",
    },
    DefaultSettings = {
        { SettingType = "Slider", Key = "SlotCount", DisplayName = "普通存档栏位数", Description = "可用的普通存档槽位数量", MinValue = 1, MaxValue = 99, StepSize = 1, DefaultValue = 20 },
        { SettingType = "Toggle", Key = "QuickSaveConfirm", DisplayName = "快速存档需要确认", Description = "快速存档前弹出确认对话框", DefaultValue = true },
        { SettingType = "Toggle", Key = "QuickLoadConfirm", DisplayName = "快速读档需要确认", Description = "快速读档前弹出确认对话框", DefaultValue = true },
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
