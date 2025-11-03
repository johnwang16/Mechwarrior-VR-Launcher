using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;

namespace MechwarriorVRLauncher.Services
{
    public class ModInfo
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int DefaultLoadOrder { get; set; }
        public string DirectoryPath { get; set; } = string.Empty;
    }

    public class ModCheck
    {
        public string Mod { get; set; } = string.Empty;
        public string? Version { get; set; }
        public bool IsRequired { get; set; } = false;
    }

    public class ModOrderRule
    {
        public string Mod { get; set; } = string.Empty;
        public List<string> MustLoadAfter { get; set; } = new List<string>();
    }

    public class ModOrderRulesConfig
    {
        public List<ModCheck> ModChecks { get; set; } = new List<ModCheck>();
        public List<string> Blacklist { get; set; } = new List<string>();
        public List<ModOrderRule> OrderRules { get; set; } = new List<ModOrderRule>();
        public List<List<string>> LoadOrderChains { get; set; } = new List<List<string>>();
    }

    public class ModOrderViolation
    {
        public string Mod { get; set; } = string.Empty;
        public string RequiredBefore { get; set; } = string.Empty;
        public int ActualOrderMod { get; set; }
        public int ActualOrderRequired { get; set; }
    }

    public class ModValidationSummary
    {
        public int MissingRequiredMods { get; set; }
        public int VersionMismatches { get; set; }
        public int BlacklistedMods { get; set; }
        public int LoadOrderViolations { get; set; }

        public bool HasIssues => MissingRequiredMods > 0 || VersionMismatches > 0 || BlacklistedMods > 0 || LoadOrderViolations > 0;
        public bool HasErrors => MissingRequiredMods > 0;
        public bool HasWarnings => VersionMismatches > 0 || BlacklistedMods > 0 || LoadOrderViolations > 0;
    }

    public class ModService
    {
        private readonly LoggingService _loggingService;
        private readonly IFileSystem _fileSystem;
        private readonly List<ModCheck> _modChecks = new List<ModCheck>();
        private readonly HashSet<string> _blacklistedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ModOrderRule> _orderRules = new List<ModOrderRule>();
        private ModValidationSummary _lastValidationSummary = new ModValidationSummary();

        public ModService(LoggingService loggingService, IFileSystem? fileSystem = null)
        {
            _loggingService = loggingService;
            _fileSystem = fileSystem ?? new FileSystem();

            LoadBlacklist();
            LoadOrderRules();
        }

        public ModValidationSummary GetLastValidationSummary()
        {
            return _lastValidationSummary;
        }

        private void LoadBlacklist()
        {
            // Blacklist is now loaded from mod_validation_rules.json in LoadOrderRules()
            // This method is kept for backward compatibility but does nothing
        }

        private void LoadOrderRules(string? customRulesDirectory = null)
        {
            try
            {
                string orderRulesPath;
                string sourceLocation;

                // Check for custom rules in MechWarriorVR/Resources folder within the mods directory
                if (!string.IsNullOrEmpty(customRulesDirectory))
                {
                    var customRulesPath = _fileSystem.Path.Combine(customRulesDirectory, "MechWarriorVR", "Resources", Constants.ModValidationRulesFile);
                    if (_fileSystem.File.Exists(customRulesPath))
                    {
                        orderRulesPath = customRulesPath;
                        sourceLocation = "MechWarriorVR/Resources";
                        _loggingService.LogMessage($"Found {Constants.ModValidationRulesFile} in MechWarriorVR/Resources");
                    }
                    else
                    {
                        // Fall back to program directory
                        orderRulesPath = _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ModValidationRulesFile);
                        sourceLocation = "program directory";
                    }
                }
                else
                {
                    orderRulesPath = _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ModValidationRulesFile);
                    sourceLocation = "program directory";
                }

                if (!_fileSystem.File.Exists(orderRulesPath))
                {
                    _loggingService.LogMessage($"No mod order rules file found at {orderRulesPath}");
                    return;
                }

                var jsonText = _fileSystem.File.ReadAllText(orderRulesPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var config = JsonSerializer.Deserialize<ModOrderRulesConfig>(jsonText, options);

                if (config != null)
                {
                    // Clear existing rules when reloading
                    _modChecks.Clear();
                    _blacklistedMods.Clear();
                    _orderRules.Clear();

                    // Load mod checks
                    if (config.ModChecks != null && config.ModChecks.Count > 0)
                    {
                        foreach (var modCheck in config.ModChecks)
                        {
                            if (!string.IsNullOrWhiteSpace(modCheck.Mod))
                            {
                                _modChecks.Add(modCheck);
                            }
                        }
                        var requiredCount = _modChecks.Count(m => m.IsRequired);
                        var versionCheckCount = _modChecks.Count(m => !string.IsNullOrWhiteSpace(m.Version));
                        if (_modChecks.Count > 0)
                        {
                            _loggingService.LogMessage($"Loaded {_modChecks.Count} mod check(s) from {sourceLocation} ({requiredCount} required, {versionCheckCount} version checks)");
                        }
                    }

                    // Load blacklist
                    if (config.Blacklist != null && config.Blacklist.Count > 0)
                    {
                        foreach (var modName in config.Blacklist)
                        {
                            if (!string.IsNullOrWhiteSpace(modName))
                            {
                                _blacklistedMods.Add(modName);
                            }
                        }
                        _loggingService.LogMessage($"Loaded {_blacklistedMods.Count} blacklisted mod(s) from {sourceLocation}");
                    }

                    int totalRules = 0;
                    int individualRules = 0;
                    int chainRules = 0;

                    // Add individual rules
                    if (config.OrderRules != null)
                    {
                        foreach (var rule in config.OrderRules)
                        {
                            _orderRules.Add(rule);
                            individualRules++;
                            _loggingService.LogMessage($"  Rule: '{rule.Mod}' must load after {string.Join(", ", rule.MustLoadAfter.Select(m => $"'{m}'"))}");
                        }
                        totalRules += config.OrderRules.Count;
                    }

                    // Process load order chains and convert to individual rules
                    if (config.LoadOrderChains != null)
                    {
                        int chainIndex = 0;
                        foreach (var chain in config.LoadOrderChains)
                        {
                            if (chain == null || chain.Count < 2)
                                continue;

                            chainIndex++;
                            _loggingService.LogMessage($"  Chain {chainIndex}: {string.Join(" -> ", chain.Select(m => $"'{m}'"))}");

                            // For each consecutive pair in the chain, create a rule
                            for (int i = 1; i < chain.Count; i++)
                            {
                                _orderRules.Add(new ModOrderRule
                                {
                                    Mod = chain[i],
                                    MustLoadAfter = new List<string> { chain[i - 1] }
                                });
                                chainRules++;
                            }
                        }
                        totalRules += chainRules;
                    }

                    if (totalRules > 0)
                    {
                        _loggingService.LogMessage($"Loaded {totalRules} mod load order rule(s) from {sourceLocation} ({individualRules} individual, {chainRules} from chains)");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"Error loading mod order rules: {ex.Message}");
            }
        }

        public bool IsModBlacklisted(string modName)
        {
            return _blacklistedMods.Contains(modName);
        }

        public List<string> GetBlacklistedMods()
        {
            return _blacklistedMods.ToList();
        }

        private List<ModOrderViolation> ValidateModLoadOrder(List<ModInfo> mods)
        {
            var violations = new List<ModOrderViolation>();

            // Create a lookup dictionary for quick mod name -> mod info lookup (case-insensitive)
            var modOrderLookup = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
            {
                modOrderLookup[mod.DisplayName] = mod;
            }

            // Check each rule
            foreach (var rule in _orderRules)
            {
                // Check if the main mod is installed
                if (!modOrderLookup.TryGetValue(rule.Mod, out var mainMod))
                {
                    continue; // Rule doesn't apply if main mod isn't installed
                }

                // Check each dependency
                foreach (var requiredBefore in rule.MustLoadAfter)
                {
                    // Check if the dependency is installed
                    if (modOrderLookup.TryGetValue(requiredBefore, out var dependencyMod))
                    {
                        // Check if the order is violated (dependency should have a LOWER load order number than main mod)
                        if (dependencyMod.DefaultLoadOrder >= mainMod.DefaultLoadOrder)
                        {
                            violations.Add(new ModOrderViolation
                            {
                                Mod = mainMod.DisplayName,
                                RequiredBefore = dependencyMod.DisplayName,
                                ActualOrderMod = mainMod.DefaultLoadOrder,
                                ActualOrderRequired = dependencyMod.DefaultLoadOrder
                            });
                        }
                    }
                }
            }

            return violations;
        }

        public List<ModInfo> ScanInstalledMods(string modsDirectory, Action<string>? additionalLogger = null)
        {
            var modInfos = new List<ModInfo>();

            // Reset validation summary for this scan
            _lastValidationSummary = new ModValidationSummary();

            void Log(string message)
            {
                _loggingService.LogMessage(message);
                additionalLogger?.Invoke(message);
            }

            try
            {
                if (string.IsNullOrEmpty(modsDirectory) || !_fileSystem.Directory.Exists(modsDirectory))
                {
                    return modInfos;
                }

                // Check for custom validation rules in the mods directory
                LoadOrderRules(modsDirectory);

                Log($"Scanning for installed mods in: {modsDirectory}");

                var vortexManagedDirs = new List<string>();
                var blacklistedModsFound = new List<string>();
                var directories = _fileSystem.Directory.GetDirectories(modsDirectory);

                foreach (var dir in directories)
                {
                    // Check if this directory is managed by Vortex
                    var vortexMarker = _fileSystem.Path.Combine(dir, Constants.VortexMarkerFile);
                    if (_fileSystem.File.Exists(vortexMarker))
                    {
                        vortexManagedDirs.Add(_fileSystem.Path.GetFileName(dir));
                    }

                    var modJsonPath = _fileSystem.Path.Combine(dir, Constants.ModJsonFile);
                    if (_fileSystem.File.Exists(modJsonPath))
                    {
                        try
                        {
                            var jsonText = _fileSystem.File.ReadAllText(modJsonPath);
                            var jsonDoc = JsonDocument.Parse(jsonText);
                            var root = jsonDoc.RootElement;

                            var displayName = root.TryGetProperty(Constants.JsonPropertyDisplayName, out var displayNameProp) ? displayNameProp.GetString() ?? _fileSystem.Path.GetFileName(dir) : _fileSystem.Path.GetFileName(dir);

                            var modInfo = new ModInfo
                            {
                                DirectoryPath = dir,
                                DisplayName = displayName,
                                Version = root.TryGetProperty(Constants.JsonPropertyVersion, out var version) ? version.GetString() ?? "Unknown" : "Unknown",
                                DefaultLoadOrder = root.TryGetProperty(Constants.JsonPropertyDefaultLoadOrder, out var loadOrder) ? loadOrder.GetInt32() : 999
                            };

                            // Check if mod is blacklisted
                            if (IsModBlacklisted(displayName))
                            {
                                blacklistedModsFound.Add(displayName);
                            }

                            modInfos.Add(modInfo);
                        }
                        catch (Exception ex)
                        {
                            Log($"Error parsing mod.json in {_fileSystem.Path.GetFileName(dir)}: {ex.Message}");
                        }
                    }
                }

                // Sort by DefaultLoadOrder
                var sortedMods = modInfos.OrderBy(m => m.DefaultLoadOrder).ToList();

                if (sortedMods.Count > 0)
                {
                    Log($"Found {sortedMods.Count} installed mod(s):");
                    foreach (var mod in sortedMods)
                    {
                        string vortexNote = vortexManagedDirs.Contains(_fileSystem.Path.GetFileName(mod.DirectoryPath)) ? " [Vortex]" : "";
                        string blacklistNote = blacklistedModsFound.Contains(mod.DisplayName) ? " ⚠" : "";
                        Log($"  [{mod.DefaultLoadOrder}] {mod.DisplayName} v{mod.Version}{vortexNote}{blacklistNote}");
                    }
                }
                else
                {
                    Log("No mods found in the mods directory");
                }

                // Report Vortex-managed directories
                if (vortexManagedDirs.Count > 0)
                {
                    Log($"Note: {vortexManagedDirs.Count} mod(s) are managed by Vortex Mod Manager");
                }

                // Report blacklisted mods
                if (blacklistedModsFound.Count > 0)
                {
                    _lastValidationSummary.BlacklistedMods = blacklistedModsFound.Count;
                    Log($"WARNING: {blacklistedModsFound.Count} blacklisted/incompatible mod(s) detected:");
                    foreach (var mod in blacklistedModsFound)
                    {
                        Log($"  {mod}");
                    }
                }

                // Check for mod validations
                if (_modChecks.Count > 0)
                {
                    var missingRequiredMods = new List<string>();
                    var versionMismatches = new List<(string mod, string required, string actual)>();

                    foreach (var modCheck in _modChecks)
                    {
                        var installedMod = sortedMods.FirstOrDefault(m =>
                            string.Equals(m.DisplayName, modCheck.Mod, StringComparison.OrdinalIgnoreCase));

                        if (installedMod == null)
                        {
                            // Only error if mod is marked as required
                            if (modCheck.IsRequired)
                            {
                                missingRequiredMods.Add(modCheck.Mod);
                            }
                        }
                        else
                        {
                            // Mod exists - check version if specified
                            if (!string.IsNullOrWhiteSpace(modCheck.Version) &&
                                !string.Equals(installedMod.Version, modCheck.Version, StringComparison.OrdinalIgnoreCase))
                            {
                                versionMismatches.Add((modCheck.Mod, modCheck.Version, installedMod.Version));
                            }
                        }
                    }

                    if (missingRequiredMods.Count > 0)
                    {
                        _lastValidationSummary.MissingRequiredMods = missingRequiredMods.Count;
                        Log($"ERROR: {missingRequiredMods.Count} required mod(s) are missing:");
                        foreach (var mod in missingRequiredMods)
                        {
                            Log($"  {mod}");
                        }
                    }

                    if (versionMismatches.Count > 0)
                    {
                        _lastValidationSummary.VersionMismatches = versionMismatches.Count;
                        Log($"WARNING: {versionMismatches.Count} mod(s) have incorrect version:");
                        foreach (var (mod, required, actual) in versionMismatches)
                        {
                            Log($"  {mod} - Expected: v{required}, Installed: v{actual}");
                        }
                    }

                    var requiredChecks = _modChecks.Count(m => m.IsRequired);
                    if (requiredChecks > 0 && missingRequiredMods.Count == 0)
                    {
                        Log($"Mod validation passed - all {requiredChecks} required mod(s) found");
                    }
                }

                // Check for load order violations
                if (_orderRules.Count > 0)
                {
                    var violations = ValidateModLoadOrder(sortedMods);
                    if (violations.Count > 0)
                    {
                        _lastValidationSummary.LoadOrderViolations = violations.Count;
                        Log($"WARNING: {violations.Count} mod load order violation(s) detected:");
                        foreach (var violation in violations)
                        {
                            Log($"  [{violation.ActualOrderRequired}] {violation.RequiredBefore} must load BEFORE [{violation.ActualOrderMod}] {violation.Mod}");
                        }
                    }
                    else
                    {
                        Log($"Load order validation passed - all {_orderRules.Count} rule(s) satisfied");
                    }
                }

                return sortedMods;
            }
            catch (Exception ex)
            {
                Log($"Error scanning mods: {ex.Message}");
                return modInfos;
            }
        }
    }
}
