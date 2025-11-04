using MechwarriorVRLauncher.Models;
using MechwarriorVRLauncher.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace MechwarriorVRLauncher
{
    public partial class MainWindow : Window
    {
        // Windows message constants
        private const int WM_SETTINGCHANGE = 0x001A;

        private readonly ConfigService _configService;
        private readonly ZipExtractionService _zipService;
        private readonly SteamService _steamService;
        private readonly List<IGamePlatformService> _platformServices;
        private readonly LoggingService _loggingService;
        private readonly UEVRService _uevrService;
        private readonly KeycodeService _keycodeService;
        private readonly ModService _modService;
        private LauncherConfig _currentConfig;
        private int _showCursorKeyCode = 36;
        private int _menuKeyCode = 45;
        private int _resetViewKeyCode = 120;

        public MainWindow()
        {
            this.InitializeComponent();
            _configService = new ConfigService();
            _zipService = new ZipExtractionService();
            _loggingService = new LoggingService();
            _steamService = new SteamService(_loggingService);
            _uevrService = new UEVRService(_loggingService);
            _keycodeService = new KeycodeService();
            _modService = new ModService(_loggingService);
            _zipService.ProgressChanged += OnZipProgressChanged;
            _currentConfig = new LauncherConfig();

            // Initialize platform services list with all supported platforms
            _platformServices = new List<IGamePlatformService>
            {
                _steamService,
                new GOGService(_loggingService)
            };

            this.Loaded += MainWindow_Loaded;

            // Set window title with version
            this.Title = $"MechWarrior VR Launcher v{GetVersion()}";

            LoadConfigOnStartup();
        }

        public static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check if auto-launch and inject was requested via command line
            if (App.AutoLaunchAndInject)
            {
                LogMessage("Auto-launch and inject requested via command line");
                await LaunchAndInjectAsync(null);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hook into Windows message loop to listen for theme changes
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Listen for system settings changes
            if (msg == WM_SETTINGCHANGE)
            {
                // Check if theme is set to Auto and reapply if so
                if (_currentConfig.Theme == "Auto")
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ApplyTheme("Auto");
                    }));
                }
            }

            return IntPtr.Zero;
        }

        // Public methods for accessing config and services
        public LauncherConfig GetCurrentConfig()
        {
            return _currentConfig;
        }

        public async Task SaveConfigAsync()
        {
            try
            {
                await _configService.SaveConfigAsync(_currentConfig);
            }
            catch (Exception ex)
            {
                LogMessage($"Error auto-saving configuration: {ex.Message}");
            }
        }

        // Window openers
        private void OpenInstallerButton_Click(object sender, RoutedEventArgs e)
        {
            var setupWindow = new SetupWindow(this);
            setupWindow.Owner = this;
            setupWindow.Show();
        }

        private async void OpenLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfigFromUI();

            // Start the launch and inject process without opening the setup window
            // Logs will be buffered and visible when user manually opens setup window
            await LaunchAndInjectAsync(null);
        }

        private async void LoadConfigOnStartup()
        {
            try
            {
                _currentConfig = await _configService.LoadConfigAsync();
                UpdateUIFromConfig();

                // Apply saved theme
                ApplyTheme(_currentConfig.Theme);

                // Auto-detect MechWarrior directory if not set
                if (string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory))
                {
                    AutoDetectModsDirectory();
                }

                LogMessage("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading config: {ex.Message}");
            }
        }

        private void UpdateUIFromConfig()
        {
            // UI elements are now in SetupWindow - this method is kept for backward compatibility
            // Set the keycodes from config
            _showCursorKeyCode = _currentConfig.ShowCursorKeyCode;
            _menuKeyCode = _currentConfig.MenuKeyCode;
            _resetViewKeyCode = _currentConfig.ResetViewKeyCode;

            // Try to read existing UEVR config.txt to get current keybindings
            ReadExistingUEVRConfig();
        }

        public void ScanInstalledMods(SetupWindow? window = null)
        {
            var modsDirectory = ExpandEnvironmentVariables(_currentConfig.MechwarriorModsDirectory);

            // LoggingService automatically routes to window via log handler pattern
            _modService.ScanInstalledMods(modsDirectory);
        }

        public Services.ModValidationSummary GetModValidationSummary()
        {
            return _modService.GetLastValidationSummary();
        }

        public void ApplyTheme(string theme)
        {
            try
            {
                // If theme is "Auto", detect system theme
                string actualTheme = theme;
                if (theme == "Auto")
                {
                    actualTheme = GetSystemTheme();
                }

                // Clear existing theme dictionaries
                Application.Current.Resources.MergedDictionaries.Clear();

                // Load the selected theme using pack URI
                var themeUri = new Uri($"/MechwarriorVRLauncher;component/Themes/{actualTheme}Theme.xaml", UriKind.Relative);
                var themeDict = new ResourceDictionary { Source = themeUri };
                Application.Current.Resources.MergedDictionaries.Add(themeDict);

                // Update hero image based on theme
                var heroImagePath = actualTheme == "Dark" ? "assets/Hero_Dark.png" : "assets/Hero.png";
                HeroImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(heroImagePath, UriKind.Relative));

                // Save theme preference (save the user's choice, not the resolved theme)
                _currentConfig.Theme = theme;
                Task.Run(async () => await SaveConfigAsync());
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying theme: {ex.Message}");
            }
        }

        private string GetSystemTheme()
        {
            try
            {
                // Read Windows registry to determine if apps use light theme
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AppsUseLightTheme");
                        if (value is int themeValue)
                        {
                            // 0 = Dark mode, 1 = Light mode
                            return themeValue == 0 ? "Dark" : "Light";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Could not read system theme preference: {ex.Message}");
            }

            // Default to Light if unable to read registry
            return "Light";
        }

        private void UpdateConfigFromUI()
        {
            // UI elements are now in SetupWindow - this method is kept for backward compatibility
            // Only update key codes here since they're stored in MainWindow
            _currentConfig.ShowCursorKeyCode = _showCursorKeyCode;
            _currentConfig.MenuKeyCode = _menuKeyCode;
            _currentConfig.ResetViewKeyCode = _resetViewKeyCode;
        }


        public void AutoDetectModsDirectory(SetupWindow? window = null, string? platformFilter = null)
        {
            var logAction = window != null ? (Action<string>)window.LogMessage : LogMessage;

            // Filter services by platform if specified
            var servicesToTry = string.IsNullOrEmpty(platformFilter)
                ? _platformServices
                : _platformServices.Where(s => s.PlatformName.Equals(platformFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (servicesToTry.Count == 0)
            {
                logAction($"Unknown platform: {platformFilter}");
                return;
            }

            // Try each platform service in order
            foreach (var platformService in servicesToTry)
            {
                // Check if the launcher is installed first
                if (!platformService.IsLauncherInstalled())
                {
                    logAction($"{platformService.PlatformName} is not installed, skipping...");
                    continue;
                }

                logAction($"Searching for MechWarrior 5 via {platformService.PlatformName}...");
                var modsPath = platformService.DetectMechWarriorModsDirectory();

                if (!string.IsNullOrEmpty(modsPath))
                {
                    _currentConfig.MechwarriorModsDirectory = modsPath;
                    logAction($"Found MechWarrior 5 ({platformService.PlatformName}) Mods directory: {modsPath}");
                    ScanInstalledMods(window);
                    return;
                }

                // Service will have logged detailed reason for failure
            }

            // No platform succeeded
            logAction($"Auto-detection unsuccessful. Please use 'Browse...' to manually select your Mods folder.");
        }




        public async Task<bool> InstallModsAsync(SetupWindow window)
        {
            // Validate required inputs
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory))
            {
                missingFields.Add("• MechWarrior Mods Directory");
            }

            if (_currentConfig.ModZipFiles.Count == 0)
            {
                missingFields.Add("• At least one MechWarrior Mod Zip File");
            }

            if (missingFields.Count > 0)
            {
                string message = "The following required fields are not specified:\n\n" +
                                string.Join("\n", missingFields) +
                                "\n\nPlease configure these fields before installing mods.";
                window.LogMessage("Cannot install mods: Missing required configuration");
                ShowErrorDialog("Missing Configuration", message);
                return false;
            }

            try
            {
                window.SetInstallButtonsEnabled(false);
                window.SetProgressIndeterminate(true);
                window.SetStatusText("Installing MW5 Mods...");

                var modsDirectory = ExpandEnvironmentVariables(_currentConfig.MechwarriorModsDirectory);
                var modZipFiles = _currentConfig.ModZipFiles.Select(ExpandEnvironmentVariables).ToArray();

                // Get directories that will be installed
                var modDirectoriesToInstall = GetModDirectoriesFromZips(modZipFiles);

                // Check for Vortex-managed mods that will be overwritten
                if (Directory.Exists(modsDirectory))
                {
                    // Check mods that will be overwritten (regardless of clean install setting)
                    var vortexManagedDirs = new List<string>();
                    foreach (var modDir in modDirectoriesToInstall)
                    {
                        var fullPath = Path.Combine(modsDirectory, modDir);
                        if (Directory.Exists(fullPath))
                        {
                            var vortexMarker = Path.Combine(fullPath, Constants.VortexMarkerFile);
                            if (File.Exists(vortexMarker))
                            {
                                vortexManagedDirs.Add(modDir);
                            }
                        }
                    }

                    if (vortexManagedDirs.Count > 0)
                    {
                        string action = window.GetCleanInstallEnabled() ? "delete and replace" : "overwrite";
                        string vortexMessage = $"WARNING: {vortexManagedDirs.Count} mod(s) are managed by Vortex Mod Manager:\n\n" +
                                             string.Join("\n", vortexManagedDirs.Select(d => $"  • {d}")) +
                                             $"\n\nInstallation will {action} these mods.\n\n" +
                                             "Do you want to continue?";

                        var result = MessageBox.Show(vortexMessage, "Vortex-Managed Mods Detected", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result != MessageBoxResult.Yes)
                        {
                            window.LogMessage("Installation cancelled by user due to Vortex-managed mods warning");
                            return false;
                        }

                        window.LogMessage($"User acknowledged Vortex warning for {vortexManagedDirs.Count} mod(s)");
                    }
                }

                // Check if clean install is enabled
                if (window.GetCleanInstallEnabled())
                {
                    window.LogMessage("Clean install enabled - analyzing zip files...");

                    // Validate mods directory before deleting anything
                    if (!ValidateModsDirectoryForDeletion(modsDirectory, out string validationError))
                    {
                        window.LogMessage($"SAFETY CHECK FAILED: {validationError}");
                        ShowErrorDialog("Safety Validation Failed", validationError);
                        return false;
                    }

                    if (modDirectoriesToInstall.Count > 0)
                    {
                        // Check which directories actually exist
                        var existingDirectories = GetExistingModDirectories();

                        // Only delete if there are directories to delete
                        if (existingDirectories.Count > 0)
                        {
                            window.LogMessage($"Found {existingDirectories.Count} existing mod directories");
                            window.LogMessage($"Safety check passed: Validated game directory structure");

                            // Proceed with deletion
                            foreach (var dir in existingDirectories)
                            {
                                var fullPath = Path.Combine(modsDirectory, dir);
                                try
                                {
                                    window.LogMessage($"  Deleting: {dir}");
                                    Directory.Delete(fullPath, true);
                                }
                                catch (Exception ex)
                                {
                                    window.LogMessage($"  Error deleting {dir}: {ex.Message}");
                                }
                            }
                            window.LogMessage("Clean install preparation complete");
                        }
                        else
                        {
                            window.LogMessage("No existing mod directories to delete");
                        }
                    }
                }

                window.LogMessage($"Extracting {modZipFiles.Length} mod file(s)...");
                var modSuccess = await _zipService.ExtractMultipleZipsAsync(modZipFiles, modsDirectory);
                window.LogMessage($"Extracted {modSuccess}/{modZipFiles.Length} mod file(s)");

                window.SetStatusText("MW5 Mods installation completed");

                // Scan and display installed mods
                ScanInstalledMods(window);

                return true;
            }
            catch (Exception ex)
            {
                window.LogMessage($"Error installing mods: {ex.Message}");
                ShowErrorDialog("Mods Installation Error", $"Failed to install mods: {ex.Message}");
                return false;
            }
        }

        private List<string> GetExistingModDirectories()
        {
            var existingDirectories = new List<string>();

            if (string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory) || _currentConfig.ModZipFiles.Count == 0)
            {
                return existingDirectories;
            }

            var modsDirectory = ExpandEnvironmentVariables(_currentConfig.MechwarriorModsDirectory);
            var modZipFiles = _currentConfig.ModZipFiles.Select(ExpandEnvironmentVariables).ToArray();
            var modDirectoriesToInstall = GetModDirectoriesFromZips(modZipFiles);

            foreach (var dir in modDirectoriesToInstall)
            {
                var fullPath = Path.Combine(modsDirectory, dir);
                if (Directory.Exists(fullPath))
                {
                    existingDirectories.Add(dir);
                }
            }

            return existingDirectories;
        }

        public List<string> GetCleanInstallDeletionInfo(bool installMods, bool installUEVR, bool installUEVRConfig)
        {
            var deletionInfo = new List<string>();

            // Check mod directories only if installing mods
            if (installMods && !string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory))
            {
                var modsDirectory = ExpandEnvironmentVariables(_currentConfig.MechwarriorModsDirectory);
                var existingModDirs = GetExistingModDirectories();
                foreach (var dir in existingModDirs)
                {
                    var fullPath = Path.Combine(modsDirectory, dir);
                    deletionInfo.Add(fullPath);
                }
            }

            // Check UEVR directory only if installing UEVR
            if (installUEVR && !string.IsNullOrWhiteSpace(_currentConfig.UEVRInstallDirectory))
            {
                var uevrInstallDirectory = ExpandEnvironmentVariables(_currentConfig.UEVRInstallDirectory);
                if (Directory.Exists(uevrInstallDirectory))
                {
                    deletionInfo.Add(uevrInstallDirectory);
                }
            }

            // Check UEVR config directory only if installing UEVR config
            if (installUEVRConfig && !string.IsNullOrWhiteSpace(_currentConfig.UEVRConfigDirectory))
            {
                var configDirectory = ExpandEnvironmentVariables(_currentConfig.UEVRConfigDirectory);
                if (Directory.Exists(configDirectory))
                {
                    deletionInfo.Add(configDirectory);
                }
            }

            return deletionInfo;
        }

        private List<string> GetModDirectoriesFromZips(string[] zipFiles)
        {
            var modDirectories = new HashSet<string>();

            foreach (var zipFile in zipFiles)
            {
                try
                {
                    if (!File.Exists(zipFile))
                    {
                        // Skip logging since we don't have a window context here
                        continue;
                    }

                    using (var archive = ZipFile.OpenRead(zipFile))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            // Get the first directory in the path
                            var parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                            {
                                // The first part is the root directory
                                modDirectories.Add(parts[0]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip logging since we don't have a window context here
                }
            }

            return modDirectories.ToList();
        }

        public async Task<bool> InstallUEVRAsync(SetupWindow window)
        {
            // Validate required inputs
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRZipFile))
            {
                missingFields.Add("• UEVR Zip File");
            }
            else
            {
                var tempZipFile = ExpandEnvironmentVariables(_currentConfig.UEVRZipFile);
                if (!File.Exists(tempZipFile))
                {
                    missingFields.Add($"• UEVR Zip File (file not found: {tempZipFile})");
                }
            }

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRInstallDirectory))
            {
                missingFields.Add("• UEVR Install Directory");
            }

            if (missingFields.Count > 0)
            {
                string message = "The following required fields are not specified:\n\n" +
                                string.Join("\n", missingFields) +
                                "\n\nPlease configure these fields before installing UEVR.";
                window.LogMessage("Cannot install UEVR: Missing required configuration");
                ShowErrorDialog("Missing Configuration", message);
                return false;
            }

            var uevrZipFile = ExpandEnvironmentVariables(_currentConfig.UEVRZipFile);

            try
            {
                var uevrInstallDirectory = ExpandEnvironmentVariables(_currentConfig.UEVRInstallDirectory);

                // Check if clean install is enabled
                if (window.GetCleanInstallEnabled() && Directory.Exists(uevrInstallDirectory))
                {
                    window.LogMessage("Clean install enabled - validating UEVR directory...");

                    // Validate UEVR directory before deleting
                    if (!ValidateUEVRDirectoryForDeletion(uevrInstallDirectory, out string validationError))
                    {
                        window.LogMessage($"SAFETY CHECK FAILED: {validationError}");
                        ShowErrorDialog("Safety Validation Failed", validationError);
                        return false;
                    }

                    window.LogMessage("Safety check passed: Validated UEVR directory structure");
                    window.LogMessage("Deleting existing UEVR installation...");

                    try
                    {
                        Directory.Delete(uevrInstallDirectory, true);
                        window.LogMessage("Existing UEVR installation deleted");
                    }
                    catch (Exception ex)
                    {
                        window.LogMessage($"Warning: Could not fully delete UEVR directory: {ex.Message}");
                    }
                }

                window.LogMessage($"Extracting UEVR to: {uevrInstallDirectory}");
                await _zipService.ExtractZipAsync(uevrZipFile, uevrInstallDirectory);
                window.LogMessage("UEVR installation completed");

                window.SetStatusText("UEVR installation completed");

                return true;
            }
            catch (Exception ex)
            {
                window.LogMessage($"Error installing UEVR: {ex.Message}");
                ShowErrorDialog("UEVR Installation Error", $"Failed to install UEVR: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InstallUEVRConfigAsync(SetupWindow window)
        {
            // Validate required inputs
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRConfigFile))
            {
                missingFields.Add("• UEVR Config Zip File");
            }
            else
            {
                var tempConfigZip = ExpandEnvironmentVariables(_currentConfig.UEVRConfigFile);
                if (!File.Exists(tempConfigZip))
                {
                    missingFields.Add($"• UEVR Config Zip File (file not found: {tempConfigZip})");
                }
            }

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRConfigDirectory))
            {
                missingFields.Add("• UEVR Config Directory");
            }

            if (missingFields.Count > 0)
            {
                string message = "The following required fields are not specified:\n\n" +
                                string.Join("\n", missingFields) +
                                "\n\nPlease configure these fields before installing UEVR Config.";
                window.LogMessage("Cannot install UEVR Config: Missing required configuration");
                ShowErrorDialog("Missing Configuration", message);
                return false;
            }

            var configZipFile = ExpandEnvironmentVariables(_currentConfig.UEVRConfigFile);

            try
            {
                window.SetStatusText("Installing UEVR Config...");

                var configDirectory = ExpandEnvironmentVariables(_currentConfig.UEVRConfigDirectory);

                // Check if clean install is enabled
                if (window.GetCleanInstallEnabled() && Directory.Exists(configDirectory))
                {
                    window.LogMessage("Clean install enabled - validating UEVR config directory...");

                    // Validate UEVR config directory before deleting
                    if (!ValidateUEVRConfigDirectoryForDeletion(configDirectory, out string validationError))
                    {
                        window.LogMessage($"SAFETY CHECK FAILED: {validationError}");
                        ShowErrorDialog("Safety Validation Failed", validationError);
                        return false;
                    }

                    window.LogMessage("Safety check passed: Validated UEVR config directory structure");
                    window.LogMessage("Deleting existing UEVR config...");

                    try
                    {
                        Directory.Delete(configDirectory, true);
                        window.LogMessage("Existing UEVR config deleted");
                    }
                    catch (Exception ex)
                    {
                        window.LogMessage($"Warning: Could not fully delete UEVR config directory: {ex.Message}");
                    }
                }

                // Ensure the directory structure exists
                if (!Directory.Exists(configDirectory))
                {
                    var parentDirectory = Path.GetDirectoryName(configDirectory);
                    if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
                    {
                        window.LogMessage($"Creating UEVR config parent directory: {parentDirectory}");
                        Directory.CreateDirectory(parentDirectory);
                    }

                    window.LogMessage($"Creating UEVR config directory: {configDirectory}");
                    Directory.CreateDirectory(configDirectory);
                }

                window.LogMessage($"Extracting UEVR config to: {configDirectory}");
                await _zipService.ExtractZipAsync(configZipFile, configDirectory);
                window.LogMessage("UEVR config extraction completed");

                // Modify config.txt with custom settings
                ModifyUEVRConfigFile(configDirectory, _currentConfig.ShowCursorKeyCode, _currentConfig.MenuKeyCode, _currentConfig.ResetViewKeyCode);

                window.SetStatusText("UEVR Config installation completed");
                return true;
            }
            catch (Exception ex)
            {
                window.LogMessage($"Error installing UEVR config: {ex.Message}");
                ShowErrorDialog("UEVR Config Installation Error", $"Failed to install UEVR config: {ex.Message}");
                return false;
            }
        }

        public bool ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory))
                return false;

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRInstallDirectory))
                return false;

            if (string.IsNullOrWhiteSpace(_currentConfig.UEVRConfigDirectory))
                return false;

            // Check if UEVR zip exists
            if (!string.IsNullOrEmpty(_currentConfig.UEVRZipFile))
            {
                var uevrZipFile = ExpandEnvironmentVariables(_currentConfig.UEVRZipFile);
                if (!File.Exists(uevrZipFile))
                    return false;
            }

            // Check if UEVR config file exists
            if (!string.IsNullOrEmpty(_currentConfig.UEVRConfigFile))
            {
                var uevrConfigFile = ExpandEnvironmentVariables(_currentConfig.UEVRConfigFile);
                if (!File.Exists(uevrConfigFile))
                    return false;
            }

            // Check if all mod zip files exist
            foreach (var modZip in _currentConfig.ModZipFiles)
            {
                var expandedModZip = ExpandEnvironmentVariables(modZip);
                if (!File.Exists(expandedModZip))
                    return false;
            }

            return true;
        }

        // Safety validation methods to prevent accidental deletion of drives/wrong directories
        private bool ValidateModsDirectoryForDeletion(string modsDirectory, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if path is a root directory (like C:\ or D:\)
            try
            {
                var dirInfo = new DirectoryInfo(modsDirectory);

                // The mods directory should be like: C:\...\MechWarrior 5 Mercenaries\MW5Mercs\Mods
                // So 1 level up should be MW5Mercs which contains Binaries\Win64\MechWarrior-Win64-Shipping.exe
                var gameDirectory = dirInfo.Parent?.FullName;
                if (string.IsNullOrEmpty(gameDirectory))
                {
                    errorMessage = "Cannot validate mods directory: Unable to determine parent directory";
                    return false;
                }

                var gameExePath = Path.Combine(gameDirectory, "Binaries", "Win64", $"{Constants.Mw5ProcessName}.exe");
                if (!File.Exists(gameExePath))
                {
                    errorMessage = $"Cannot validate mods directory: Expected game executable not found at {gameExePath}. This may not be a valid MechWarrior 5 mods directory.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating mods directory: {ex.Message}";
                return false;
            }
        }

        private bool ValidateUEVRDirectoryForDeletion(string uevrDirectory, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if path is a root directory (like C:\ or D:\)
            try
            {
                var dirInfo = new DirectoryInfo(uevrDirectory);
                if (dirInfo.Parent == null)
                {
                    errorMessage = $"Cannot delete UEVR directory: Path is a root drive ({uevrDirectory})";
                    return false;
                }

                // Check if directory exists and contains UEVRInjector.exe
                if (!Directory.Exists(uevrDirectory))
                {
                    // If directory doesn't exist yet, it's safe (nothing to delete)
                    return true;
                }

                var injectorPath = Path.Combine(uevrDirectory, "UEVRInjector.exe");
                if (!File.Exists(injectorPath))
                {
                    errorMessage = $"Cannot validate UEVR directory: Expected UEVRInjector.exe not found at {injectorPath}. This may not be a valid UEVR installation directory.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating UEVR directory: {ex.Message}";
                return false;
            }
        }

        private bool ValidateUEVRConfigDirectoryForDeletion(string configDirectory, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if path is a root directory (like C:\ or D:\)
            try
            {
                var dirInfo = new DirectoryInfo(configDirectory);
                if (dirInfo.Parent == null)
                {
                    errorMessage = $"Cannot delete UEVR config directory: Path is a root drive ({configDirectory})";
                    return false;
                }

                // The directory should be exactly named "MechWarrior-Win64-Shipping"
                if (!dirInfo.Name.Equals(Constants.MechWarriorShippingName, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Cannot validate UEVR config directory: Directory must be named '{Constants.MechWarriorShippingName}' but found '{dirInfo.Name}'";
                    return false;
                }

                // If directory doesn't exist yet, it's safe (nothing to delete)
                if (!Directory.Exists(configDirectory))
                {
                    return true;
                }

                // Optionally check for config.txt to verify it's the right directory
                var configFilePath = Path.Combine(configDirectory, Constants.UevrConfigFile);
                if (!File.Exists(configFilePath))
                {
                    errorMessage = $"Cannot validate UEVR config directory: Expected {Constants.UevrConfigFile} not found at {configFilePath}. This may not be a valid UEVR config directory.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating UEVR config directory: {ex.Message}";
                return false;
            }
        }

        public string? PickFolder(string initialPath = null)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder";
            dialog.ShowNewFolderButton = true;

            // Set initial directory if provided and exists
            if (!string.IsNullOrEmpty(initialPath))
            {
                string expandedPath = ExpandEnvironmentVariables(initialPath);
                if (Directory.Exists(expandedPath))
                {
                    dialog.SelectedPath = expandedPath;
                }
                else
                {
                    // Try parent directory if the path doesn't exist
                    string parentPath = Path.GetDirectoryName(expandedPath);
                    if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                    {
                        dialog.SelectedPath = parentPath;
                    }
                }
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.SelectedPath;
            }

            return null;
        }

        public string? PickFile(string filter, string initialPath = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter
            };

            // Set initial directory if provided and exists
            if (!string.IsNullOrEmpty(initialPath))
            {
                string expandedPath = ExpandEnvironmentVariables(initialPath);
                if (File.Exists(expandedPath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(expandedPath);
                    dialog.FileName = Path.GetFileName(expandedPath);
                }
                else
                {
                    // Try using the directory if it exists
                    string directoryPath = Path.GetDirectoryName(expandedPath);
                    if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
                    {
                        dialog.InitialDirectory = directoryPath;
                    }
                }
            }

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        private void OnZipProgressChanged(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage(message);
            });
        }

        private void LogMessage(string message)
        {
            _loggingService.LogMessage(message);
        }

        public List<string> GetLogBuffer()
        {
            return _loggingService.GetLogBuffer();
        }

        public void AddToLogBuffer(string formattedMessage)
        {
            _loggingService.AddToLogBuffer(formattedMessage);
        }

        public LoggingService GetLoggingService()
        {
            return _loggingService;
        }

        public KeycodeService GetKeycodeService()
        {
            return _keycodeService;
        }

        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccessDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public string ExpandEnvironmentVariables(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return Environment.ExpandEnvironmentVariables(path);
        }


        private void ReadExistingUEVRConfig()
        {
            var configDirectory = ExpandEnvironmentVariables(_currentConfig.UEVRConfigDirectory);
            var keybindings = _uevrService.ReadExistingConfig(configDirectory);

            if (keybindings != null)
            {
                _showCursorKeyCode = keybindings.ShowCursorKeyCode;
                _menuKeyCode = keybindings.MenuKeyCode;
                _resetViewKeyCode = keybindings.ResetViewKeyCode;

                _currentConfig.ShowCursorKeyCode = keybindings.ShowCursorKeyCode;
                _currentConfig.MenuKeyCode = keybindings.MenuKeyCode;
                _currentConfig.ResetViewKeyCode = keybindings.ResetViewKeyCode;
            }
        }

        private void ModifyUEVRConfigFile(string configDirectory, int showCursorKeyCode, int menuKeyCode, int resetViewKeyCode)
        {
            _uevrService.ModifyConfigFile(configDirectory, showCursorKeyCode, menuKeyCode, resetViewKeyCode);
        }

        // Launch and Inject functionality
        private async Task<bool> StartSteamVRAsync()
        {
            return await _steamService.StartSteamVRAsync();
        }

        public async Task LaunchAndInjectAsync(SetupWindow? window)
        {
            // Helper methods to handle logging/status with or without a window
            void Log(string message)
            {
                if (window != null)
                    window.LogMessage(message);
                else
                    LogMessage(message);
            }

            void SetStatus(string status)
            {
                window?.SetStatusText(status);
            }

            void SetButtonsEnabled(bool enabled)
            {
                window?.SetInstallButtonsEnabled(enabled);
            }

            try
            {
                SetButtonsEnabled(false);

                // Start SteamVR if configured
                if (_currentConfig.StartSteamVR)
                {
                    SetStatus("Starting SteamVR...");
                    Log("Starting SteamVR...");

                    if (!await StartSteamVRAsync())
                    {
                        Log("Warning: Failed to start SteamVR, continuing anyway...");
                    }
                }

                SetStatus("Launching MechWarrior 5...");
                Log("Starting launch and inject process...");

                // Validate UEVR installation directory
                if (string.IsNullOrWhiteSpace(_currentConfig.UEVRInstallDirectory))
                {
                    ShowErrorDialog("Configuration Error", "UEVR Install Directory is not configured.");
                    Log("Error: UEVR Install Directory not set");
                    return;
                }

                var uevrInstallDir = ExpandEnvironmentVariables(_currentConfig.UEVRInstallDirectory);
                if (!Directory.Exists(uevrInstallDir))
                {
                    ShowErrorDialog("Configuration Error", $"UEVR Install Directory not found: {uevrInstallDir}");
                    Log($"Error: UEVR Install Directory not found: {uevrInstallDir}");
                    return;
                }

                // Validate MechWarrior directory
                if (string.IsNullOrWhiteSpace(_currentConfig.MechwarriorModsDirectory))
                {
                    ShowErrorDialog("Configuration Error", "MechWarrior Mods Directory is not configured.");
                    Log("Error: MechWarrior Mods Directory not set");
                    return;
                }

                var modsDirectory = ExpandEnvironmentVariables(_currentConfig.MechwarriorModsDirectory);
                if (!Directory.Exists(modsDirectory))
                {
                    ShowErrorDialog("Configuration Error", $"MechWarrior Mods Directory not found: {modsDirectory}");
                    Log($"Error: MechWarrior Mods Directory not found: {modsDirectory}");
                    return;
                }

                // Get the MechWarrior executable path (Mods directory is at MW5Mercs\Mods, exe is at MW5Mercs\Binaries\Win64)
                var mw5Directory = Directory.GetParent(modsDirectory)?.FullName;
                if (string.IsNullOrEmpty(mw5Directory))
                {
                    ShowErrorDialog("Configuration Error", "Could not determine MechWarrior 5 installation directory from Mods path.");
                    Log("Error: Could not determine MW5 installation directory");
                    return;
                }

                var mw5ExePath = Path.Combine(mw5Directory, "Binaries", "Win64", $"{Constants.Mw5ProcessName}.exe");
                if (!File.Exists(mw5ExePath))
                {
                    ShowErrorDialog("Configuration Error", $"MechWarrior 5 executable not found at: {mw5ExePath}");
                    Log($"Error: MW5 executable not found: {mw5ExePath}");
                    return;
                }

                // Verify UEVR DLLs exist
                var dllPaths = new List<string>();
                foreach (var dllName in Constants.UevrDllNames)
                {
                    var dllPath = Path.Combine(uevrInstallDir, dllName);
                    if (!File.Exists(dllPath))
                    {
                        ShowErrorDialog("Configuration Error", $"UEVR DLL not found: {dllPath}");
                        Log($"Error: UEVR DLL not found: {dllPath}");
                        return;
                    }
                    dllPaths.Add(dllPath);
                }

                Log($"Launching MechWarrior 5: {mw5ExePath}");

                // Launch MechWarrior 5
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mw5ExePath,
                    WorkingDirectory = Path.GetDirectoryName(mw5ExePath),
                    UseShellExecute = false
                };

                var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    ShowErrorDialog("Launch Error", "Failed to launch MechWarrior 5.");
                    Log("Error: Failed to start MW5 process");
                    return;
                }

                Log($"MechWarrior 5 launched (PID: {process.Id})");
                SetStatus("Waiting for process to initialize...");

                // Wait a moment for the process to initialize
                await Task.Delay(3000);

                // Inject DLLs
                Log("Starting DLL injection...");
                SetStatus("Injecting UEVR DLLs...");

                bool injectionSuccess = true;
                foreach (var dllPath in dllPaths)
                {
                    var dllName = Path.GetFileName(dllPath);
                    Log($"Injecting {dllName}...");

                    if (!InjectDll(process, dllPath))
                    {
                        Log($"Failed to inject {dllName}");
                        ShowErrorDialog("Injection Error", $"Failed to inject {dllName}");
                        injectionSuccess = false;
                        break;
                    }

                    Log($"Successfully injected {dllName}");
                    await Task.Delay(500); // Small delay between injections
                }

                if (injectionSuccess)
                {
                    Log("All DLLs injected successfully!");
                    SetStatus("Injection complete - MechWarrior 5 VR is ready");

                    // Check if auto-exit is enabled (from command line or config)
                    bool shouldAutoExit = App.AutoLaunchAndInject || _currentConfig.AutoExitAfterLaunch;

                    if (shouldAutoExit)
                    {
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    SetStatus("Injection failed");
                }
            }
            catch (Exception ex)
            {
                Log($"Error during launch and inject: {ex.Message}");
                ShowErrorDialog("Launch Error", $"An error occurred: {ex.Message}");
                SetStatus("Launch failed");
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private bool InjectDll(System.Diagnostics.Process targetProcess, string dllPath)
        {
            return _uevrService.InjectDll(targetProcess, dllPath);
        }
    }
}
