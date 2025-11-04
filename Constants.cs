namespace MechwarriorVRLauncher
{
    public static class Constants
    {
        // App constants
        public const string DefaultConfigFileName = "launcher_config.json";
        public const string CmdArgLaunchInjectDash = "--launch";
        public const string CmdArgLaunchInjectSlash = "/launch";

        // File/Folder Markers
        public const string VortexMarkerFile = "__folder_managed_by_vortex";
        public const string ModJsonFile = "mod.json";
        public const string ModListJsonFile = "modlist.json";
        public const string ModValidationRulesFile = "mwvr_mod_validation_rules.json";
        public const string UevrConfigFile = "config.txt";
        public const string MechWarriorShippingName = "MechWarrior-Win64-Shipping";

        // File Filters
        public const string ZipFileFilter = "Zip Files|*.zip|All Files|*.*";
        public const string MechWarriorConfigFilter = "Zip Files|*.zip|All Files|*.*";

        // Paths
        public const string DefaultUevrConfigPath = "%APPDATA%\\UnrealVRMod\\MechWarrior-Win64-Shipping";

        // JSON Properties
        public const string JsonPropertyDisplayName = "displayName";
        public const string JsonPropertyVersion = "version";
        public const string JsonPropertyDefaultLoadOrder = "defaultLoadOrder";

        // UEVR Configuration Keys
        public const string ConfigShowCursorKey = "FrameworkConfig_ShowCursorKey";
        public const string ConfigMenuKey = "FrameworkConfig_MenuKey";
        public const string ConfigResetViewKey = "VR_ResetStandingOriginKey";

        // Windows API Constants
        public const uint ProcessAll = 0x1F0FFF;
        public const uint MemCommit = 0x1000;
        public const uint MemReserve = 0x2000;
        public const uint PageReadWrite = 0x04;
        public const uint WaitTimeout = 0x00000102;
        public const uint Infinite = 0xFFFFFFFF;

        // Process and DLL Names
        public const string Mw5ProcessName = "MechWarrior-Win64-Shipping";
        public static readonly string[] UevrDllNames = new[] { "openxr_loader.dll", "UEVRBackend.dll" };

        // Steam Constants
        public const string Mw5AppId = "784080";

        // Steam Registry Paths
        public const string SteamRegistryPathCurrentUser = @"Software\Valve\Steam";
        public const string SteamRegistryPathLocalMachine = @"SOFTWARE\WOW6432Node\Valve\Steam";
        public const string SteamRegistryValuePath = "SteamPath";
        public const string SteamRegistryValueInstallPath = "InstallPath";

        // Steam Directory Names
        public const string SteamAppsFolder = "steamapps";
        public const string SteamCommonFolder = "common";
        public const string SteamLibraryFoldersFile = "libraryfolders.vdf";
        public const string SteamAppManifestPrefix = "appmanifest_";
        public const string SteamAppManifestSuffix = ".acf";

        // Steam VDF Regex Patterns
        public const string SteamVdfPathPattern = @"""path""\s+""([^""]+)""";
        public const string SteamVdfInstalldirPattern = @"""installdir""\s+""([^""]+)""";

        // MechWarrior 5 Directory Names
        public const string Mw5ModsFolder = "Mods";
        public const string Mw5MercsFolder = "MW5Mercs";
        public const string Mw5BinariesFolder = "Binaries";
        public const string Mw5Win64Folder = "Win64";

        // SteamVR Constants
        public const string SteamVrAppId = "250820";
        public const string SteamVrFolder = "SteamVR";
        public const string SteamVrBinPath = @"bin\win64";
        public const string SteamVrMonitorExe = "vrmonitor.exe";
        public const string SteamVrUri = $"steam://rungameid/{SteamVrAppId}";

        // Logging Constants
        public const int MaxLogBufferSize = 1000;

        // Installation Component Names
        public const string InstallComponentMods = "MW5 Mods";
        public const string InstallComponentUevr = "UEVR";
        public const string InstallComponentUevrConfig = "UEVR Config";

        // Download URLs
        public const string MechWarriorVrNexusModsUrl = "https://www.nexusmods.com/mechwarrior5mercenaries/mods/1009";
        public const string UevrGitHubReleasesUrl = "https://github.com/praydog/UEVR/releases";

        // GOG Constants
        public const string GogMw5GameId = "2147483045";
        public const string GogRegistryPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";
        public const string GogRegistryValuePath = "path";

    }
}
