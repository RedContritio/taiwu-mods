return {
    Title = "EasyBridge",
    Description = "自动化测试桥：前端 UI 桥（easybridge-ui，检视/操控游戏 UI）+ 后端状态桥（easybridge-state，读取/构造角色状态，含 /eval 动态执行任意 C#）。仅供开发调试使用。",
    Version = "0.0.1",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "1.0.32",
    FrontendPlugins = {
        "EasyBridge.Frontend.dll",
    },
    BackendPlugins = {
        "EasyBridge.Backend.dll",
    },
    -- 无用户设置：这是 agent 驱动的调试桥，行为按请求传参 / 运行时 POST /config 控制。
    DefaultSettings = {},
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
