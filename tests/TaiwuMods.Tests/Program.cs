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
        Run("ModBuild auto-increment version wiring is stable", ModBuildAutoIncrementVersionWiring);
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

    private void ModBuildAutoIncrementVersionWiring()
    {
        string buildCommon = File.ReadAllText(Path.Combine(_repo, "ModBuild", "ModBuild.Common.ps1"));
        Assert(buildCommon.Contains("Resolve-PluginConfigArgument", StringComparison.Ordinal), "ModBuild should resolve constant PluginConfig mod ids for validation and version bumping");
        Assert(buildCommon.Contains("Get-PluginConfigConstantValues", StringComparison.Ordinal), "ModBuild should parse shared PluginConfig constants instead of trusting identifier shape");
        Assert(buildCommon.Contains("$outerName.Mod.", StringComparison.Ordinal), "ModBuild should resolve nested Mod metadata constants by their qualified names");
        Assert(buildCommon.Contains("$projectSourceFiles = Get-ProjectSourceFiles", StringComparison.Ordinal), "ModBuild version bumping should collect all project sources before resolving PluginConfig constants");
        Assert(buildCommon.Contains("Get-PluginConfigConstantValues -SourceFiles $projectSourceFiles", StringComparison.Ordinal), "ModBuild version bumping should resolve PluginConfig constants from linked project sources");
        Assert(!buildCommon.Contains("Get-PluginConfigConstantValues -SourceFiles @($sourceFile)", StringComparison.Ordinal), "ModBuild version bumping should not resolve PluginConfig constants from only the current source file");

        string packageScript = File.ReadAllText(Path.Combine(_repo, "ModBuild", "Package-Mod.ps1"));
        string deployScript = File.ReadAllText(Path.Combine(_repo, "deploy.ps1"));
        Assert(packageScript.Contains("Update-ModBuildVersion", StringComparison.Ordinal), "Package-Mod.ps1 should bump auto-increment build versions");
        Assert(deployScript.Contains("Update-ModBuildVersion", StringComparison.Ordinal), "deploy.ps1 should bump auto-increment build versions before local game deploy");
        Assert(deployScript.Contains("dotnet build", StringComparison.Ordinal), "deploy.ps1 should build current projects before local game deploy");
    }

    private void ForceEncounterInteractionContract()
    {
        ModEntry mod = _mods.Single(m => m.Name == "ForceEncounter");
        string config = ReadModFile(mod.Name, "config.lua");
        Assert(mod.AutoIncrementBuildVersion, "ForceEncounter should auto-increment its build version during packaging and local deploy builds");
        string forceEncounterVersion = ExtractLuaString(config, "Version");
        Assert(forceEncounterVersion.StartsWith("1.0.0.", StringComparison.Ordinal), "ForceEncounter version should start from the 1.0.0.x line");
        Assert(int.TryParse(forceEncounterVersion.Split('.')[3], out int forceEncounterBuild) && forceEncounterBuild >= 0, "ForceEncounter build version should be non-negative");
        Assert(File.Exists(Path.Combine(_repo, "TaiwuMod.Common", "TaiwuModSettings.cs")), "Taiwu common settings facade is missing");

        string sharedIds = ReadModFile(mod.Name, "ForceEncounter.Shared", "ForceEncounterConstants.cs");
        string nativeEnemyInteractionEventGuid = ExtractNestedConst(sharedIds, "EventGuids", "NativeEnemyInteraction");
        string ids = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventIds.cs");
        string eventGuid = ExtractNestedConst(sharedIds, "EventGuids", "Entry");
        string combatResultEventGuid = ExtractNestedConst(sharedIds, "EventGuids", "CombatResult");
        string capturedTargetDispositionEventGuid = ExtractNestedConst(sharedIds, "EventGuids", "CapturedTargetDisposition");
        string consentChoiceEventGuid = ExtractNestedConst(sharedIds, "EventGuids", "ConsentChoice");
        string acceptedResultEventGuid = ExtractNestedConst(sharedIds, "EventGuids", "AcceptedResult");
        EventOptionIdSnapshot navigationOption = new(
            ExtractNestedConst(sharedIds, "Options", "OpenKey"),
            ExtractNestedConst(sharedIds, "Options", "OpenGuid"));
        EventOptionIdSnapshot executeOption = new(
            ExtractNestedConst(sharedIds, "Options", "ExecuteKey"),
            ExtractNestedConst(sharedIds, "Options", "ExecuteGuid"));
        string navigationOptionGuid = navigationOption.Guid;
        string optionGuid = executeOption.Guid;

        string interaction = ReadModFile(mod.Name, "Config", "InteractionEventOption.lua");
        string forceEncounterConfig = ReadModFile(mod.Name, "config.lua");
        Assert(forceEncounterConfig.Contains("Key = \"DebugMode\"", StringComparison.Ordinal), "ForceEncounter should expose a debug mode setting");
        Assert(forceEncounterConfig.Contains("Key = \"DebugMode\", DisplayName = \"调试模式\", DefaultValue = true", StringComparison.Ordinal), "ForceEncounter debug mode should default on while in active in-game testing");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Slider"",\s*Key\s*=\s*""ActionTimeCostDays""[\s\S]*?MinValue\s*=\s*0,\s*MaxValue\s*=\s*5[\s\S]*?DefaultValue\s*=\s*5"), "ForceEncounter should expose action time cost as a 0..5 slider defaulting to 5");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Slider"",\s*Key\s*=\s*""ForcedFavorabilityPenalty""[\s\S]*?MinValue\s*=\s*0,\s*MaxValue\s*=\s*30000[\s\S]*?DefaultValue\s*=\s*30000"), "ForceEncounter should expose forced favorability penalty as a 0..30000 slider defaulting to 30000");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Slider"",\s*Key\s*=\s*""TaiwuVillagerPenaltyPercent""[\s\S]*?MinValue\s*=\s*0,\s*MaxValue\s*=\s*100[\s\S]*?DefaultValue\s*=\s*30"), "ForceEncounter should expose Taiwu villager penalty ratio as a 0..100 slider defaulting to 30");
        Assert(Regex.IsMatch(forceEncounterConfig, @"SettingType\s*=\s*""Toggle"",\s*Key\s*=\s*""ApplyAlertnessOnCombatStart""[\s\S]*?DisplayName\s*=\s*""开战增加戒心""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose forced combat alertness as an enabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""EnableGuardInterception""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose guard interception as an enabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""EnableTaiwuVillagerGuardInterception""[\s\S]*?DefaultValue\s*=\s*false"), "ForceEncounter should expose Taiwu-villager guard interception as a disabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""BecomeEnemyOnForcedRoute""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose forced-route enmity as an enabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""CreateSecretOnForcedSuccess""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose forced-success secret creation as an enabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""CreateSecretOnAcceptedSuccess""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose accepted-success secret creation as an enabled-by-default toggle");
        Assert(Regex.IsMatch(forceEncounterConfig, @"Key\s*=\s*""允许未成年""[\s\S]*?DisplayName\s*=\s*""允许未成年""[\s\S]*?DefaultValue\s*=\s*true"), "ForceEncounter should expose minor interaction visibility as an enabled-by-default toggle");
        Assert(interaction.Contains("SrcConfigRefName = \"敌对-出手袭击\"", StringComparison.Ordinal), "ForceEncounter should inherit from an existing formal hostile interaction option");
        Assert(interaction.Contains("OncePerMonth = false", StringComparison.Ordinal), "ForceEncounter should match native attack, which is not limited to once per month");
        Assert(!interaction.Contains("ArrestPrison", StringComparison.Ordinal), "ForceEncounter should not inherit from the obsolete ArrestPrison ref name");
        Assert(interaction.Contains("ActionPointCost = 0", StringComparison.Ordinal), "ForceEncounter interaction patch should not keep a static action cost when the event option reads configurable costs");
        Assert(TryExtractLuaInt(interaction, "TemplateId", out int interactionTemplateId), "ForceEncounter interaction patch is missing TemplateId");
        string backendIds = ReadModFile(mod.Name, "ForceEncounter.Backend", "ForceEncounterEventIds.cs");
        Assert(interactionTemplateId == ExtractNestedConstShort(sharedIds, "Gameplay", "InteractionTemplateId"), "ForceEncounter Lua TemplateId must match the shared interaction template id constant");
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
        Assert(package.Contains("new ForceEncounterEvent()", StringComparison.Ordinal), "ForceEncounter entry event is not registered");
        Assert(package.Contains("new ForceEncounterConsentChoiceEvent()", StringComparison.Ordinal), "ForceEncounter consent choice event is not registered");
        Assert(package.Contains("new ForceEncounterCombatResultEvent()", StringComparison.Ordinal), "ForceEncounter combat result event is not registered");
        Assert(package.Contains("new ForceEncounterCapturedTargetDispositionEvent()", StringComparison.Ordinal), "ForceEncounter captured-target disposition event is not registered");
        Assert(package.Contains("new ForceEncounterAcceptedResultEvent()", StringComparison.Ordinal), "ForceEncounter accepted result event is not registered");
        Assert(package.Contains("item.Package = this", StringComparison.Ordinal), "ForceEncounter event package should bind runtime package metadata to event items");
        Assert(package.Contains("EventHelper.AddOptionToEvent", StringComparison.Ordinal), "ForceEncounter should extend the native hostile action menu");
        Assert(package.Contains("ForceEncounterEventIds.事件.原生敌对菜单", StringComparison.Ordinal), "ForceEncounter should target the native hostile interaction event");
        Assert(nativeEnemyInteractionEventGuid == "7c70ce0c-577a-4049-bcad-e593c63d62d4", "ForceEncounter native hostile interaction event guid changed unexpectedly");
        Assert(package.Contains("StayOnEntryEvent", StringComparison.Ordinal), "ForceEncounter entry event should have a side-effect-free navigation option");
        Assert(package.Contains("EventHelper.StartCombat", StringComparison.Ordinal), "ForceEncounter does not start formal combat");
        Assert(package.Contains("CombatResultType.IsPlayerWin", StringComparison.Ordinal), "ForceEncounter does not map formal combat result");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.NativeMainEnemyId", StringComparison.Ordinal), "ForceEncounter combat result should inspect the actual main enemy before resolving against the target");
        Assert(package.Contains("ForceEncounterConstants.Reasons.GuardIntercepted", StringComparison.Ordinal), "ForceEncounter guard combat should not resolve as target success");
        Assert(Regex.IsMatch(package, @"mainEnemyId\s*==\s*targetId", RegexOptions.Singleline), "ForceEncounter combat result should distinguish guard combat from target combat");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.NativeSeizedCharacterId", StringComparison.Ordinal), "ForceEncounter combat result should read the native captured-character combat key");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.NativeSeizeItemKey", StringComparison.Ordinal), "ForceEncounter combat result should read the native capture rope item key");
        Assert(package.Contains("EventHelper.AddPrisonerToCharacter", StringComparison.Ordinal), "ForceEncounter combat result should preserve native prisoner side effects");
        Assert(package.Contains("ForceEncounterConstants.Reasons.TargetCapturedInCombat", StringComparison.Ordinal), "ForceEncounter target-captured combat result should use a distinct route reason");
        Assert(package.Contains("ForceEncounterEventIds.事件.擒获处置", StringComparison.Ordinal), "ForceEncounter should route captured original targets to a captured-target disposition page");
        Assert(package.Contains("EventHelper.RemovePrisonerFromCharacter(prisoner.GetId(), actor.GetId(), false)", StringComparison.Ordinal), "ForceEncounter captured-target release should remove the native prisoner state");
        Assert(package.Contains("EventHelper.HandleCombatResultReleaseEnemy(actor, prisoner)", StringComparison.Ordinal), "ForceEncounter captured-target release should use native release settlement helper");
        Assert(package.Contains("AddKidnapInPublic", StringComparison.Ordinal), "ForceEncounter captured-target keep should record native public-kidnap side effects");
        Assert(package.Contains("AddKidnapInPrivate", StringComparison.Ordinal), "ForceEncounter captured-target secret keep should record native private-kidnap side effects");
        Assert(package.Contains("（公开关押！）", StringComparison.Ordinal), "ForceEncounter captured-target keep option should use the native public-imprison wording");
        Assert(package.Contains("（秘密关押……）", StringComparison.Ordinal), "ForceEncounter captured-target secret keep option should use the native private-imprison wording");
        Assert(package.Contains("（放其离开……）", StringComparison.Ordinal), "ForceEncounter captured-target release option should use the native release wording");
        Assert(!package.Contains("（关押……）", StringComparison.Ordinal), "ForceEncounter captured-target keep option should not use a custom generic imprison label");
        Assert(!package.Contains("（释放……）", StringComparison.Ordinal), "ForceEncounter captured-target release option should not use a custom generic release label");
        Assert(!package.Contains("HandleCombatResultKillEnemy(actor, prisoner", StringComparison.Ordinal), "ForceEncounter captured-target disposition should not copy native execution options");
        Assert(package.Contains("EventHelper.CharacterLoseGuard(targetId, combatType)", StringComparison.Ordinal), "ForceEncounter guarded combat result should preserve native guard-loss side effects");
        Assert(package.Contains("EventHelper.CharacterEscapeToNearbyBlock(ArgBox, target, 3)", StringComparison.Ordinal), "ForceEncounter direct enemy-flee result should preserve native target-escape side effects");
        Assert(package.Contains("case CombatResultType.EnemyFlee", StringComparison.Ordinal) &&
               package.Contains("ForceEncounterConstants.Reasons.TargetEscaped", StringComparison.Ordinal), "ForceEncounter should not treat an escaping target as a forced-route success");
        Assert(package.Contains("case CombatResultType.EnemyDie", StringComparison.Ordinal) &&
               package.Contains("killTargetAfterBackend: true", StringComparison.Ordinal), "ForceEncounter should treat target death in combat as forced-route success before death settlement");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.KillTargetAfterCombatResult", StringComparison.Ordinal), "ForceEncounter should delay target death until after successful forced-route settlement");
        Assert(package.Contains("EventHelper.HandleCombatResultKillEnemy(actor, target, true)", StringComparison.Ordinal), "ForceEncounter target-death combat result should use the native kill settlement helper after success");
        Assert(package.Contains("EventHelper.IsCharacterDirectFallenInCombat(targetId, (CombatType)CombatConfig.DefKey.DieNormal)", StringComparison.Ordinal), "ForceEncounter direct target combat should copy native attack's direct-fallen check");
        Assert(package.Contains("ForceEncounterConstants.Reasons.TargetDirectFallen", StringComparison.Ordinal), "ForceEncounter direct-fallen combat path should return a distinct feedback reason");
        Assert(package.Contains("StoreDirectFallenSuccess", StringComparison.Ordinal) &&
               package.Contains("ForceEncounterConstants.Reasons.TargetDirectFallen, battleSucceeded: true", StringComparison.Ordinal), "ForceEncounter direct-fallen pre-combat path should settle as forced-route success");
        Assert(package.Contains("target.GetKidnapperId() == actorId", StringComparison.Ordinal), "ForceEncounter should skip repeated death combat for targets already kidnapped by Taiwu");
        Assert(package.Contains("ForceEncounterConstants.Reasons.TargetAlreadyPrisoner, battleSucceeded: true", StringComparison.Ordinal), "ForceEncounter already-prisoner path should settle as forced-route success");
        Assert(package.Contains("成年无力应战成功", StringComparison.Ordinal) &&
               package.Contains("未成年无力应战成功", StringComparison.Ordinal), "ForceEncounter direct-fallen success should have adult and minor feedback text");
        Assert(!package.Contains("if (reason == ForceEncounter.Shared.ForceEncounterConstants.Reasons.TargetDirectFallen)", StringComparison.Ordinal), "ForceEncounter direct-fallen target should not be routed to forced-route failure feedback");
        Assert(!package.Contains("成年无力应战结仇", StringComparison.Ordinal) &&
               !package.Contains("未成年无力应战结仇", StringComparison.Ordinal), "ForceEncounter direct-fallen target should not have failure/enmity feedback text");
        Assert(package.Contains("ForceEncounterEventIds.选项.战斗反馈继续.Key", StringComparison.Ordinal), "ForceEncounter combat result event should show a feedback page with a continue option");
        Assert(!package.Contains("EventOptions = Array.Empty<TaiwuEventOption>()", StringComparison.Ordinal), "ForceEncounter combat result event should not silently close without feedback");
        Assert(package.Contains("成年成功", StringComparison.Ordinal) &&
               package.Contains("未成年成功", StringComparison.Ordinal) &&
               package.Contains("成年成功村民", StringComparison.Ordinal) &&
               package.Contains("未成年成功村民", StringComparison.Ordinal), "ForceEncounter forced success feedback branches are missing");
        Assert(package.Contains("成年普通失败结仇", StringComparison.Ordinal) &&
               package.Contains("成年普通失败不结仇", StringComparison.Ordinal) &&
               package.Contains("成年普通失败村民", StringComparison.Ordinal) &&
               package.Contains("未成年普通失败结仇", StringComparison.Ordinal) &&
               package.Contains("未成年普通失败不结仇", StringComparison.Ordinal) &&
               package.Contains("未成年普通失败村民", StringComparison.Ordinal), "ForceEncounter forced failure feedback branches are missing");
        Assert(!package.Contains("死仇", StringComparison.Ordinal), "ForceEncounter forced feedback should not expose enmity as raw death-feud wording");
        Assert(!string.IsNullOrWhiteSpace(combatResultEventGuid), "ForceEncounter combat result event guid is empty");
        Assert(!string.IsNullOrWhiteSpace(capturedTargetDispositionEventGuid), "ForceEncounter captured-target disposition event guid is empty");
        Assert(!string.IsNullOrWhiteSpace(consentChoiceEventGuid), "ForceEncounter consent choice event guid is empty");
        Assert(!string.IsNullOrWhiteSpace(acceptedResultEventGuid), "ForceEncounter accepted result event guid is empty");
        Assert(package.Contains("ForceEncounterEventIds.结算模式.探测", StringComparison.Ordinal), "ForceEncounter entry event does not probe before combat");
        Assert(package.Contains("ForceEncounterEventIds.探测结果.需要战斗选择", StringComparison.Ordinal), "ForceEncounter entry event does not branch to combat choice");
        Assert(package.Contains("return ForceEncounterEventIds.事件.内层选择", StringComparison.Ordinal), "ForceEncounter should return the combat choice event guid from the option callback");
        Assert(!package.Contains("ActorNotAdult", StringComparison.Ordinal), "ForceEncounter should warn for special age groups instead of blocking the actor by age");
        Assert(!package.Contains("TargetNotAdult", StringComparison.Ordinal), "ForceEncounter should warn for special age groups instead of blocking the target by age");
        Assert(package.Contains("ActorBaby", StringComparison.Ordinal), "ForceEncounter should follow native non-baby interaction filtering for the actor");
        Assert(package.Contains("TargetBaby", StringComparison.Ordinal), "ForceEncounter should follow native non-baby interaction filtering for the target");
        Assert(!package.Contains("EventHelper.ToEvent(ForceEncounterEventIds.事件.内层选择)", StringComparison.Ordinal), "ForceEncounter should not route the combat choice through ToEvent plus an empty option return");
        Assert(!eventIdsSource.Contains("const string ModId", StringComparison.Ordinal), "ForceEncounter events should use the runtime package mod id, not a hard-coded development mod id");
        Assert(package.Contains("Package?.ModIdString", StringComparison.Ordinal), "ForceEncounter events should call the backend through the runtime package mod id");
        Assert(package.Contains("TaiwuModSettings.GetBool(GetRuntimeModId(), ForceEncounterConstants.Settings.DebugMode, true)", StringComparison.Ordinal), "ForceEncounter event debug logging should default on through the common settings facade");
        Assert(ReadModFile(mod.Name, "ForceEncounter.Backend", "ForceEncounter.Backend.csproj").Contains("TaiwuMod.Common\\TaiwuModSettings.cs", StringComparison.Ordinal), "ForceEncounter backend project should link the common settings facade");
        Assert(ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounter.Events.csproj").Contains("TaiwuMod.Common\\TaiwuModSettings.cs", StringComparison.Ordinal), "ForceEncounter events project should link the common settings facade");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.情难自已\.Key,[\s\S]*?Behavior\s*=\s*EventOptionBehavior\.BehaviorEgoistic,[\s\S]*?Important\s*=\s*false,[\s\S]*?OnOptionSelect\s*=\s*Execute"), "ForceEncounter outer hostile option should use native egoistic styling");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.正常发生关系\.Key,[\s\S]*?Important\s*=\s*false,[\s\S]*?OnOptionAvailableCheck\s*=\s*CanCommitAcceptedResolution,[\s\S]*?OnOptionSelect\s*=\s*NormalEncounter"), "ForceEncounter accepted commit option should re-check acceptance before native commit costs can be consumed");
        Assert(Regex.IsMatch(package, @"OptionKey\s*=\s*ForceEncounterEventIds\.选项\.强制关系\.Key,[\s\S]*?Behavior\s*=\s*EventOptionBehavior\.None,[\s\S]*?Important\s*=\s*true,[\s\S]*?OnOptionSelect\s*=\s*ForceCombat"), "ForceEncounter forced commit option should confirm the outer wait option instead of applying a second behavior effect");
        Assert(package.Contains("EventArgBox.OptionWaitConfirmKey", StringComparison.Ordinal), "ForceEncounter outer egoistic styling should use native wait-confirm to avoid applying behavior effects on entry");
        Assert(package.Contains("ForceEncounterEventIds.等待确认.外层预览", StringComparison.Ordinal), "ForceEncounter wait-confirm should use a stable mod-owned wait key");
        Assert(sharedIds.Contains("ConchShip_PresetKey_ConfirmWaitOptionSignal", StringComparison.Ordinal) &&
               package.Contains("ConfirmOuterWaitOption(ArgBox)", StringComparison.Ordinal), "ForceEncounter inner commit options should confirm the outer wait option so native behavior effects happen only on commit");
        Assert(Regex.IsMatch(package, @"ForceEncounterConstants\.Reasons\.NeedCombatChoice[\s\S]*?return ForceEncounterEventIds\.事件\.内层选择;[\s\S]*?ConfirmOuterWaitOption\(ArgBox\)"), "ForceEncounter accepted commit should not confirm the outer wait option when backend re-check falls back to forced choice");
        Assert(package.Contains("ForceEncounterEventIds.事件.亲密反馈", StringComparison.Ordinal), "ForceEncounter accepted branch should show an end feedback event instead of closing silently");
        Assert(package.Contains("BuildAcceptedResultContent", StringComparison.Ordinal), "ForceEncounter accepted result event should have dedicated feedback text");
        Assert(package.Contains("public static class 按钮", StringComparison.Ordinal), "ForceEncounter button texts should be grouped in the event text catalog");
        Assert(package.Contains("private static class 入口说明", StringComparison.Ordinal), "ForceEncounter event text should group entry-branch descriptions explicitly");
        Assert(package.Contains("private static class 亲密反馈", StringComparison.Ordinal), "ForceEncounter event text should group accepted-route feedback explicitly");
        Assert(package.Contains("private static class 强制反馈", StringComparison.Ordinal), "ForceEncounter event text should group forced-route feedback explicitly");
        Assert(!Regex.IsMatch(package, @"SetContent\(\s*"""), "ForceEncounter event options should not hard-code button text outside the text catalog");
        Assert(package.Contains("成年结算失败", StringComparison.Ordinal) &&
               package.Contains("未成年结算失败", StringComparison.Ordinal), "ForceEncounter accepted-route feedback should keep adult and minor fallback branches");
        Assert(package.Contains("RefreshPreviewCosts()", StringComparison.Ordinal) &&
               package.Contains("BuildPreviewCosts(modId)", StringComparison.Ordinal), "ForceEncounter should refresh outer action cost preview from runtime settings");
        Assert(package.Contains("BuildCommitCosts(modId)", StringComparison.Ordinal), "ForceEncounter should refresh inner commit costs from runtime settings");
        Assert(package.Contains("ForceEncounterConstants.Settings.ActionTimeCostDays", StringComparison.Ordinal), "ForceEncounter option costs should read the configured action time cost setting");
        Assert(package.Contains("ForceEncounterConstants.Costs.DefaultActionTimeDays", StringComparison.Ordinal), "ForceEncounter option costs should keep a shared default action time cost");
        Assert(package.Contains("return new List<OptionConsumeInfo>();", StringComparison.Ordinal), "ForceEncounter zero action-time cost should remove native cost display");
        Assert(package.Contains("new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, days, false)", StringComparison.Ordinal), "ForceEncounter outer cost preview must not auto-consume action time");
        Assert(package.Contains("new OptionConsumeInfo(ForceEncounterConstants.Costs.ActionTimeConsumeType, days, true)", StringComparison.Ordinal), "ForceEncounter inner commit costs must auto-consume action time");
        Assert(Regex.IsMatch(package, @"ForceEncounterEventText\.构造内层说明\(\s*亲密通过,\s*未成年,\s*有护卫\)", RegexOptions.Singleline), "ForceEncounter inner description should use explicit minor and guard variables");
        Assert(package.Contains("ForceEncounterEventRuntime.需要未成年提示(actorId, targetId)", StringComparison.Ordinal), "ForceEncounter should identify minor warning with a Chinese runtime helper");
        Assert(package.Contains("actor.GetAgeGroup() < ForceEncounterConstants.Gameplay.成年年龄组", StringComparison.Ordinal), "ForceEncounter minor checks should not classify elders as minors");
        Assert(!package.Contains("new OptionConsumeInfo((sbyte)16", StringComparison.Ordinal), "ForceEncounter should not consume a main attribute cost");
        Assert(package.Contains("（情难自已……）", StringComparison.Ordinal), "ForceEncounter hostile option should use native parenthesized option text");
        Assert(package.Contains("（半推半就……）", StringComparison.Ordinal), "ForceEncounter accepted branch prompt option is missing");
        Assert(package.Contains("（更进一步……）", StringComparison.Ordinal), "ForceEncounter forced branch prompt option is missing");
        Assert(package.Contains("其他话题", StringComparison.Ordinal), "ForceEncounter abandon options should use the native other-topic label");
        Assert(!package.Contains("就此作罢", StringComparison.Ordinal), "ForceEncounter should not use a custom abandon label where native hostile interactions use other-topic");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.NativeMainInteractionHeadEvent", StringComparison.Ordinal), "ForceEncounter abandon options should use the native main-interaction head event key");
        Assert(package.Contains("?? ForceEncounterEventIds.事件.原生敌对菜单", StringComparison.Ordinal), "ForceEncounter abandon options should return to the native hostile topic instead of closing the event");
        Assert(package.Contains("ForceEncounterEventIds.结算模式.亲密提交", StringComparison.Ordinal), "ForceEncounter accepted branch should commit only from the inner option");
        Assert(package.Contains("EventHelper.ChangeAlertnessOnAttack(targetId)", StringComparison.Ordinal), "ForceEncounter forced combat commit should apply the native attack alertness effect");
        Assert(package.Contains("TaiwuModSettings.GetBool(modId, ForceEncounterConstants.Settings.ApplyAlertnessOnCombatStart, true)", StringComparison.Ordinal), "ForceEncounter forced combat alertness should default on through the common settings facade");
        Assert(package.Contains("StartForcedCombat(ArgBox, GetRuntimeModId())", StringComparison.Ordinal), "ForceEncounter forced combat should pass the runtime mod id into configurable native alertness handling");
        Assert(package.Contains("new ForceEncounterGuardInterceptEvent()", StringComparison.Ordinal), "ForceEncounter should register a guard intercept event");
        Assert(package.Contains("ForceEncounterConstants.Settings.EnableGuardInterception", StringComparison.Ordinal), "ForceEncounter guard interception should read the runtime guard setting");
        Assert(package.Contains("ForceEncounterConstants.Settings.EnableTaiwuVillagerGuardInterception", StringComparison.Ordinal), "ForceEncounter Taiwu-villager guard interception should read its own runtime setting");
        Assert(package.Contains("ShouldUseGuardInterceptionForTarget(targetId, modId)", StringComparison.Ordinal), "ForceEncounter guard warning and combat should share target-specific guard settings");
        Assert(package.Contains("EventHelper.HasGuard(target)", StringComparison.Ordinal), "ForceEncounter forced combat should check native guard state");
        Assert(package.Contains("!IsTaiwuVillager(targetId) || IsTaiwuVillagerGuardInterceptionEnabled(modId)", StringComparison.Ordinal), "ForceEncounter Taiwu villagers should bypass guard interception unless their own guard setting is enabled");
        Assert(package.Contains("EventHelper.PrepareCombatEnemy(targetId, CombatConfig.DefKey.DieNormal, false)", StringComparison.Ordinal), "ForceEncounter forced combat should use native guarded enemy-team preparation");
        Assert(package.Contains("ForceEncounterEventIds.事件.护卫出面", StringComparison.Ordinal), "ForceEncounter forced combat should route to its own guard intercept event");
        Assert(!package.Contains("9638c0a8-fadf-4f6a-bb22-05f3aed994ed", StringComparison.Ordinal), "ForceEncounter should not jump to the native guard event because it hard-codes native attack result events");
        Assert(package.Contains("ForceEncounterConstants.ArgBox.GuardInterceptActive", StringComparison.Ordinal), "ForceEncounter guard combat should mark guard-intercept state so result handling cannot fail open");
        Assert(package.Contains("ForceEncounterEventIds.参数.战斗应用结仇后果", StringComparison.Ordinal) &&
               package.Contains("ForceEncounterConstants.Response.AppliedEnmity", StringComparison.Ordinal), "ForceEncounter combat feedback should reflect whether the backend applied the enmity consequence");
        Assert(package.Contains("enemyTeam.Count == 0", StringComparison.Ordinal) && package.Contains("ForceEncounterConstants.Reasons.GuardIntercepted", StringComparison.Ordinal), "ForceEncounter empty guarded enemy teams should settle with guard-intercept feedback instead of silently returning");
        Assert(Regex.IsMatch(package, @"EventHelper\.StartCombat\(\s*partnerId,\s*CombatConfig\.DefKey\.DieNormal,\s*ForceEncounterEventIds\.事件\.战斗反馈,\s*(ArgBox|argBox),\s*true\)", RegexOptions.Singleline), "ForceEncounter guard combat should fight the intercepting guard and return to ForceEncounter result handling");
        Assert(package.Contains("guardInterceptActive && !hasMainEnemy", StringComparison.Ordinal), "ForceEncounter guard combat result should fail closed when the native main enemy id is missing");
        Assert(Regex.IsMatch(package, @"!ArgBox\.Get\(ForceEncounterConstants\.ArgBox\.NativeCombatResult,[\s\S]*?StoreResult\(false,\s*false,\s*false,\s*ForceEncounterConstants\.Reasons\.MissingBattleResult\)[\s\S]*?return;"), "ForceEncounter combat result should not write backend failure side effects when native CombatResult is missing");
        Assert(package.Contains("成年护卫拦截村民", StringComparison.Ordinal) &&
               package.Contains("未成年护卫拦截村民", StringComparison.Ordinal), "ForceEncounter guard-intercept villager failure should have adult and minor feedback text");
        Assert(package.Contains("成年目标逃走村民", StringComparison.Ordinal) &&
               package.Contains("未成年目标逃走村民", StringComparison.Ordinal), "ForceEncounter target-escaped villager failure should have adult and minor feedback text");
        Assert(package.Contains("成年太吾撤退村民", StringComparison.Ordinal) &&
               package.Contains("未成年太吾撤退村民", StringComparison.Ordinal), "ForceEncounter actor-escaped villager failure should have adult and minor feedback text");
        Assert(!package.Contains("actor.GetCurrMainAttribute(4) < GlobalConfig.Instance.HarmfulActionCost", StringComparison.Ordinal), "ForceEncounter entry option should not block cost-free inspection");
        Assert(!package.Contains("InsufficientHarmfulActionCost", StringComparison.Ordinal), "ForceEncounter entry option should not diagnose inner commit costs");
        Assert(package.Contains("ForceEncounterEventIds.事件.战斗反馈", StringComparison.Ordinal), "ForceEncounter package does not reference combat result event guid");

        string backend = ReadModFile(mod.Name, "ForceEncounter.Backend", "BackendPlugin.cs");
        string backendProject = ReadProjectDirectorySource(mod.Name, "ForceEncounter.Backend");
        Assert(backend.Contains($"PluginConfig(ForceEncounterConstants.Mod.Id, ForceEncounterConstants.Mod.Author, \"{forceEncounterVersion}\")", StringComparison.Ordinal), "ForceEncounter backend plugin version should match config.lua Version");
        Assert(!backend.Contains("AddExecuteMethod(\"ForceEncounter\")", StringComparison.Ordinal), "ForceEncounter backend should not register methods under a hard-coded development mod id");
        Assert(backend.Contains("actorId != taiwuId", StringComparison.Ordinal), "ForceEncounter backend should reject non-Taiwu actors until its public interface supports NPC actors");
        Assert(backend.Contains("ForceEncounterConstants.Reasons.NonTaiwuActorNotAllowed", StringComparison.Ordinal), "ForceEncounter backend should return a clear reason for non-Taiwu actors");
        Assert(backend.Contains("GetBoolSetting(ForceEncounterConstants.Settings.DebugMode, true)", StringComparison.Ordinal), "ForceEncounter backend debug logging should default on when settings are unavailable");
        Assert(!backend.Contains("HandleRapeAction", StringComparison.Ordinal), "ForceEncounter should not call original combat-power-based HandleRapeAction");
        Assert(!backend.Contains("GetCombatPower()", StringComparison.Ordinal), "ForceEncounter should not use combat power as a success gate");
        Assert(!backend.Contains("\"NotAdult\"", StringComparison.Ordinal), "ForceEncounter backend should not hard-block special age groups");
        Assert(backend.Contains("ForceEncounterConstants.Reasons.BabyNotAllowed", StringComparison.Ordinal), "ForceEncounter backend should reject babies to match native non-baby interaction behavior");
        Assert(backend.Contains("ForceEncounterConstants.Settings.ForcedFavorabilityPenalty", StringComparison.Ordinal), "ForceEncounter backend should read the configured forced favorability penalty");
        Assert(backend.Contains("ForceEncounterConstants.Settings.TaiwuVillagerPenaltyPercent", StringComparison.Ordinal), "ForceEncounter backend should read the configured Taiwu villager penalty ratio");
        Assert(backend.Contains("ForceEncounterConstants.Settings.BecomeEnemyOnForcedRoute", StringComparison.Ordinal), "ForceEncounter backend should read the forced-route enmity setting");
        Assert(backend.Contains("ForceEncounterConstants.Settings.CreateSecretOnForcedSuccess", StringComparison.Ordinal), "ForceEncounter backend should read the forced-success secret setting");
        Assert(backend.Contains("ForceEncounterConstants.Settings.CreateSecretOnAcceptedSuccess", StringComparison.Ordinal), "ForceEncounter backend should read the accepted-success secret setting");
        Assert(!backend.Contains("ForceEncounterConstants.Settings.允许未成年", StringComparison.Ordinal), "ForceEncounter backend direct calls should not be blocked by the UI-only minor setting");
        Assert(!backend.Contains("ForceEncounterConstants.Reasons.未成年不允许", StringComparison.Ordinal), "ForceEncounter backend should preserve direct minor calls instead of returning 未成年不允许");
        Assert(!backend.Contains("RapeFavorabilityDelta = -30000", StringComparison.Ordinal), "ForceEncounter backend should not hard-code the forced favorability penalty");
        Assert(backend.Contains("CalculateForcedFavorabilityDelta", StringComparison.Ordinal), "ForceEncounter backend should calculate normal and Taiwu-villager favorability penalties through shared rules");
        Assert(backendProject.Contains("ForceEncounterConstants.Features.DeepValleyCloseFriend", StringComparison.Ordinal), "ForceEncounter should route 谷中密友 through the shared native feature id");
        Assert(!backend.Contains("ChangeFavorabilityOptionalRepeatedEvent(context, target, actor", StringComparison.Ordinal), "ForceEncounter accepted branch should not add extra favorability beyond native ordinary talk semantics");
        Assert(backendProject.Contains("actor.MakeLove(context, target, isRape: true)", StringComparison.Ordinal), "ForceEncounter success branch does not call MakeLove");
        Assert(backend.Contains("ForceEncounterConstants.Response.AppliedEnmity", StringComparison.Ordinal), "ForceEncounter backend should report whether forced settlement applied the enmity consequence");
        Assert(Regex.IsMatch(backend, @"ExecuteAcceptedEncounter[\s\S]*?bool\s+createSecret\s*=\s*ShouldCreateAcceptedSecret\(\)[\s\S]*?ForceEncounterEffectApplier\.ApplySuccess\([\s\S]*?addHatredRelation:\s*false,[\s\S]*?createSecret:\s*createSecret"), "ForceEncounter accepted commit should route through the effect applier without enmity and with the accepted-secret setting");
        Assert(Regex.IsMatch(backend, @"if \(success\)[\s\S]*?bool\s+createSecret\s*=\s*ShouldCreateForcedSecret\(\)[\s\S]*?ForceEncounterEffectApplier\.ApplySuccess\([\s\S]*?addHatredRelation:\s*appliedEnmity,[\s\S]*?createSecret:\s*createSecret"), "ForceEncounter forced success should route through the effect applier with forced-secret setting");
        Assert(Regex.IsMatch(backend, @"else[\s\S]*?int\s+favorabilityDelta\s*=\s*GetForcedFavorabilityDelta[\s\S]*?ForceEncounterEffectApplier\.ApplyFailure\([\s\S]*?addHatredRelation:\s*appliedEnmity,[\s\S]*?favorabilityDelta:\s*favorabilityDelta"), "ForceEncounter forced failure should route through the effect applier without success effects");
        Assert(backend.Contains("OnLoadedArchiveData()", StringComparison.Ordinal), "ForceEncounter backend should restore native menu extension after archive runtime reset");
        Assert(backend.Contains("OnEnterNewWorld()", StringComparison.Ordinal), "ForceEncounter backend should restore native menu extension after new-world runtime reset");
        Assert(backend.Contains("EventHelper.AddOptionToEvent", StringComparison.Ordinal), "ForceEncounter backend should use the formal event extension path after runtime reset");
        Assert(backendProject.Contains("AddRapeSucceed", StringComparison.Ordinal), "ForceEncounter success branch does not record rape success");
        Assert(backendProject.Contains("AddRapeFail", StringComparison.Ordinal), "ForceEncounter failure branch does not record rape failure");
        Assert(!backend.Contains("HatredRelationType", StringComparison.Ordinal), "ForceEncounter should not keep a direct hatred relation constant when using native enemy route");
        Assert(!backend.Contains("ChangeCurrMainAttribute(context, 4, -GlobalConfig.Instance.HarmfulActionCost)", StringComparison.Ordinal), "ForceEncounter backend should not double-consume costs handled by native option metadata");
        Assert(backend.Contains("CanResolveAsIntimateAcceptance", StringComparison.Ordinal), "ForceEncounter does not check intimate acceptance before combat");
        Assert(Regex.IsMatch(backend, @"ExecuteAcceptedEncounter[\s\S]*?!CanResolveAsIntimateAcceptance\(actor,\s*target,\s*""AcceptedCommitRecheck""\)[\s\S]*?ForceEncounterConstants\.Reasons\.NeedCombatChoice"), "ForceEncounter accepted commit should re-check intimate acceptance and refuse direct backend bypasses");
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
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(true, false, false, false, false, false, 4, 4, 2), "ForceEncounter should allow good spouse relation for neutral behavior");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(true, false, false, false, false, false, 3, 4, 2), "ForceEncounter should require both directions to pass intimate favor threshold");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, true, false, false, false, false, 3, 3, 3), "ForceEncounter should allow rebellious mutual lovers at a lower threshold");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, true, false, false, false, false, 4, 4, 0), "ForceEncounter should require stricter favor for upright mutual lovers");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, true, false, false, false, 3, 3, 3), "ForceEncounter should treat targets who unilaterally adore Taiwu as lover-like for intimate difficulty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, true, false, false, 5, 5, 0), "ForceEncounter should allow unattached 谷中密友 through the intimate route");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, true, false, true, 5, 3, 0), "ForceEncounter should allow attached 谷中密友 above native Favorite2 / 融洽 through the intimate route");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, true, false, true, 5, 2, 0), "ForceEncounter should send attached 谷中密友 at or below native Favorite2 / 融洽 to the combat route");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, false, true, false, 4, 4, 0), "ForceEncounter should let unattached Taiwu villagers use the villager intimacy leniency");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, false, true, true, 4, 4, 0), "ForceEncounter should cancel villager intimacy leniency when the target has an exclusive living attachment");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CanAcceptIntimateEncounter(false, false, false, false, true, true, 5, 5, 0), "ForceEncounter attached Taiwu villagers should still pass at normal intimacy difficulty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, false, 30) == -30000, "ForceEncounter normal forced route should default to the configured full penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, true, 30) == -9000, "ForceEncounter Taiwu villager forced route should use 30% of the normal forced penalty by default");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, true, 0) == 0, "ForceEncounter Taiwu villager penalty ratio should support 0%");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, true, 100) == -30000, "ForceEncounter Taiwu villager penalty ratio should support 100%");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(30000, true, 150) == -30000, "ForceEncounter Taiwu villager penalty ratio should clamp above 100%");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(40000, false, 30) == -30000, "ForceEncounter forced favorability penalty should clamp above the configured maximum");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(40000, true, 30) == -9000, "ForceEncounter Taiwu villager forced route should use the configured ratio after clamping above the configured maximum");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(-100, false, 30) == 0, "ForceEncounter forced favorability penalty should clamp below zero");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(0, false, 30) == 0, "ForceEncounter normal forced route should allow disabling favorability penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.CalculateForcedFavorabilityDelta(0, true, 30) == 0, "ForceEncounter Taiwu villager forced route should allow disabling favorability penalty");
        Assert(ForceEncounter.Backend.ForceEncounterRules.ShouldApplyReducedAcceptedFavorabilityPenalty(false, false, true, false), "ForceEncounter should reduce accepted favorability for true one-sided adoration");
        Assert(ForceEncounter.Backend.ForceEncounterRules.ShouldApplyReducedAcceptedFavorabilityPenalty(false, false, false, true), "ForceEncounter should reduce accepted favorability for attached deep-valley close friends");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.ShouldApplyReducedAcceptedFavorabilityPenalty(true, false, true, true), "ForceEncounter should not reduce accepted favorability for spouses");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.ShouldApplyReducedAcceptedFavorabilityPenalty(false, true, true, true), "ForceEncounter should not reduce accepted favorability for mutual lovers");
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

    private static int ExtractNestedConstShort(string source, string className, string name)
    {
        var classMatch = Regex.Match(
            source,
            $@"public\s+static\s+class\s+{Regex.Escape(className)}\s*\{{(?<body>.*?)\n\s*\}}",
            RegexOptions.Singleline);
        Assert(classMatch.Success, $"missing id group {className}");
        return ExtractConstShort(classMatch.Groups["body"].Value, name);
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
            mod.Projects.SelectMany(EnumerateProjectSourceFiles).Distinct(StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));
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

    private IEnumerable<string> EnumerateProjectSourceFiles(string project)
    {
        string projectPath = Path.Combine(_repo, project);
        string projectDir = Path.GetDirectoryName(projectPath)!;
        foreach (string source in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            yield return source;
        }

        string projectText = File.ReadAllText(projectPath);
        foreach (Match match in Regex.Matches(projectText, @"<Compile\s+Include=""([^""]+)"""))
        {
            string linkedSource = Path.GetFullPath(Path.Combine(projectDir, match.Groups[1].Value));
            if (File.Exists(linkedSource))
            {
                yield return linkedSource;
            }
        }
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
