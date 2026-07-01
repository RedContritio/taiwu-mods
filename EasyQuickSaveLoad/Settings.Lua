return {
    { SettingType = "Slider", Key = "SlotCount", DisplayName = "普通存档栏位数", Description = "可用的普通存档槽位数量", MinValue = 1, MaxValue = 99, StepSize = 1, DefaultValue = 20 },
    { SettingType = "Toggle", Key = "QuickSaveConfirm", DisplayName = "快速存档需要确认", Description = "快速存档前弹出确认对话框", DefaultValue = true },
    { SettingType = "Toggle", Key = "QuickLoadConfirm", DisplayName = "快速读档需要确认", Description = "快速读档前弹出确认对话框", DefaultValue = true },
}
