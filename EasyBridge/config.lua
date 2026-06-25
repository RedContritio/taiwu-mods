return {
    Title = "EasyBridge",
    Description = "自动化测试桥：前端 UI 桥（easybridge-ui，检视/操控游戏 UI）+ 后端状态桥（easybridge-state，读取/构造角色状态，含 /eval 动态执行任意 C#）。仅供开发调试使用。",
    Version = "0.0.1",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "",
    FrontendPlugins = {
        "EasyBridge.Frontend.dll",
    },
    BackendPlugins = {
        "EasyBridge.Backend.dll",
    },
    DefaultSettings = {
        { SettingType = "Slider", Key = "DefaultMax", DisplayName = "精简模式条目上限", Description = "前端单窗口默认返回的控件/文本上限", MinValue = 10, MaxValue = 500, StepSize = 10, DefaultValue = 60 },
        { SettingType = "Slider", Key = "MaxReflectDepth", DisplayName = "反射快照深度", Description = "前端 /reflect 返回对象字段时递归展开的最大深度", MinValue = 0, MaxValue = 3, StepSize = 1, DefaultValue = 2 },
        { SettingType = "Slider", Key = "MaxReflectMembers", DisplayName = "反射成员上限", Description = "前端 /inspect 与 /reflect 单次返回的最大组件/字段数量", MinValue = 1, MaxValue = 200, StepSize = 1, DefaultValue = 80 },
        { SettingType = "Toggle", Key = "EnableReflectInvoke", DisplayName = "允许反射调用方法", Description = "仅临时调试时开启；开启后 /reflect/invoke 可调用前端实例方法", DefaultValue = false },
    },
    TagList = {
        "Extensions",
    },
    HasArchive = false,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
