return {
    Title = "EasyQuickSaveLoad",
    Description = "在 ESC 系统面板内提供存档、读档、快速存档和快速读档。覆盖主档前会展示保存时间和简略信息，存档可在同一太吾世界中保留多个回溯点。",
    Version = "1.0.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "1.0.44",
    FrontendPlugins = {
        "EasyQuickSaveLoad.Frontend.dll",
    },
    BackendPlugins = {
        "EasyQuickSaveLoad.Backend.dll",
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
