using MechwarriorVRLauncher.Services;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;

namespace MechwarriorVRLauncher.Tests
{
    // Mock LoggingService for testing
    public class MockLoggingService : LoggingService
    {
        public List<string> Messages { get; } = new List<string>();

        public MockLoggingService()
        {
        }

        public override void LogMessage(string message)
        {
            Messages.Add(message);
        }

        public override List<string> GetLogBuffer()
        {
            return Messages;
        }
    }

    public class ModServiceTests
    {
        private readonly MockLoggingService _loggingService;

        public ModServiceTests()
        {
            _loggingService = new MockLoggingService();
        }

        private MockFileSystem CreateMockFileSystem()
        {
            return new MockFileSystem();
        }

        private void CreateTestModDirectory(MockFileSystem fs, string modsDir, string modName, string version = "1.0", int loadOrder = 100)
        {
            var modDir = fs.Path.Combine(modsDir, modName);
            fs.Directory.CreateDirectory(modDir);

            var modJson = new
            {
                displayName = modName,
                version = version,
                defaultLoadOrder = loadOrder
            };

            var modJsonPath = fs.Path.Combine(modDir, "mod.json");
            fs.File.WriteAllText(modJsonPath, JsonSerializer.Serialize(modJson));
        }

        private void CreateOrderRulesFile(MockFileSystem fs, List<ModCheck>? modChecks = null, List<string>? blacklist = null, List<ModOrderRule>? orderRules = null, List<List<string>>? chains = null)
        {
            var config = new ModOrderRulesConfig
            {
                ModChecks = modChecks ?? new List<ModCheck>(),
                Blacklist = blacklist ?? new List<string>(),
                OrderRules = orderRules ?? new List<ModOrderRule>(),
                LoadOrderChains = chains ?? new List<List<string>>()
            };

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            fs.Directory.CreateDirectory(baseDir);
            var orderRulesPath = fs.Path.Combine(baseDir, Constants.ModValidationRulesFile);
            fs.File.WriteAllText(orderRulesPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }

        [Fact]
        public void IsModBlacklisted_ReturnsTrueForBlacklistedMod()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            CreateOrderRulesFile(fs, blacklist: new List<string> { "BadMod", "IncompatibleMod" });
            var modService = new ModService(_loggingService, fs);

            // Act
            var isBlacklisted1 = modService.IsModBlacklisted("BadMod");
            var isBlacklisted2 = modService.IsModBlacklisted("IncompatibleMod");
            var isNotBlacklisted = modService.IsModBlacklisted("GoodMod");

            // Assert
            Assert.True(isBlacklisted1);
            Assert.True(isBlacklisted2);
            Assert.False(isNotBlacklisted);
        }

        [Fact]
        public void IsModBlacklisted_IsCaseInsensitive()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            CreateOrderRulesFile(fs, blacklist: new List<string> { "BadMod" });
            var modService = new ModService(_loggingService, fs);

            // Act & Assert
            Assert.True(modService.IsModBlacklisted("BadMod"));
            Assert.True(modService.IsModBlacklisted("badmod"));
            Assert.True(modService.IsModBlacklisted("BADMOD"));
        }

        [Fact]
        public void ScanInstalledMods_ReturnsEmptyListForNonExistentDirectory()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modService = new ModService(_loggingService, fs);
            var nonExistentDir = "/NonExistent";

