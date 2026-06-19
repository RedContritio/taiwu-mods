using System.Text.Json;
using System.Text.RegularExpressions;

var repo = FindRepoRoot();
var test = new ContractTests(repo);
test.RunAll();

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ModBuild", "mods.json")) &&
            File.Exists(Path.Combine(dir.FullName, "TaiwuMods.sln")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Cannot locate repository root.");
}

sealed class ContractTests
{
    private readonly string _repo;
    private readonly List<string> _failures = new();
    private readonly List<ModEntry> _mods;

    public ContractTests(string repo)
    {
        _repo = repo;
        _mods = LoadMods();
    }

    public void RunAll()
    {
        Run("mod manifest projects and roots exist", ModManifestProjectsAndRootsExist);
        Run("config declarations match project outputs", ConfigDeclarationsMatchProjects);
        Run("release configs declare supported game version", ReleaseConfigsDeclareSupportedGameVersion);
        Run("mod config patches reference formal config names", ModConfigPatchesReferenceFormalConfigNames);
        Run("settings keys are read by source", SettingsKeysAreReadBySource);
        Run("Harmony manifest matches source patch surface", HarmonyManifestMatchesSourcePatchSurface);
        Run("ForceEncounter interaction contract is wired", ForceEncounterInteractionContract);
        Run("pure mod rules", PureModRules);

        if (_failures.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Contract tests failed:");
            foreach (string failure in _failures)
            {
                Console.Error.WriteLine(" - " + failure);
            }

            Environment.Exit(1);
        }

        Console.WriteLine("All Taiwu mod contract tests passed.");
    }

