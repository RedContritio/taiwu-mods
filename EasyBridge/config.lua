return {
    Title = "EasyBridge",
    Description = "用于 LLM agent 的工具 Mod，可以让 ai 玩太吾绘卷。\n通过命名管道检视/操控界面、读写角色状态并执行临时 C#，辅助自动化验证。",
    Version = "0.0.1",
    Author = "RedContritio",
    Source = 0,
    Cover = "cover.jpg",
    WorkshopCover = "cover.jpg",
    GameVersion = "1.0.40",
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
