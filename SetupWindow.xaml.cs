using MechwarriorVRLauncher.Services;

namespace MechwarriorVRLauncher
{
    public partial class SetupWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly ZipExtractionService _zipService;
        private readonly KeycodeService _keycodeService;

        // Key codes
        private int _showCursorKeyCode = 36; // Home
        private int _menuKeyCode = 45; // Insert
        private int _resetViewKeyCode = 120; // F9

        public SetupWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _zipService = new ZipExtractionService();
            _zipService.ProgressChanged += OnZipProgressChanged;
            _keycodeService = mainWindow.GetKeycodeService();

            // Set window title with version
            this.Title = $"Setup - MechWarrior VR Launcher v{MainWindow.GetVersion()}";

            // Set hyperlink URLs from constants
            ModZipNexusLink.NavigateUri = new Uri(Constants.MechWarriorVrNexusModsUrl);
            UevrGitHubLink.NavigateUri = new Uri(Constants.UevrGitHubReleasesUrl);
            UevrConfigNexusLink.NavigateUri = new Uri(Constants.MechWarriorVrNexusModsUrl);

            // Load current configuration
            LoadUIFromConfig();

            // Wire up auto-save events
            WireUpAutoSaveEvents();

            // Check if SteamVR is available
            CheckSteamVRAvailability();

            // Display any buffered log messages
            DisplayBufferedLogs();