    private void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("OK: " + name);
        }
        catch (Exception ex)
        {
            _failures.Add($"{name}: {ex.Message}");
        }
    }

    private List<ModEntry> LoadMods()
    {
        string path = Path.Combine(_repo, "ModBuild", "mods.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<ModEntry>();
        foreach (JsonElement element in doc.RootElement.GetProperty("mods").EnumerateArray())
        {
            var projects = element.GetProperty("projects")
                .EnumerateArray()
                .Select(p => p.GetString() ?? string.Empty)
                .Where(p => p.Length > 0)
                .ToArray();

            result.Add(new ModEntry(
                element.GetProperty("name").GetString() ?? string.Empty,
                element.GetProperty("status").GetString() ?? string.Empty,
                projects,
                element.TryGetProperty("autoIncrementBuildVersion", out JsonElement autoIncrement) &&
                autoIncrement.ValueKind == JsonValueKind.True));
        }

        return result;
    }

    private void ModManifestProjectsAndRootsExist()
    {
        foreach (ModEntry mod in _mods)
        {
            Assert(!string.IsNullOrWhiteSpace(mod.Name), "mod entry has empty name");
            Assert(mod.Projects.Length > 0, $"{mod.Name} has no projects");

            string modDir = Path.Combine(_repo, mod.Name);
            Assert(Directory.Exists(modDir), $"{mod.Name} directory is missing");
            Assert(File.Exists(Path.Combine(modDir, "config.lua")), $"{mod.Name} config.lua is missing");
            Assert(File.Exists(Path.Combine(modDir, "Settings.Lua")), $"{mod.Name} Settings.Lua is missing");

            if (mod.Status == "release")
            {
                Assert(File.Exists(Path.Combine(modDir, "README.md")), $"{mod.Name} README.md is missing");
            }

            foreach (string project in mod.Projects)
            {
                Assert(File.Exists(Path.Combine(_repo, project)), $"{mod.Name} project does not exist: {project}");
            }
        }
    }

    private void ConfigDeclarationsMatchProjects()
    {
        foreach (ModEntry mod in _mods)
        {
            string config = ReadModFile(mod.Name, "config.lua");
            var declaredPlugins = ParseLuaStringList(config, "BackendPlugins")
                .Concat(ParseLuaStringList(config, "FrontendPlugins"))
                .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                .Where(p => p.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var declaredEvents = ParseLuaStringList(config, "EventPackages")
                .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                .Where(p => p.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var projectNames = mod.Projects
                .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                .Where(p => p.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string plugin in declaredPlugins)
            {
                Assert(projectNames.Contains(plugin), $"{mod.Name} declares plugin {plugin}.dll but no matching project exists");
            }

            foreach (string eventPackage in declaredEvents)
            {
                Assert(projectNames.Contains(eventPackage), $"{mod.Name} declares event package {eventPackage}.dll but no matching project exists");
            }

            foreach (string project in projectNames)
            {
                if (project.EndsWith(".Backend", StringComparison.OrdinalIgnoreCase) ||
                    project.EndsWith(".Frontend", StringComparison.OrdinalIgnoreCase))
                {
                    Assert(declaredPlugins.Contains(project), $"{mod.Name} project {project} is not declared as a plugin");
                }

                if (project.EndsWith(".Events", StringComparison.OrdinalIgnoreCase))
                {
                    Assert(declaredEvents.Contains(project), $"{mod.Name} project {project} is not declared as an event package");
                }
            }
        }
    }

    private void ReleaseConfigsDeclareSupportedGameVersion()
    {
        foreach (ModEntry mod in _mods.Where(m => m.Status == "release"))
        {
            string config = ReadModFile(mod.Name, "config.lua");
            string modVersion = ExtractLuaString(config, "Version");
            if (mod.AutoIncrementBuildVersion)
            {
                Assert(Regex.IsMatch(modVersion, @"^\d+\.\d+\.\d+\.\d+$"), $"{mod.Name} auto-increment Version must use four numeric parts: {modVersion}");
            }

            string gameVersion = ExtractLuaString(config, "GameVersion");
            Assert(!string.IsNullOrWhiteSpace(gameVersion), $"{mod.Name} release config.lua must declare GameVersion");
            Assert(Version.TryParse(StripVersionSuffix(gameVersion), out Version? version), $"{mod.Name} GameVersion is not parseable: {gameVersion}");
            Assert(version >= new Version(0, 0, 79), $"{mod.Name} GameVersion is below the formal mod compatibility cut: {gameVersion}");
        }
    }

    private void ModConfigPatchesReferenceFormalConfigNames()
    {
        foreach (ModEntry mod in _mods.Where(m => m.Status == "release"))
        {
            string configDir = Path.Combine(_repo, mod.Name, "Config");
            if (!Directory.Exists(configDir))
            {
                continue;
            }

            foreach (string patchPath in Directory.EnumerateFiles(configDir, "*.lua", SearchOption.AllDirectories))
            {
                string patch = File.ReadAllText(patchPath);
                string configName = ExtractLuaString(patch, "ConfigName");
                Assert(!string.IsNullOrWhiteSpace(configName), $"{mod.Name} config patch missing ConfigName: {patchPath}");

                var mapping = LoadFormalConfigRefMap(configName);
                string sourceRefName = ExtractLuaString(patch, "SrcConfigRefName");
                Assert(!string.IsNullOrWhiteSpace(sourceRefName), $"{mod.Name} config patch must clone a formal {configName} row with SrcConfigRefName because the formal mod loader only implements source-based patches: {patchPath}");
                Assert(mapping.ByName.ContainsKey(sourceRefName), $"{mod.Name} config patch SrcConfigRefName does not exist in formal {configName} mapping: {sourceRefName}");

                string destRefName = ExtractLuaString(patch, "DestConfigRefName");
                Assert(!mapping.ByName.ContainsKey(destRefName), $"{mod.Name} config patch DestConfigRefName collides with formal {configName} mapping: {destRefName}");

                if (TryExtractLuaInt(patch, "TemplateId", out int templateId))
                {
                    if (mapping.ById.TryGetValue(templateId, out string? existingRefName))
                    {
                        Assert(false, $"{mod.Name} config patch TemplateId collides with formal {configName} mapping: {templateId} ({existingRefName})");
                    }
                }
            }
        }
    }

    private void SettingsKeysAreReadBySource()
    {
        foreach (ModEntry mod in _mods.Where(m => m.Status == "release"))
        {
            string config = ReadModFile(mod.Name, "config.lua");
            var keys = ParseDefaultSettingKeys(config);
            string source = ReadProjectSource(mod);
            foreach (string key in keys)
            {
                Assert(IsSettingKeyRead(key, source), $"{mod.Name} setting key is declared but not read by source: {key}");
            }
        }
    }

    private void HarmonyManifestMatchesSourcePatchSurface()
    {
        string manifestPath = Path.Combine(_repo, "ModBuild", "harmony-targets.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement target in doc.RootElement.GetProperty("targets").EnumerateArray())
        {
            string mod = target.GetProperty("mod").GetString() ?? string.Empty;
            manifest[mod] = target.GetProperty("patterns")
                .EnumerateArray()
                .Select(p => p.GetString() ?? string.Empty)
                .Where(p => p.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        foreach (ModEntry mod in _mods.Where(m => m.Status == "release"))
        {
            var patchedMethods = ExtractHarmonyPatchMethods(ReadProjectSource(mod));
            manifest.TryGetValue(mod.Name, out HashSet<string>? declared);
            declared ??= new HashSet<string>(StringComparer.Ordinal);

            foreach (string patchedMethod in patchedMethods)
            {
                Assert(declared.Contains(patchedMethod), $"{mod.Name} patches {patchedMethod} but harmony-targets.json does not declare it");
            }

            foreach (string declaredMethod in declared)
            {
                Assert(patchedMethods.Contains(declaredMethod), $"{mod.Name} declares Harmony target {declaredMethod} but source no longer patches it");
            }
        }
    }

    private void ForceEncounterInteractionContract()
    {
        ModEntry mod = _mods.Single(m => m.Name == "ForceEncounter");
        string config = ReadModFile(mod.Name, "config.lua");
        Assert(!config.Contains("FrontendPlugins", StringComparison.Ordinal), "ForceEncounter should not declare a frontend plugin");
        Assert(!config.Contains("ForceSuccess", StringComparison.Ordinal), "ForceEncounter should not expose ForceSuccess settings");
        Assert(!config.Contains("AllowTaiwuAsTarget", StringComparison.Ordinal), "ForceEncounter should not expose a Taiwu-as-target setting");
        Assert(mod.AutoIncrementBuildVersion, "ForceEncounter should auto-increment its build version during packaging and local deploy builds");
        string forceEncounterVersion = ExtractLuaString(config, "Version");
        Assert(forceEncounterVersion.StartsWith("0.0.0.", StringComparison.Ordinal), "ForceEncounter version should start from the 0.0.0.x line");
        Assert(int.TryParse(forceEncounterVersion.Split('.')[3], out int forceEncounterBuild) && forceEncounterBuild >= 1, "ForceEncounter build version should be at least 1");
        string packageScript = File.ReadAllText(Path.Combine(_repo, "ModBuild", "Package-Mod.ps1"));
        string deployScript = File.ReadAllText(Path.Combine(_repo, "deploy.ps1"));
        Assert(packageScript.Contains("Update-ModBuildVersion", StringComparison.Ordinal), "Package-Mod.ps1 should bump auto-increment build versions");
        Assert(deployScript.Contains("Update-ModBuildVersion", StringComparison.Ordinal), "deploy.ps1 should bump auto-increment build versions before local game deploy");
        Assert(deployScript.Contains("dotnet build", StringComparison.Ordinal), "deploy.ps1 should build current projects before local game deploy");

        string ids = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventIds.cs");
        string nativeEnemyInteractionEventGuid = ExtractNestedConst(ids, "事件", "原生敌对菜单");
        string eventGuid = ExtractNestedConst(ids, "事件", "外层入口");
        string combatResultEventGuid = ExtractNestedConst(ids, "事件", "战斗反馈");
        string consentChoiceEventGuid = ExtractNestedConst(ids, "事件", "内层选择");
        EventOptionIdSnapshot navigationOption = ExtractEventOptionId(ids, "打开入口");
        EventOptionIdSnapshot executeOption = ExtractEventOptionId(ids, "情难自已");
        string navigationOptionGuid = navigationOption.Guid;
        string optionGuid = executeOption.Guid;

        string interaction = ReadModFile(mod.Name, "Config", "InteractionEventOption.lua");
        string forceEncounterConfig = ReadModFile(mod.Name, "config.lua");
        Assert(forceEncounterConfig.Contains("Key = \"DebugMode\"", StringComparison.Ordinal), "ForceEncounter should expose a debug mode setting");
        Assert(forceEncounterConfig.Contains("Key = \"DebugMode\", DisplayName = \"调试模式\", DefaultValue = true", StringComparison.Ordinal), "ForceEncounter debug mode should default on while in active in-game testing");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Slider"",\s*Key\s*=\s*""ForcedFavorabilityPenalty""[\s\S]*?MinValue\s*=\s*0,\s*MaxValue\s*=\s*30000[\s\S]*?DefaultValue\s*=\s*30000"), "ForceEncounter should expose forced favorability penalty as a 0..30000 slider defaulting to 30000");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Toggle"",\s*Key\s*=\s*""ApplyAlertnessOnCombatStart""[\s\S]*?DisplayName\s*=\s*""开战增加戒心""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose forced combat alertness as an enabled-by-default toggle");
        Assert(interaction.Contains("SrcConfigRefName = \"敌对-出手袭击\"", StringComparison.Ordinal), "ForceEncounter should inherit from an existing formal hostile interaction option");
        Assert(!interaction.Contains("ArrestPrison", StringComparison.Ordinal), "ForceEncounter should not inherit from the obsolete ArrestPrison ref name");
        Assert(interaction.Contains("ActionPointCost = 50", StringComparison.Ordinal), "ForceEncounter interaction entry should show and gate the native 5-day action cost before entering the inner choice");
        Assert(TryExtractLuaInt(interaction, "TemplateId", out int interactionTemplateId), "ForceEncounter interaction patch is missing TemplateId");
        string backendIds = ReadModFile(mod.Name, "ForceEncounter.Backend", "ForceEncounterEventIds.cs");
        Assert(interactionTemplateId == ExtractConstShort(backendIds, "InteractionTemplateId"), "ForceEncounter Lua TemplateId must match backend interaction template id constant");
        Assert(nativeEnemyInteractionEventGuid == ExtractConst(backendIds, "NativeEnemyInteractionEventGuid"), "ForceEncounter backend native hostile event guid must match event package constant");
        Assert(navigationOptionGuid == ExtractConst(backendIds, "NavigationOptionGuid"), "ForceEncounter navigation option guid must match backend constant");
        Assert(navigationOptionGuid != optionGuid, "ForceEncounter navigation option must be separate from the executing option");
        var eventPath = ParseLuaStringList(interaction, "MapBlockCharCustomButtonEventPath");
        var optionPath = ParseLuaStringList(interaction, "MapBlockCharCustomButtonEventOptionPath");
        Assert(eventPath.SequenceEqual(new[] { eventGuid }), "ForceEncounter interaction path must point directly at its own event");
        Assert(optionPath.SequenceEqual(new[] { navigationOptionGuid }), "ForceEncounter interaction option path must use the side-effect-free navigation option");
        Assert(interaction.Contains($"OptionGuid = \"{optionGuid}\"", StringComparison.Ordinal), "ForceEncounter interaction target option must remain the executing option");
        string optionTips = ReadModFile(mod.Name, "Config", "EventOptionTipsInfo.lua");
        Assert(optionTips.Contains("ConfigName = \"EventOptionTipsInfo\"", StringComparison.Ordinal), "ForceEncounter should add native event option help metadata");
        Assert(optionTips.Contains("SrcConfigRefName = \"袭击\"", StringComparison.Ordinal), "ForceEncounter option help should clone an existing formal hostile tips row");
        Assert(optionTips.Contains($"Guid = {{ \"{optionGuid}\" }}", StringComparison.Ordinal), "ForceEncounter option help should map to the executing option guid");
        Assert(optionTips.Contains("放弃不会消耗行动力", StringComparison.Ordinal), "ForceEncounter option help should explain that abandon does not consume action time");

        string package = ReadProjectDirectorySource(mod.Name, "ForceEncounter.Events");
        string eventIdsSource = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventIds.cs");
        Assert(eventIdsSource.Contains("public static class 事件", StringComparison.Ordinal), "ForceEncounter event ids should group event guids by purpose");
        Assert(eventIdsSource.Contains("public static class 选项", StringComparison.Ordinal), "ForceEncounter event ids should group option keys and guids together");
        Assert(!eventIdsSource.Contains("NavigationOptionGuid", StringComparison.Ordinal), "ForceEncounter event ids should not keep flat option guid constants");
        Assert(!package.Contains("AllowTaiwuAsTarget", StringComparison.Ordinal), "ForceEncounter event UI should not read a Taiwu-as-target setting");
        Assert(package.Contains("new ForceEncounterEvent()", StringComparison.Ordinal), "ForceEncounter entry event is not registered");
        Assert(!package.Contains("new ForceEncounterAgeWarningEvent()", StringComparison.Ordinal), "ForceEncounter special-age warning should replace the inner choice instead of adding an extra event layer");
        Assert(package.Contains("new ForceEncounterConsentChoiceEvent()", StringComparison.Ordinal), "ForceEncounter consent choice event is not registered");
        Assert(package.Contains("new ForceEncounterCombatResultEvent()", StringComparison.Ordinal), "ForceEncounter combat result event is not registered");
        Assert(package.Contains("item.Package = this", StringComparison.Ordinal), "ForceEncounter event package should bind runtime package metadata to event items");
        Assert(package.Contains("EventHelper.AddOptionToEvent", StringComparison.Ordinal), "ForceEncounter should extend the native hostile action menu");
        Assert(package.Contains("ForceEncounterEventIds.事件.原生敌对菜单", StringComparison.Ordinal), "ForceEncounter should target the native hostile interaction event");
        Assert(nativeEnemyInteractionEventGuid == "7c70ce0c-577a-4049-bcad-e593c63d62d4", "ForceEncounter native hostile interaction event guid changed unexpectedly");
        Assert(package.Contains("StayOnEntryEvent", StringComparison.Ordinal), "ForceEncounter entry event should have a side-effect-free navigation option");
        Assert(package.Contains("EventHelper.StartCombat", StringComparison.Ordinal), "ForceEncounter does not start formal combat");
        Assert(package.Contains("CombatResultType.IsPlayerWin", StringComparison.Ordinal), "ForceEncounter does not map formal combat result");
        Assert(package.Contains("ForceEncounterEventIds.选项.战斗反馈继续.Key", StringComparison.Ordinal), "ForceEncounter combat result event should show a feedback page with a continue option");
        Assert(!package.Contains("EventOptions = Array.Empty<TaiwuEventOption>()", StringComparison.Ordinal), "ForceEncounter combat result event should not silently close without feedback");
        Assert(package.Contains("战斗已经结束。你压服了对方", StringComparison.Ordinal), "ForceEncounter combat success feedback is missing");
        Assert(package.Contains("战斗已经结束。你未能压服对方", StringComparison.Ordinal), "ForceEncounter combat failure feedback is missing");
        Assert(!string.IsNullOrWhiteSpace(combatResultEventGuid), "ForceEncounter combat result event guid is empty");
        Assert(!string.IsNullOrWhiteSpace(consentChoiceEventGuid), "ForceEncounter consent choice event guid is empty");
        Assert(package.Contains("ForceEncounterEventIds.结算模式.探测", StringComparison.Ordinal), "ForceEncounter entry event does not probe before combat");
        Assert(package.Contains("ForceEncounterEventIds.探测结果.需要战斗选择", StringComparison.Ordinal), "ForceEncounter entry event does not branch to combat choice");
        Assert(package.Contains("return ForceEncounterEventIds.事件.内层选择", StringComparison.Ordinal), "ForceEncounter should return the combat choice event guid from the option callback");
        Assert(!package.Contains("AgeWarning", StringComparison.Ordinal), "ForceEncounter should not route special age groups through an extra warning event");
        Assert(package.Contains("ForceEncounterEventIds.参数.特殊年龄", StringComparison.Ordinal), "ForceEncounter should carry special-age state into the inner choice");
        Assert(!package.Contains("仍要正常发生关系", StringComparison.Ordinal), "ForceEncounter special-age accepted option text should stay identical to the normal-age option text");
        Assert(!package.Contains("仍要强制关系", StringComparison.Ordinal), "ForceEncounter special-age forced option text should stay identical to the normal-age option text");
        Assert(!package.Contains("ActorNotAdult", StringComparison.Ordinal), "ForceEncounter should warn for special age groups instead of blocking the actor by age");
        Assert(!package.Contains("TargetNotAdult", StringComparison.Ordinal), "ForceEncounter should warn for special age groups instead of blocking the target by age");
        Assert(package.Contains("ActorBaby", StringComparison.Ordinal), "ForceEncounter should follow native non-baby interaction filtering for the actor");
        Assert(package.Contains("TargetBaby", StringComparison.Ordinal), "ForceEncounter should follow native non-baby interaction filtering for the target");
        Assert(!package.Contains("EventHelper.ToEvent(ForceEncounterEventIds.事件.内层选择)", StringComparison.Ordinal), "ForceEncounter should not route the combat choice through ToEvent plus an empty option return");
        Assert(!eventIdsSource.Contains("const string ModId", StringComparison.Ordinal), "ForceEncounter events should use the runtime package mod id, not a hard-coded development mod id");
        Assert(package.Contains("Package?.ModIdString", StringComparison.Ordinal), "ForceEncounter events should call the backend through the runtime package mod id");
        Assert(package.Contains("bool debugMode = true", StringComparison.Ordinal), "ForceEncounter event debug logging should default on when settings are unavailable");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.情难自已\.Key,[\s\S]*?Behavior\s*=\s*EventOptionBehavior\.BehaviorEgoistic,[\s\S]*?Important\s*=\s*false,[\s\S]*?OnOptionSelect\s*=\s*Execute"), "ForceEncounter outer hostile option should use native egoistic styling");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.正常发生关系\.Key,[\s\S]*?Important\s*=\s*true,[\s\S]*?OnOptionSelect\s*=\s*NormalEncounter"), "ForceEncounter accepted commit option should be marked important");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.强制关系\.Key,[\s\S]*?Behavior\s*=\s*EventOptionBehavior\.BehaviorEgoistic,[\s\S]*?Important\s*=\s*true,[\s\S]*?OnOptionSelect\s*=\s*ForceCombat"), "ForceEncounter forced commit option should carry the native egoistic behavior styling/effect");
        Assert(package.Contains("EventArgBox.OptionWaitConfirmKey", StringComparison.Ordinal), "ForceEncounter outer egoistic styling should use native wait-confirm to avoid applying behavior effects on entry");
        Assert(package.Contains("ForceEncounterEventIds.等待确认.外层预览", StringComparison.Ordinal), "ForceEncounter wait-confirm should use a stable mod-owned wait key");
        Assert(!package.Contains("ConchShip_PresetKey_ConfirmWaitOptionSignal", StringComparison.Ordinal), "ForceEncounter inner commit options should not confirm the outer wait option; forced commit carries its own behavior effect");
        Assert(package.Contains("BuildPreviewCosts()", StringComparison.Ordinal), "ForceEncounter should show the action cost on the outer hostile option");
        Assert(package.Contains("new OptionConsumeInfo((sbyte)8, 5, false)", StringComparison.Ordinal), "ForceEncounter outer hostile option should display but not consume the native 5-day action cost");
        Assert(package.Contains("BuildCommitCosts()", StringComparison.Ordinal), "ForceEncounter should put costs on inner commit options");
        Assert(package.Contains("new OptionConsumeInfo((sbyte)8, 5, true)", StringComparison.Ordinal), "ForceEncounter inner commit options should consume the native 5-day action cost");
        Assert(!package.Contains("new OptionConsumeInfo((sbyte)16", StringComparison.Ordinal), "ForceEncounter should not consume a main attribute cost");
        Assert(package.Contains("（情难自已……）", StringComparison.Ordinal), "ForceEncounter hostile option should use native parenthesized option text");
        Assert(package.Contains("（正常发生关系……）", StringComparison.Ordinal), "ForceEncounter accepted branch prompt option is missing");
        Assert(package.Contains("（强制关系……）", StringComparison.Ordinal), "ForceEncounter forced branch prompt option is missing");
        Assert(package.Contains("其他话题", StringComparison.Ordinal), "ForceEncounter abandon options should use the native other-topic label");
        Assert(!package.Contains("就此作罢", StringComparison.Ordinal), "ForceEncounter should not use a custom abandon label where native hostile interactions use other-topic");
        Assert(package.Contains("ArgBox?.GetString(\"MainInteractionHeadEvent\") ?? ForceEncounterEventIds.事件.原生敌对菜单", StringComparison.Ordinal), "ForceEncounter abandon options should return to the native hostile topic instead of closing the event");
        Assert(package.Contains("ForceEncounterEventIds.结算模式.亲密提交", StringComparison.Ordinal), "ForceEncounter accepted branch should commit only from the inner option");
        Assert(package.Contains("EventHelper.ChangeAlertnessOnAttack(targetId)", StringComparison.Ordinal), "ForceEncounter forced combat commit should apply the native attack alertness effect");
        Assert(package.Contains("bool applyAlertness = true", StringComparison.Ordinal), "ForceEncounter forced combat alertness setting should default on when settings are unavailable");
        Assert(package.Contains("DomainManager.Mod.GetSetting(modId, ApplyAlertnessOnCombatStartSettingKey, ref applyAlertness)", StringComparison.Ordinal), "ForceEncounter forced combat should read the alertness setting through the runtime mod id");
        Assert(package.Contains("StartForcedCombat(ArgBox, GetRuntimeModId())", StringComparison.Ordinal), "ForceEncounter forced combat should pass the runtime mod id into configurable native alertness handling");
        Assert(package.Contains("new ForceEncounterGuardInterceptEvent()", StringComparison.Ordinal), "ForceEncounter should register a guard intercept event");
        Assert(package.Contains("EventHelper.HasGuard(target)", StringComparison.Ordinal), "ForceEncounter forced combat should check native guard state");
        Assert(package.Contains("EventHelper.PrepareCombatEnemy(targetId, CombatConfig.DefKey.DieNormal, false)", StringComparison.Ordinal), "ForceEncounter forced combat should use native guarded enemy-team preparation");
        Assert(package.Contains("ForceEncounterEventIds.事件.护卫出面", StringComparison.Ordinal), "ForceEncounter forced combat should route to its own guard intercept event");
        Assert(!package.Contains("9638c0a8-fadf-4f6a-bb22-05f3aed994ed", StringComparison.Ordinal), "ForceEncounter should not jump to the native guard event because it hard-codes native attack result events");
        Assert(Regex.IsMatch(package, @"EventHelper\.StartCombat\(\s*partnerId,\s*CombatConfig\.DefKey\.DieNormal,\s*ForceEncounterEventIds\.事件\.战斗反馈,\s*(ArgBox|argBox),\s*true\)", RegexOptions.Singleline), "ForceEncounter guard combat should fight the intercepting guard and return to ForceEncounter result handling");
        Assert(!package.Contains("actor.GetCurrMainAttribute(4) < GlobalConfig.Instance.HarmfulActionCost", StringComparison.Ordinal), "ForceEncounter entry option should not block cost-free inspection");
        Assert(!package.Contains("InsufficientHarmfulActionCost", StringComparison.Ordinal), "ForceEncounter entry option should not diagnose inner commit costs");
        Assert(package.Contains("ForceEncounterEventIds.事件.战斗反馈", StringComparison.Ordinal), "ForceEncounter package does not reference combat result event guid");

        string backend = ReadModFile(mod.Name, "ForceEncounter.Backend", "BackendPlugin.cs");
        Assert(backend.Contains($"PluginConfig(\"ForceEncounter\", \"RedContritio\", \"{forceEncounterVersion}\")", StringComparison.Ordinal), "ForceEncounter backend plugin version should match config.lua Version");
        Assert(!backend.Contains("AddExecuteMethod(\"ForceEncounter\")", StringComparison.Ordinal), "ForceEncounter backend should not register methods under a hard-coded development mod id");
        Assert(backend.Contains("GetBoolSetting(\"DebugMode\", true)", StringComparison.Ordinal), "ForceEncounter backend debug logging should default on when settings are unavailable");
        Assert(!backend.Contains("AllowTaiwuAsTarget", StringComparison.Ordinal), "ForceEncounter backend should not accept a Taiwu-as-target override");
        Assert(!backend.Contains("HandleRapeAction", StringComparison.Ordinal), "ForceEncounter should not call original combat-power-based HandleRapeAction");
        Assert(!backend.Contains("GetCombatPower()", StringComparison.Ordinal), "ForceEncounter should not use combat power as a success gate");
        Assert(!backend.Contains("ForceSuccess", StringComparison.Ordinal), "ForceEncounter backend should not keep ForceSuccess");
        Assert(!backend.Contains("\"NotAdult\"", StringComparison.Ordinal), "ForceEncounter backend should not hard-block special age groups");
        Assert(backend.Contains("\"BabyNotAllowed\"", StringComparison.Ordinal), "ForceEncounter backend should reject babies to match native non-baby interaction behavior");
        Assert(backend.Contains("\"ForcedFavorabilityPenalty\"", StringComparison.Ordinal), "ForceEncounter backend should read the configured forced favorability penalty");
        Assert(!backend.Contains("RapeFavorabilityDelta = -30000", StringComparison.Ordinal), "ForceEncounter backend should not hard-code the forced favorability penalty");
        Assert(backend.Contains("CalculateForcedFavorabilityDelta", StringComparison.Ordinal), "ForceEncounter backend should calculate normal and Taiwu-villager favorability penalties through shared rules");
        Assert(!backend.Contains("CloseFriendFeatureId", StringComparison.Ordinal), "ForceEncounter should not give 谷中密友(685) a special favorability bonus because native TalkByNormalInformation has no direct favorability delta");
        Assert(!backend.Contains("ChangeFavorabilityOptionalRepeatedEvent(context, target, actor", StringComparison.Ordinal), "ForceEncounter accepted branch should not add extra favorability beyond native ordinary talk semantics");
        Assert(backend.Contains("actor.MakeLove(context, target, isRape: true)", StringComparison.Ordinal), "ForceEncounter success branch does not call MakeLove");
        Assert(backend.Contains("OnLoadedArchiveData()", StringComparison.Ordinal), "ForceEncounter backend should restore native menu extension after archive runtime reset");
        Assert(backend.Contains("OnEnterNewWorld()", StringComparison.Ordinal), "ForceEncounter backend should restore native menu extension after new-world runtime reset");
        Assert(backend.Contains("EventHelper.AddOptionToEvent", StringComparison.Ordinal), "ForceEncounter backend should use the formal event extension path after runtime reset");
        Assert(backend.Contains("AddRapeSucceed", StringComparison.Ordinal), "ForceEncounter success branch does not record rape success");
        Assert(backend.Contains("AddRapeFail", StringComparison.Ordinal), "ForceEncounter failure branch does not record rape failure");
        Assert(!backend.Contains("ChangeCurrMainAttribute(context, 4, -GlobalConfig.Instance.HarmfulActionCost)", StringComparison.Ordinal), "ForceEncounter backend should not double-consume costs handled by native option metadata");
        Assert(backend.Contains("CanResolveAsIntimateAcceptance", StringComparison.Ordinal), "ForceEncounter does not check intimate acceptance before combat");
    }

    private static void PureModRules()
    {
        ForceEncounterRules();
        FertilityRules();
        AntiNtrRules();
        DreamLoverRules();
    }

    private static void ForceEncounterRules()
    {
        Assert(ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(true, false), "ForceEncounter should succeed after formal battle win");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(false, false), "ForceEncounter should fail after battle loss");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(true, true), "ForceEncounter should not succeed when target is Taiwu");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(true, false, false, 4, 4, 2), "ForceEncounter should allow good spouse relation for neutral behavior");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(true, false, false, 3, 4, 2), "ForceEncounter should require both directions to pass intimate favor threshold");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, true, false, 3, 3, 3), "ForceEncounter should allow rebellious mutual lovers at a lower threshold");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, true, false, 4, 4, 0), "ForceEncounter should require stricter favor for upright mutual lovers");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, 6, 6, 4), "ForceEncounter should not skip combat without spouse or mutual lover relation");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, false) == -30000, "ForceEncounter normal forced route should default to the configured full penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, true) == -9000, "ForceEncounter Taiwu villager forced route should use 30% of the normal forced penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(40000, false) == -30000, "ForceEncounter forced favorability penalty should clamp above the configured maximum");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(40000, true) == -9000, "ForceEncounter Taiwu villager forced route should use 30% after clamping above the configured maximum");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(-100, false) == 0, "ForceEncounter forced favorability penalty should clamp below zero");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(0, false) == 0, "ForceEncounter normal forced route should allow disabling favorability penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(0, true) == 0, "ForceEncounter Taiwu villager forced route should allow disabling favorability penalty");
    }

    private static void FertilityRules()
    {
        Assert(FertilityControl.Backend.FertilityRules.PassChildLimit(5, 5, 100, 100), "Fertility child limit should allow children equal to fertility / 20");
        Assert(!FertilityControl.Backend.FertilityRules.PassChildLimit(6, 5, 100, 100), "Fertility child limit should block father over limit");
        Assert(!FertilityControl.Backend.FertilityRules.PassChildLimit(5, 6, 100, 100), "Fertility child limit should block mother over limit");
        Assert(FertilityControl.Backend.FertilityRules.CalculatePregnancyChance(false, 100, 100, false, 0) == 60, "Normal pregnancy chance should match native base chance");
        Assert(FertilityControl.Backend.FertilityRules.CalculatePregnancyChance(true, 100, 100, false, 0) == 20, "Rape pregnancy chance should match native base chance");
        Assert(FertilityControl.Backend.FertilityRules.CalculatePregnancyChance(false, 1, 1, true, 77) == 77, "Explicit pregnancy rate should override fertility math");
        Assert(FertilityControl.Backend.FertilityRules.CalculateTotalFertilityChance(false, 7500) == 45, "Total fertility override should scale normal base chance");
        Assert(FertilityControl.Backend.FertilityRules.ShouldOverrideInbreeding(true, true), "Inbreeding override should apply when enabled and Taiwu is involved");
        Assert(!FertilityControl.Backend.FertilityRules.ShouldOverrideInbreeding(true, false), "Inbreeding override should ignore non-Taiwu pairs");
        Assert(FertilityControl.Backend.FertilityRules.ShouldOverrideCricketRate(true, false, true), "Cricket override should apply to new Taiwu pregnancies");
        Assert(!FertilityControl.Backend.FertilityRules.ShouldOverrideCricketRate(true, true, true), "Cricket override should not rewrite existing pregnancy state");
    }

    private static void AntiNtrRules()
    {
        Assert(AntiNTR.Backend.AntiNtrRules.CanEvaluatePair(true, 1, 2, 3), "AntiNTR should evaluate enabled non-Taiwu pairs");
        Assert(!AntiNTR.Backend.AntiNtrRules.CanEvaluatePair(false, 1, 2, 3), "AntiNTR should ignore disabled mod");
        Assert(!AntiNTR.Backend.AntiNtrRules.CanEvaluatePair(true, -1, 2, 3), "AntiNTR should ignore missing Taiwu");
        Assert(!AntiNTR.Backend.AntiNtrRules.CanEvaluatePair(true, 1, 1, 3), "AntiNTR should ignore direct Taiwu pair");
        Assert(AntiNTR.Backend.AntiNtrRules.ShouldPreventAll(true), "AntiNTR PreventAll should block immediately");
        Assert(!AntiNTR.Backend.AntiNtrRules.ShouldBlockProtectedSpouse(true, 10, 10), "AntiNTR should allow protected couple when AllowCouple is on");
        Assert(AntiNTR.Backend.AntiNtrRules.ShouldBlockProtectedSpouse(true, 11, 10), "AntiNTR should block non-spouse partner of protected spouse");
        Assert(AntiNTR.Backend.AntiNtrRules.ShouldBlockProtectedSpouse(false, 10, 10), "AntiNTR should block even spouse when AllowCouple is off");
    }

    private static void DreamLoverRules()
    {
        bool[] favor = AllowOnly(13, 8);
        bool[] good = AllowOnly(5, 2);
        bool[] charm = AllowOnly(9, 4);
        bool[] rank = AllowOnly(9, 3);
        bool[] infect = AllowOnly(3, 0);

        Assert(DreamLover.Backend.DreamLoverRules.PassBasicFilters(
            acceptSameGender: false,
            sameGender: false,
            ignoreDistance: false,
            sameLocation: true,
            ageYears: 30,
            minAge: 16,
            maxAge: 60,
            favorType: 2,
            favor,
            goodnessLevel: 2,
            good,
            charmLevel: 4,
            charm,
            rankLevel: 3,
            rank,
            infectState: 0,
            infect), "DreamLover basic filters should allow a matching candidate");

        Assert(!DreamLover.Backend.DreamLoverRules.PassBasicFilters(false, true, false, true, 30, 16, 60, 2, favor, 2, good, 4, charm, 3, rank, 0, infect), "DreamLover should block same gender by default");
        Assert(!DreamLover.Backend.DreamLoverRules.PassBasicFilters(false, false, false, false, 30, 16, 60, 2, favor, 2, good, 4, charm, 3, rank, 0, infect), "DreamLover should block distance by default");
        Assert(!DreamLover.Backend.DreamLoverRules.PassBasicFilters(false, false, true, false, 15, 16, 60, 2, favor, 2, good, 4, charm, 3, rank, 0, infect), "DreamLover should block below minimum age");
        Assert(!DreamLover.Backend.DreamLoverRules.PassBasicFilters(false, false, true, false, 30, 16, 60, -6, favor, 2, good, 4, charm, 3, rank, 0, infect), "DreamLover should block disallowed favorability");

        var relationDefs = new[] { ("Rel_A", (ushort)1), ("Rel_B", (ushort)2) };
        Assert(DreamLover.Backend.DreamLoverRules.PassRelationFilter(0, relationDefs, new[] { false, false }), "DreamLover should allow candidates without configured relation flags");
        Assert(!DreamLover.Backend.DreamLoverRules.PassRelationFilter(1, relationDefs, new[] { false, true }), "DreamLover should block disallowed relation flags");
        Assert(DreamLover.Backend.DreamLoverRules.PassRelationFilter(2, relationDefs, new[] { false, true }), "DreamLover should allow enabled relation flags");

        Assert(DreamLover.Backend.DreamLoverRules.ShouldForgetUnreciprocatedAdoration(true, false, false, true, false, false), "DreamLover ForgetMe should queue unreciprocated adoration");
        Assert(!DreamLover.Backend.DreamLoverRules.ShouldForgetUnreciprocatedAdoration(true, true, false, true, false, false), "DreamLover ForgetMe should not run while enamor is enabled");
        Assert(!DreamLover.Backend.DreamLoverRules.ShouldForgetUnreciprocatedAdoration(true, false, false, true, false, true), "DreamLover ForgetMe should keep mutual adoration");
    }

    private static bool[] AllowOnly(int length, int allowedIndex)
    {
        var values = new bool[length];
        values[allowedIndex] = true;
        return values;
    }

    private static HashSet<string> ExtractHarmonyPatchMethods(string source)
    {
        var methods = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(source, @"\[HarmonyPatch\(typeof\([^)]*\),\s*""([^""]+)""\)\]"))
        {
            methods.Add(match.Groups[1].Value);
        }

        foreach (Match match in Regex.Matches(source, @"AccessTools\.Method\(typeof\([^)]*\),\s*""([^""]+)"""))
        {
            methods.Add(match.Groups[1].Value);
        }

        return methods;
    }

    private static bool IsSettingKeyRead(string key, string source)
    {
        if (source.Contains($"\"{key}\"", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (string prefix in new[] { "Favor_", "Good_", "Charm_", "Rank_", "Infect_" })
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) &&
                source.Contains($"\"{prefix}\" +", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractConst(string source, string name)
    {
        var match = Regex.Match(source, $@"const\s+string\s+{Regex.Escape(name)}\s*=\s*""([^""]+)""");
        Assert(match.Success, $"missing const string {name}");
        return match.Groups[1].Value;
    }

    private static string ExtractNestedConst(string source, string className, string name)
    {
        var classMatch = Regex.Match(
            source,
            $@"public\s+static\s+class\s+{Regex.Escape(className)}\s*\{{(?<body>.*?)\n\s*\}}",
            RegexOptions.Singleline);
        Assert(classMatch.Success, $"missing id group {className}");
        return ExtractConst(classMatch.Groups["body"].Value, name);
    }

    private static EventOptionIdSnapshot ExtractEventOptionId(string source, string name)
    {
        var match = Regex.Match(
            source,
            $@"public\s+static\s+readonly\s+EventOptionId\s+{Regex.Escape(name)}\s*=\s*new\(""([^""]+)"",\s*""([^""]+)""\)");
        Assert(match.Success, $"missing event option id {name}");
        return new EventOptionIdSnapshot(match.Groups[1].Value, match.Groups[2].Value);
    }

    private static int ExtractConstShort(string source, string name)
    {
        var match = Regex.Match(source, $@"const\s+short\s+{Regex.Escape(name)}\s*=\s*(-?\d+)");
        Assert(match.Success, $"missing const short {name}");
        return int.Parse(match.Groups[1].Value);
    }

    private FormalConfigRefMap LoadFormalConfigRefMap(string configName)
    {
        string? gameDir = Environment.GetEnvironmentVariable("TAIWU_GAME_DIR");
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            gameDir = @"D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu";
        }

        string mappingPath = Path.Combine(
            gameDir,
            "The Scroll of Taiwu_Data",
            "StreamingAssets",
            "ConfigRefNameMapping",
            configName + ".ref.txt");
        Assert(File.Exists(mappingPath), $"formal config ref mapping is missing: {mappingPath}");

        var byName = new Dictionary<string, int>(StringComparer.Ordinal);
        var byId = new Dictionary<int, string>();
        string[] lines = File.ReadAllLines(mappingPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            string name = lines[i];
            int id = int.Parse(lines[i + 1]);
            byName[name] = id;
            byId[id] = name;
        }

        return new FormalConfigRefMap(byName, byId);
    }

    private static string ExtractLuaString(string source, string name)
    {
        var match = Regex.Match(source, $@"(?m)^\s*{Regex.Escape(name)}\s*=\s*""([^""]*)""");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string StripVersionSuffix(string value)
    {
        if (value.StartsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }

        int suffixIndex = value.IndexOf('-', StringComparison.Ordinal);
        return suffixIndex >= 0 ? value[..suffixIndex] : value;
    }

    private static bool TryExtractLuaInt(string source, string name, out int value)
    {
        var match = Regex.Match(source, $@"(?m)^\s*{Regex.Escape(name)}\s*=\s*(-?\d+)");
        if (match.Success)
        {
            value = int.Parse(match.Groups[1].Value);
            return true;
        }

        value = 0;
        return false;
    }

    private string ReadProjectSource(ModEntry mod)
    {
        return string.Join(
            "\n",
            mod.Projects.SelectMany(project =>
            {
                string projectDir = Path.GetDirectoryName(Path.Combine(_repo, project))!;
                return Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                   !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            }).Select(File.ReadAllText));
    }

    private string ReadProjectDirectorySource(string modName, string projectDirectory)
    {
        string projectDir = Path.Combine(_repo, modName, projectDirectory);
        return string.Join(
            "\n",
            Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                               !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
    }

    private string ReadModFile(string modName, params string[] segments)
    {
        return File.ReadAllText(Path.Combine(new[] { _repo, modName }.Concat(segments).ToArray()));
    }

    private static List<string> ParseDefaultSettingKeys(string config)
    {
        return Regex.Matches(config, @"Key\s*=\s*""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ParseLuaStringList(string text, string key)
    {
        var match = Regex.Match(text, $@"{Regex.Escape(key)}\s*=\s*\{{(?<body>.*?)\}}", RegexOptions.Singleline);
        if (!match.Success)
        {
            return new List<string>();
        }

        return Regex.Matches(match.Groups["body"].Value, @"""([^""]+)""")
            .Select(item => item.Groups[1].Value)
            .ToList();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

sealed record EventOptionIdSnapshot(string Key, string Guid);
sealed record ModEntry(string Name, string Status, string[] Projects, bool AutoIncrementBuildVersion);
sealed record FormalConfigRefMap(Dictionary<string, int> ByName, Dictionary<int, string> ById);
