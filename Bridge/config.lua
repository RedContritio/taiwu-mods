return {
    Title = "Bridge",
    Description = "自动化测试桥：前端 UI 桥（taiwu-uibridge，检视/操控游戏 UI）+ 后端状态桥（taiwu-testbridge，读取/构造角色状态，含 /eval 动态执行任意 C#）。仅供开发调试使用。",
    Version = "0.0.1",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "",
    FrontendPlugins = {
        "Bridge.Frontend.dll",
    },
    BackendPlugins = {
        "Bridge.Backend.dll",
    },
    DefaultSettings = {
        { SettingType = "Toggle", Key = "Enabled", DisplayName = "启用桥", Description = "关闭后不创建前端/后端命名管道", DefaultValue = true },
        { SettingType = "Slider", Key = "DefaultMax", DisplayName = "精简模式条目上限", Description = "前端单窗口默认返回的控件/文本上限", MinValue = 10, MaxValue = 500, StepSize = 10, DefaultValue = 60 },
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
