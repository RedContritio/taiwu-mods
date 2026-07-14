return {
	Title = "旮旯太吾快速SL",
	Description = "在 ESC 界面内，按照旮旯给姆的风格，提供存档、读档、快速存档和快速读档功能。",
	Version = "1.0.0.0",
	Author = "RedContritio",
	Source = 0,
	Cover = "cover.jpg",
	WorkshopCover = "cover.jpg",
	GameVersion = "1.0.58.0",
	FrontendPlugins = {
		[1] = "EasyQuickSaveLoad.Frontend.dll",
	},
	BackendPlugins = {
		[1] = "EasyQuickSaveLoad.Backend.dll",
	},
	DefaultSettings = {
		[1] = {
			SettingType = "Slider",
			Key = "PageCount",
			DisplayName = "可用存档页数",
			Description = "存档/读档界面可用的页数，每页 5 个栏位。",
			GroupName = "Default",
			MinValue = 1,
			MaxValue = 10,
			StepSize = 1,
			DefaultValue = 4,
		},
		[2] = {
			SettingType = "Toggle",
			Key = "QuickSaveConfirm",
			DisplayName = "快速存档需要确认",
			Description = "快速存档前弹出确认对话框",
			GroupName = "Default",
			DefaultValue = true,
		},
		[3] = {
			SettingType = "Toggle",
			Key = "QuickLoadConfirm",
			DisplayName = "快速读档需要确认",
			Description = "快速读档前弹出确认对话框",
			GroupName = "Default",
			DefaultValue = true,
		},
		[4] = {
			SettingType = "Toggle",
			Key = "OverwriteConfirm",
			DisplayName = "覆盖存档需要确认",
			Description = "覆盖已有存档前弹出确认对话框",
			GroupName = "Default",
			DefaultValue = true,
		},
		[5] = {
			SettingType = "Toggle",
			Key = "DeleteConfirm",
			DisplayName = "删除存档需要确认",
			Description = "删除存档前弹出确认对话框",
			GroupName = "Default",
			DefaultValue = true,
		},
	},
	TagList = {
		[1] = "Extensions",
	},
	HasArchive = false,
	ChangeConfig = false,
	NeedRestartWhenSettingChanged = false,
	Visibility = 0,
	FileId = 3764696598,
	UpdateLogList = {
		[1] = {
			Timestamp = 1784039908,
		},
		[2] = {
			Timestamp = 1784040148,
		},
		[3] = {
			Timestamp = 1784040165,
		},
	},
	SettingGroups = {
		[1] = "Default",
	},
}