            // Scan installed mods when window opens
            _mainWindow.ScanInstalledMods(this);
        }

        private void DisplayBufferedLogs()
        {
            var bufferedLogs = _mainWindow.GetLogBuffer();
            foreach (var log in bufferedLogs)
            {
                LogTextBox.Text += log + "\n";
            }
        }

        private void CheckSteamVRAvailability()
        {
            var loggingService = _mainWindow.GetLoggingService();
            var steamService = new Services.SteamService(loggingService);
            var steamVRPath = steamService.DetectSteamVRPath();

            if (string.IsNullOrEmpty(steamVRPath))
            {
                StartSteamVRCheckBox.IsEnabled = false;
                StartSteamVRCheckBox.IsChecked = false;
                StartSteamVRCheckBox.ToolTip = "Steam installation not found";
                LogMessage("SteamVR not detected - Start SteamVR option disabled");
            }
            else
            {
                StartSteamVRCheckBox.IsEnabled = true;
                StartSteamVRCheckBox.ToolTip = $"Start SteamVR before launching the game\nDetected at: {steamVRPath}";
                LogMessage($"SteamVR detected at: {steamVRPath}");
            }
        }

        private void WireUpAutoSaveEvents()
        {
            // TextBox changes
            ModsDirectoryTextBox.TextChanged += OnConfigChanged;
            UEVRZipFileTextBox.TextChanged += OnConfigChanged;
            UEVRInstallDirectoryTextBox.TextChanged += OnConfigChanged;
            UEVRConfigZipFileTextBox.TextChanged += OnConfigChanged;
            UEVRConfigDirectoryTextBox.TextChanged += OnConfigChanged;

            // CheckBox changes
            InstallModsCheckBox.Checked += OnConfigChanged;
            InstallModsCheckBox.Unchecked += OnConfigChanged;
            InstallUEVRCheckBox.Checked += OnConfigChanged;
            InstallUEVRCheckBox.Unchecked += OnConfigChanged;
            InstallUEVRConfigCheckBox.Checked += OnConfigChanged;
            InstallUEVRConfigCheckBox.Unchecked += OnConfigChanged;
            StartSteamVRCheckBox.Checked += OnConfigChanged;
            StartSteamVRCheckBox.Unchecked += OnConfigChanged;
            AutoExitCheckBox.Checked += OnConfigChanged;
            AutoExitCheckBox.Unchecked += OnConfigChanged;
            CleanInstallCheckBox.Checked += OnConfigChanged;
            CleanInstallCheckBox.Unchecked += OnConfigChanged;
        }

        private async void OnConfigChanged(object sender, RoutedEventArgs e)
        {
            // Auto-save the configuration
            await AutoSaveConfigAsync();
        }

        private async Task AutoSaveConfigAsync()
        {
            try
            {
                UpdateConfig();
                await _mainWindow.SaveConfigAsync();
            }
            catch
            {
                // Silently fail - don't interrupt user with save errors during UI interaction
            }
        }

        public void LoadUIFromConfig()
        {
            var config = _mainWindow.GetCurrentConfig();

            // Populate UI fields
            ModsDirectoryTextBox.Text = config.MechwarriorModsDirectory;
            UEVRZipFileTextBox.Text = config.UEVRZipFile;
            UEVRInstallDirectoryTextBox.Text = config.UEVRInstallDirectory;
            UEVRConfigZipFileTextBox.Text = config.UEVRConfigFile;
            UEVRConfigDirectoryTextBox.Text = config.UEVRConfigDirectory;

            // Load mod zip files
            ModZipFilesListBox.Items.Clear();
            foreach (var modZip in config.ModZipFiles)
            {
                ModZipFilesListBox.Items.Add(modZip);
            }

            // Load key bindings
            _showCursorKeyCode = config.ShowCursorKeyCode;
            _menuKeyCode = config.MenuKeyCode;
            _resetViewKeyCode = config.ResetViewKeyCode;

            ShowCursorKeyTextBox.Text = _keycodeService.GetKeyName(_showCursorKeyCode);
            MenuKeyTextBox.Text = _keycodeService.GetKeyName(_menuKeyCode);
            ResetViewKeyTextBox.Text = _keycodeService.GetKeyName(_resetViewKeyCode);

            // Load installation component selection
            InstallModsCheckBox.IsChecked = config.InstallMods;
            InstallUEVRCheckBox.IsChecked = config.InstallUEVR;
            InstallUEVRConfigCheckBox.IsChecked = config.InstallUEVRConfig;

            // Load launcher options
            StartSteamVRCheckBox.IsChecked = config.StartSteamVR;
            AutoExitCheckBox.IsChecked = config.AutoExitAfterLaunch;
        }

        public void UpdateConfig()
        {
            var config = _mainWindow.GetCurrentConfig();

            // Update config from UI
            config.MechwarriorModsDirectory = ModsDirectoryTextBox.Text;
            config.UEVRZipFile = UEVRZipFileTextBox.Text;
            config.UEVRInstallDirectory = UEVRInstallDirectoryTextBox.Text;
            config.UEVRConfigFile = UEVRConfigZipFileTextBox.Text;
            config.UEVRConfigDirectory = UEVRConfigDirectoryTextBox.Text;
            config.ShowCursorKeyCode = _showCursorKeyCode;
            config.MenuKeyCode = _menuKeyCode;
            config.ResetViewKeyCode = _resetViewKeyCode;

            // Update mod zip files
            config.ModZipFiles.Clear();
            foreach (var item in ModZipFilesListBox.Items)
            {
                config.ModZipFiles.Add(item.ToString());
            }

            // Update installation component selection
            config.InstallMods = InstallModsCheckBox.IsChecked == true;
            config.InstallUEVR = InstallUEVRCheckBox.IsChecked == true;
            config.InstallUEVRConfig = InstallUEVRConfigCheckBox.IsChecked == true;

            // Update launcher options
            config.StartSteamVR = StartSteamVRCheckBox.IsChecked == true;
            config.AutoExitAfterLaunch = AutoExitCheckBox.IsChecked == true;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfig(); // Save UI state to config before validating and installing

            // Check if at least one component is selected
            bool installMods = InstallModsCheckBox.IsChecked == true;
            bool installUEVR = InstallUEVRCheckBox.IsChecked == true;
            bool installUEVRConfig = InstallUEVRConfigCheckBox.IsChecked == true;

            if (!installMods && !installUEVR && !installUEVRConfig)
            {
                ShowErrorDialog("No Components Selected", "Please select at least one component to install.");
                return;
            }

            if (!_mainWindow.ValidateConfiguration())
            {
                ShowErrorDialog("Validation Error", "Please fill in all required fields and ensure all zip files exist.");
                return;
            }

            // If clean install is enabled, show a combined confirmation dialog
            if (GetCleanInstallEnabled())
            {
                var deletionInfo = _mainWindow.GetCleanInstallDeletionInfo(installMods, installUEVR, installUEVRConfig);

                if (deletionInfo.Count > 0)
                {
                    var confirmDialog = new ConfirmationDialog(deletionInfo);
                    confirmDialog.ShowDialog();

                    if (!confirmDialog.UserConfirmed)
                    {
                        LogMessage("Installation cancelled by user");
                        StatusTextBlock.Text = "Installation cancelled";
                        return;
                    }

                    LogMessage("User confirmed clean install");
                }
            }

            try
            {
                SetInstallButtonsEnabled(false);
                InstallProgressBar.IsIndeterminate = true;
                StatusTextBlock.Text = "Installing...";
                LogMessage("Starting installation...");

                // Install selected components and track results
                bool modsSuccess = true;
                bool uevrSuccess = true;
                bool configSuccess = true;

                if (installMods)
                {
                    modsSuccess = await _mainWindow.InstallModsAsync(this);
                }

                if (installUEVR)
                {
                    uevrSuccess = await _mainWindow.InstallUEVRAsync(this);
                }

                if (installUEVRConfig)
                {
                    configSuccess = await _mainWindow.InstallUEVRConfigAsync(this);
                }

                // Report results
                bool allSuccessful = modsSuccess && uevrSuccess && configSuccess;

                if (allSuccessful)
                {
                    LogMessage("All installations completed successfully!");
                    StatusTextBlock.Text = "Installation completed";

                    // Check for mod validation issues if mods were installed
                    var validationSummary = _mainWindow.GetModValidationSummary();
                    string message = "All selected components have been installed successfully.";

                    if (installMods && validationSummary.HasIssues)
                    {
                        var issues = new List<string>();

                        if (validationSummary.MissingRequiredMods > 0)
                            issues.Add($"{validationSummary.MissingRequiredMods} required mod(s) missing");
                        if (validationSummary.VersionMismatches > 0)
                            issues.Add($"{validationSummary.VersionMismatches} version mismatch(es)");
                        if (validationSummary.BlacklistedMods > 0)
                            issues.Add($"{validationSummary.BlacklistedMods} blacklisted mod(s)");
                        if (validationSummary.LoadOrderViolations > 0)
                            issues.Add($"{validationSummary.LoadOrderViolations} load order violation(s)");

                        message += "\n\nMod validation issues detected:\n• " + string.Join("\n• ", issues);
                        message += "\n\nRefer to the Log tab for details.";

                        if (validationSummary.HasErrors)
                        {
                            ShowErrorDialog("Installation Complete with Errors", message);
                        }
                        else
                        {
                            ShowWarningDialog("Installation Complete with Warnings", message);
                        }
                    }
                    else
                    {
                        ShowSuccessDialog("Installation Complete", message);
                    }
                }
                else
                {
                    var failedComponents = new List<string>();
                    if (!modsSuccess && installMods) failedComponents.Add(Constants.InstallComponentMods);
                    if (!uevrSuccess && installUEVR) failedComponents.Add(Constants.InstallComponentUevr);
                    if (!configSuccess && installUEVRConfig) failedComponents.Add(Constants.InstallComponentUevrConfig);

                    string failedList = string.Join(", ", failedComponents);
                    LogMessage($"Installation completed with errors: {failedList} failed");
                    StatusTextBlock.Text = "Installation completed with errors";
                    ShowErrorDialog("Partial Installation Failure", $"Some components failed to install:\n\n{failedList}\n\nCheck the log for details.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during installation: {ex.Message}");
                ShowErrorDialog("Installation Error", $"An error occurred during installation: {ex.Message}");
            }
            finally
            {
                SetInstallButtonsEnabled(true);
                InstallProgressBar.IsIndeterminate = false;
                StatusTextBlock.Text = "Ready";
            }
        }

        public void SetInstallButtonsEnabled(bool enabled)
        {
            InstallButton.IsEnabled = enabled;
        }

        public void SetStatusText(string text)
        {
            StatusTextBlock.Text = text;
        }

        public void SetProgressIndeterminate(bool isIndeterminate)
        {
            InstallProgressBar.IsIndeterminate = isIndeterminate;
        }

        public bool GetCleanInstallEnabled()
        {
            return CleanInstallCheckBox.IsChecked == true;
        }

        private void OnZipProgressChanged(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage(message);
            });
        }

        public void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] {message}";
            LogTextBox.Text += formattedMessage + "\n";

            // Also store in MainWindow buffer so logs persist across window open/close
            _mainWindow.AddToLogBuffer(formattedMessage);
        }

        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccessDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowWarningDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Browse and Auto-Detect Event Handlers
        private void AutoDetectModsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfig(); // Save current UI state to config
            _mainWindow.AutoDetectModsDirectory(this);
            LoadUIFromConfig(); // Reload UI from updated config
        }

        private void BrowseModsButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = _mainWindow.PickFolder(ModsDirectoryTextBox.Text);
            if (folder != null)
            {
                ModsDirectoryTextBox.Text = folder;
                var config = _mainWindow.GetCurrentConfig();
                config.MechwarriorModsDirectory = folder;
                _mainWindow.ScanInstalledMods(this);
            }
        }

        private async void AddModZipButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = Constants.ZipFileFilter,
                Title = "Select MechWarrior Mod Zip File",
                Multiselect = true
            };

            // Try to start in a sensible location
            string lastModZip = ModZipFilesListBox.Items.Count > 0
                ? ModZipFilesListBox.Items[ModZipFilesListBox.Items.Count - 1].ToString()
                : null;

            if (!string.IsNullOrEmpty(lastModZip) && File.Exists(lastModZip))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(lastModZip);
            }

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    // Avoid duplicates
                    if (!ModZipFilesListBox.Items.Contains(file))
                    {
                        ModZipFilesListBox.Items.Add(file);
                        LogMessage($"Added mod zip: {file}");
                    }
                }

                // Auto-save configuration
                await AutoSaveConfigAsync();
            }
        }

        private async void RemoveModZipButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModZipFilesListBox.SelectedItem != null)
            {
                string selectedFile = ModZipFilesListBox.SelectedItem.ToString();
                ModZipFilesListBox.Items.Remove(ModZipFilesListBox.SelectedItem);
                LogMessage($"Removed mod zip: {selectedFile}");

                // Auto-save configuration
                await AutoSaveConfigAsync();
            }
            else
            {
                LogMessage("No mod zip selected to remove");
            }
        }

        private void BrowseUEVRZipButton_Click(object sender, RoutedEventArgs e)
        {
            var file = _mainWindow.PickFile(Constants.ZipFileFilter, UEVRZipFileTextBox.Text);
            if (file != null)
            {
                UEVRZipFileTextBox.Text = file;
            }
        }

        private void BrowseUEVRInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = _mainWindow.PickFolder(UEVRInstallDirectoryTextBox.Text);
            if (folder != null)
            {
                UEVRInstallDirectoryTextBox.Text = folder;
            }
        }

        private void BrowseUEVRConfigZipButton_Click(object sender, RoutedEventArgs e)
        {
            var file = _mainWindow.PickFile(Constants.MechWarriorConfigFilter, UEVRConfigZipFileTextBox.Text);
            if (file != null)
            {
                UEVRConfigZipFileTextBox.Text = file;
            }
        }

        private void BrowseUEVRConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = _mainWindow.PickFolder(UEVRConfigDirectoryTextBox.Text);
            if (folder != null)
            {
                UEVRConfigDirectoryTextBox.Text = folder;
            }
        }

        // Key Binding Event Handlers
        private void KeyBindingTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // Use system highlight color for better dark mode support
                textBox.Background = SystemColors.HighlightBrush;
                textBox.Foreground = SystemColors.HighlightTextBrush;
                textBox.SelectAll();
            }
        }

        private void KeyBindingTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Prevent text input
            e.Handled = true;
        }

        private async Task HandleKeyBindingInput(System.Windows.Controls.TextBox textBox, KeyEventArgs e, string keyDescription, Action<int> setKeyCode)
        {
            e.Handled = true;
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            int keyCode = _keycodeService.ConvertWpfKeyToVirtualKeyCode(actualKey);

            setKeyCode(keyCode);
            string keyName = _keycodeService.GetKeyName(actualKey, keyCode);
            textBox.Text = keyName;
            textBox.ClearValue(BackgroundProperty);
            textBox.ClearValue(ForegroundProperty);
            LogMessage($"{keyDescription} set to: {keyName} (keycode {keyCode})");
            Keyboard.ClearFocus();

            // Auto-save configuration
            await AutoSaveConfigAsync();
        }

        private async void ShowCursorKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            await HandleKeyBindingInput(ShowCursorKeyTextBox, e, "Show Cursor Key", code => _showCursorKeyCode = code);
        }

        private async void MenuKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            await HandleKeyBindingInput(MenuKeyTextBox, e, "Menu Key", code => _menuKeyCode = code);
        }

        private async void ResetViewKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            await HandleKeyBindingInput(ResetViewKeyTextBox, e, "Reset View Key", code => _resetViewKeyCode = code);
        }

        // Hyperlink Event Handler
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error opening URL: {ex.Message}");
            }
        }
    }
}
