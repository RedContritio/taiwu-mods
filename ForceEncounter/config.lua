return {
    Title = "情难自已",
    Description = "为玩家提供接口，主动触发角色之间的正式版强制行为。",
    Version = "0.0.0.18",
    Author = "RedContritio",
    Source = 0,
    Cover = "",
    GameVersion = "1.0.10",
    BackendPlugins = {
        "ForceEncounter.Backend.dll",
    },
    EventPackages = {
        "ForceEncounter.Events.dll",
    },
    DefaultSettings = {
        { SettingType = "Toggle", Key = "Enabled", DisplayName = "启用接口", DefaultValue = true },
        { SettingType = "Slider", Key = "ForcedFavorabilityPenalty", DisplayName = "强制好感惩罚", Description = "普通强制路线对目标的好感惩罚；太吾村民按该数值的30%计算", MinValue = 0, MaxValue = 30000, StepSize = 100, DefaultValue = 30000 },
        { SettingType = "Toggle", Key = "ApplyAlertnessOnCombatStart", DisplayName = "开战增加戒心", Description = "进入强制战斗时调用原生敌对开战戒心效果", DefaultValue = true },
        { SettingType = "Toggle", Key = "DebugMode", DisplayName = "调试模式", DefaultValue = true },
    },
    TagList = { "Extensions" },
    HasArchive = true,
    ChangeConfig = false,
    NeedRestartWhenSettingChanged = false,
    Visibility = 0,
}