            // Act
            var result = modService.ScanInstalledMods(nonExistentDir);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ScanInstalledMods_ParsesModsCorrectly()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "TestMod1", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "TestMod2", "2.0", 200);
            CreateTestModDirectory(fs, modsDir, "TestMod3", "3.0", 50);

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            Assert.Equal(3, result.Count);

            // Verify sorted by load order
            Assert.Equal("TestMod3", result[0].DisplayName);
            Assert.Equal(50, result[0].DefaultLoadOrder);

            Assert.Equal("TestMod1", result[1].DisplayName);
            Assert.Equal(100, result[1].DefaultLoadOrder);

            Assert.Equal("TestMod2", result[2].DisplayName);
            Assert.Equal(200, result[2].DefaultLoadOrder);
        }

        [Fact]
        public void ScanInstalledMods_DetectsVortexManagedMods()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "VortexMod", "1.0", 100);
            var vortexMarker = fs.Path.Combine(modsDir, "VortexMod", Constants.VortexMarkerFile);
            fs.File.WriteAllText(vortexMarker, "");

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            Assert.Single(result);
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("Vortex"));
        }

        [Fact]
        public void ScanInstalledMods_DetectsBlacklistedMods()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateOrderRulesFile(fs, blacklist: new List<string> { "BadMod" });
            CreateTestModDirectory(fs, modsDir, "BadMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "GoodMod", "1.0", 200);

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            Assert.Equal(2, result.Count);
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("blacklisted"));
            Assert.Contains(logMessages, m => m.Contains("⚠") && m.Contains("BadMod"));
        }

        [Fact]
        public void LoadOrderValidation_DetectsSimpleViolation()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods in wrong order: ModB (100) should load after ModA (200)
            CreateTestModDirectory(fs, modsDir, "ModA", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "ModB", "1.0", 100);

            // Create order rule: ModB must load after ModA
            CreateOrderRulesFile(fs,
                orderRules: new List<ModOrderRule>
                {
                    new ModOrderRule { Mod = "ModB", MustLoadAfter = new List<string> { "ModA" } }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("load order violation"));
            Assert.Contains(logMessages, m => m.Contains("ModA") && m.Contains("ModB"));
        }

        [Fact]
        public void LoadOrderValidation_PassesWhenOrderIsCorrect()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods in correct order: ModA (100) before ModB (200)
            CreateTestModDirectory(fs, modsDir, "ModA", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "ModB", "1.0", 200);

            // Create order rule: ModB must load after ModA
            CreateOrderRulesFile(fs,
                orderRules: new List<ModOrderRule>
                {
                    new ModOrderRule { Mod = "ModB", MustLoadAfter = new List<string> { "ModA" } }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.DoesNotContain(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void LoadOrderValidation_ChainDetectsViolation()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods in wrong order
            CreateTestModDirectory(fs, modsDir, "Mod1", "1.0", 300);
            CreateTestModDirectory(fs, modsDir, "Mod2", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "Mod3", "1.0", 100);

            // Create chain rule: Mod1 -> Mod2 -> Mod3
            CreateOrderRulesFile(fs,
                chains: new List<List<string>>
                {
                    new List<string> { "Mod1", "Mod2", "Mod3" }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void LoadOrderValidation_ChainPassesWhenOrderIsCorrect()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods in correct order
            CreateTestModDirectory(fs, modsDir, "Mod1", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "Mod2", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "Mod3", "1.0", 300);

            // Create chain rule: Mod1 -> Mod2 -> Mod3
            CreateOrderRulesFile(fs,
                chains: new List<List<string>>
                {
                    new List<string> { "Mod1", "Mod2", "Mod3" }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.DoesNotContain(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void LoadOrderValidation_IgnoresRulesForMissingMods()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Only create ModA
            CreateTestModDirectory(fs, modsDir, "ModA", "1.0", 100);

            // Create rule that references non-existent ModB
            CreateOrderRulesFile(fs,
                orderRules: new List<ModOrderRule>
                {
                    new ModOrderRule { Mod = "ModB", MustLoadAfter = new List<string> { "ModA" } }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert - should not throw, should not report violations
            Assert.Single(result);
            var logMessages = _loggingService.GetLogBuffer();
            Assert.DoesNotContain(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void LoadOrderValidation_HandlesMultipleDependencies()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods
            CreateTestModDirectory(fs, modsDir, "Core", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "Expansion", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "Patch", "1.0", 50); // Wrong! Should be after both

            // Create rule: Patch must load after both Core and Expansion
            CreateOrderRulesFile(fs,
                orderRules: new List<ModOrderRule>
                {
                    new ModOrderRule
                    {
                        Mod = "Patch",
                        MustLoadAfter = new List<string> { "Core", "Expansion" }
                    }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("load order violation"));
            // Should detect violations for both Core and Expansion
            Assert.Contains(logMessages, m => m.Contains("Core") && m.Contains("Patch"));
            Assert.Contains(logMessages, m => m.Contains("Expansion") && m.Contains("Patch"));
        }

        [Fact]
        public void LoadOrderValidation_CaseInsensitiveModNames()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods
            CreateTestModDirectory(fs, modsDir, "TestMod", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "DependentMod", "1.0", 100);

            // Create rule with different casing
            CreateOrderRulesFile(fs,
                orderRules: new List<ModOrderRule>
                {
                    new ModOrderRule
                    {
                        Mod = "DEPENDENTMOD",
                        MustLoadAfter = new List<string> { "testmod" }
                    }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert - should detect violation despite different casing
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void LoadOrderValidation_MultipleChains()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            // Create mods for two separate chains
            CreateTestModDirectory(fs, modsDir, "ChainA1", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "ChainA2", "1.0", 200);
            CreateTestModDirectory(fs, modsDir, "ChainB1", "1.0", 300);
            CreateTestModDirectory(fs, modsDir, "ChainB2", "1.0", 400);

            // Create two chains
            CreateOrderRulesFile(fs,
                chains: new List<List<string>>
                {
                    new List<string> { "ChainA1", "ChainA2" },
                    new List<string> { "ChainB1", "ChainB2" }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert - both chains should be valid
            var logMessages = _loggingService.GetLogBuffer();
            Assert.DoesNotContain(logMessages, m => m.Contains("load order violation"));
        }

        [Fact]
        public void ModChecks_AllRequiredPresent()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "ExpansionMod", "2.0", 200);

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", IsRequired = true },
                    new ModCheck { Mod = "ExpansionMod", IsRequired = true }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("Mod validation passed"));
            Assert.DoesNotContain(logMessages, m => m.Contains("missing"));
        }

        [Fact]
        public void ModChecks_DetectsMissingRequiredMod()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            // ExpansionMod is NOT installed

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", IsRequired = true },
                    new ModCheck { Mod = "ExpansionMod", IsRequired = true }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("ERROR") && m.Contains("required mod(s) are missing"));
            Assert.Contains(logMessages, m => m.Contains("ExpansionMod"));
        }

        [Fact]
        public void ModChecks_DetectsVersionMismatch()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "ExpansionMod", "1.5", 200);

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", Version = "1.0" },
                    new ModCheck { Mod = "ExpansionMod", Version = "2.0" } // Wrong version
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("incorrect version"));
            Assert.Contains(logMessages, m => m.Contains("ExpansionMod") && m.Contains("Expected: v2.0") && m.Contains("Installed: v1.5"));
        }

        [Fact]
        public void ModChecks_PassesWithCorrectVersion()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "ExpansionMod", "2.0", 200);

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", Version = "1.0", IsRequired = true },
                    new ModCheck { Mod = "ExpansionMod", Version = "2.0", IsRequired = true }
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("Mod validation passed"));
            Assert.DoesNotContain(logMessages, m => m.Contains("missing"));
            Assert.DoesNotContain(logMessages, m => m.Contains("incorrect version"));
        }

        [Fact]
        public void ModChecks_CaseInsensitive()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "COREMOD", IsRequired = true } // Different case
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("Mod validation passed"));
        }

        [Fact]
        public void ModChecks_OptionalModNotInstalledNoError()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            // OptionalMod is NOT installed, but isRequired = false

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", IsRequired = true },
                    new ModCheck { Mod = "OptionalMod", IsRequired = false } // Not required
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.DoesNotContain(logMessages, m => m.Contains("ERROR"));
            Assert.DoesNotContain(logMessages, m => m.Contains("OptionalMod"));
        }

        [Fact]
        public void ModChecks_OptionalModVersionCheckedWhenPresent()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "OptionalMod", "1.5", 200);

            CreateOrderRulesFile(fs,
                modChecks: new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", IsRequired = true },
                    new ModCheck { Mod = "OptionalMod", Version = "2.0", IsRequired = false } // Optional but version specified
                }
            );

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("incorrect version"));
            Assert.Contains(logMessages, m => m.Contains("OptionalMod") && m.Contains("Expected: v2.0") && m.Contains("Installed: v1.5"));
        }

        [Fact]
        public void CustomRulesInMechWarriorVRResources_OverridesProgramRules()
        {
            // Arrange
            var fs = CreateMockFileSystem();
            var modsDir = "/Mods";

            CreateTestModDirectory(fs, modsDir, "CoreMod", "1.0", 100);
            CreateTestModDirectory(fs, modsDir, "RequiredByCustom", "1.0", 200);

            // Create default rules in base directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            fs.Directory.CreateDirectory(baseDir);
            var baseRulesPath = fs.Path.Combine(baseDir, Constants.ModValidationRulesFile);
            var baseConfig = new ModOrderRulesConfig
            {
                ModChecks = new List<ModCheck>
                {
                    new ModCheck { Mod = "CoreMod", IsRequired = true }
                }
            };
            fs.File.WriteAllText(baseRulesPath, JsonSerializer.Serialize(baseConfig, new JsonSerializerOptions { WriteIndented = true }));

            // Create custom rules in MechWarriorVR/Resources directory (different requirements)
            var resourcesDir = fs.Path.Combine(modsDir, "MechWarriorVR", "Resources");
            fs.Directory.CreateDirectory(resourcesDir);
            var customRulesPath = fs.Path.Combine(resourcesDir, Constants.ModValidationRulesFile);
            var customConfig = new ModOrderRulesConfig
            {
                ModChecks = new List<ModCheck>
                {
                    new ModCheck { Mod = "RequiredByCustom", IsRequired = true }
                }
            };
            fs.File.WriteAllText(customRulesPath, JsonSerializer.Serialize(customConfig, new JsonSerializerOptions { WriteIndented = true }));

            var modService = new ModService(_loggingService, fs);

            // Act
            var result = modService.ScanInstalledMods(modsDir);

            // Assert
            var logMessages = _loggingService.GetLogBuffer();
            Assert.Contains(logMessages, m => m.Contains("Found") && m.Contains("MechWarriorVR/Resources"));
            Assert.Contains(logMessages, m => m.Contains("from MechWarriorVR/Resources"));
        }
    }
}
