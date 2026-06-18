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
                projects));
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

        string ids = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventIds.cs");
        string eventGuid = ExtractConst(ids, "EventGuid");
        string optionGuid = ExtractConst(ids, "OptionGuid");
        string combatResultEventGuid = ExtractConst(ids, "CombatResultEventGuid");

        string interaction = ReadModFile(mod.Name, "Config", "InteractionEventOption.lua");
        var eventPath = ParseLuaStringList(interaction, "MapBlockCharCustomButtonEventPath");
        var optionPath = ParseLuaStringList(interaction, "MapBlockCharCustomButtonEventOptionPath");
        Assert(eventPath.SequenceEqual(new[] { eventGuid }), "ForceEncounter interaction path must point directly at its own event");
        Assert(optionPath.SequenceEqual(new[] { optionGuid }), "ForceEncounter interaction option path must point at its own option");

        string package = ReadModFile(mod.Name, "ForceEncounter.Events", "ForceEncounterEventPackage.cs");
        Assert(package.Contains("new ForceEncounterEvent()", StringComparison.Ordinal), "ForceEncounter entry event is not registered");
        Assert(package.Contains("new ForceEncounterCombatResultEvent()", StringComparison.Ordinal), "ForceEncounter combat result event is not registered");
        Assert(package.Contains("EventHelper.StartCombat", StringComparison.Ordinal), "ForceEncounter does not start formal combat");
        Assert(package.Contains("CombatResultType.IsPlayerWin", StringComparison.Ordinal), "ForceEncounter does not map formal combat result");
        Assert(!string.IsNullOrWhiteSpace(combatResultEventGuid), "ForceEncounter combat result event guid is empty");
        Assert(package.Contains("CombatResultEventGuid", StringComparison.Ordinal), "ForceEncounter package does not reference combat result event guid");

        string backend = ReadModFile(mod.Name, "ForceEncounter.Backend", "BackendPlugin.cs");
        Assert(!backend.Contains("HandleRapeAction", StringComparison.Ordinal), "ForceEncounter should not call original combat-power-based HandleRapeAction");
        Assert(!backend.Contains("ForceSuccess", StringComparison.Ordinal), "ForceEncounter backend should not keep ForceSuccess");
        Assert(backend.Contains("actor.MakeLove(context, target, isRape: true)", StringComparison.Ordinal), "ForceEncounter success branch does not call MakeLove");
        Assert(backend.Contains("AddRapeSucceed", StringComparison.Ordinal), "ForceEncounter success branch does not record rape success");
        Assert(backend.Contains("AddRapeFail", StringComparison.Ordinal), "ForceEncounter failure branch does not record rape failure");
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
        Assert(ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(true, false, 51), "ForceEncounter should succeed after battle win and fertility above native threshold");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(true, false, 50), "ForceEncounter should keep native fertility threshold exclusive");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(false, false, 100), "ForceEncounter should fail after battle loss");
        Assert(!ForceEncounter.Backend.ForceEncounterRules.IsResolvedSuccess(true, true, 100), "ForceEncounter should not succeed when target is Taiwu");
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

sealed record ModEntry(string Name, string Status, string[] Projects);
