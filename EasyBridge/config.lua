return {
    Title = "EasyBridge",
    Description = "用于 LLM agent 的工具 Mod，可以让 ai 玩太吾绘卷。\n通过命名管道检视/操控界面、读写角色状态，辅助自动化验证；任意 C# 执行和通用反射写入默认关闭，需要时通过 /config 显式启用。\n\n该 mod 的 agent skills 在 mod 目录中，在安装后，可以告诉 AI：\n我给太吾绘卷安装了 EasyBridge Mod，你去读这个 Mod 的目录内容，然后帮我开发我想要的 Mod。\n\n推荐用于 AI 开发 Mod，不推荐用于替自己行侠仗义拯救苍生。\n",
    Version = "0.2.0.0",
    Author = "RedContritio",
    Source = 0,
    Cover = "cover.jpg",
    WorkshopCover = "cover.jpg",
    GameVersion = "1.0.52",
    FrontendPlugins = {
        "EasyBridge.Frontend.dll",
    },
    BackendPlugins = {
        "EasyBridge.Backend.dll",
    },
    -- 无用户设置：这是 agent 驱动的调试桥，危险能力按请求传参 / 运行时 POST /config 显式开启。
    DefaultSettings = {},
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
    -- 创意工坊发布元数据（游戏上传后写入；持久化在此，避免重新部署时被覆盖丢失）。
    FileId = 3753794240,
    UpdateLogList = {
        {
            Timestamp = 1782742612,
        },
    },
}
