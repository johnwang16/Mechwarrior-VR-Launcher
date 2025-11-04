using Microsoft.Win32;
using System;
using System.IO;

namespace MechwarriorVRLauncher.Services
{
    public class GOGService : GamePlatformService
    {
        public override string PlatformName => "GOG";

        public GOGService(LoggingService loggingService) : base(loggingService)
        {
        }

        protected override bool IsPlatformInstalled()
        {
            // Check if GOG Galaxy is installed by looking for the main GOG registry key
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(Constants.GogRegistryPath);
                if (key != null)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore errors
            }

            return false;
        }

        /// <summary>
        /// Gets the GOG game installation path from registry
        /// </summary>
        /// <returns>Path to the game installation, or null if not found</returns>
        protected override string? GetGameInstallPath()
        {
            _loggingService.LogMessage("Checking GOG registry for MechWarrior 5...");

            try
            {
                // Try to find MechWarrior 5 in GOG registry
                var gogGameKey = $@"{Constants.GogRegistryPath}\{Constants.GogMw5GameId}";

                using var key = Registry.LocalMachine.OpenSubKey(gogGameKey);
                if (key != null)
                {
                    var gamePath = key.GetValue(Constants.GogRegistryValuePath) as string;

                    if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
                    {
                        _loggingService.LogMessage($"  GOG registry entry found: {gamePath}");

                        // Verify it's actually MW5 by checking for the executable
                        var exePath = Path.Combine(gamePath, Constants.Mw5MercsFolder, Constants.Mw5BinariesFolder, Constants.Mw5Win64Folder, $"{Constants.Mw5ProcessName}.exe");
                        if (File.Exists(exePath))
                        {
                            _loggingService.LogMessage($"  Verified MechWarrior 5 executable exists");
                            _loggingService.LogMessage($"Found MechWarrior 5 in GOG registry at: {gamePath}");
                            return gamePath;
                        }
                        else
                        {
                            _loggingService.LogMessage($"  MechWarrior 5 executable not found at expected location");
                        }
                    }
                    else
                    {
                        _loggingService.LogMessage($"  GOG registry entry exists but path is invalid");
                    }
                }
                else
                {
                    _loggingService.LogMessage($"  No GOG registry entry found for MechWarrior 5");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"  Error reading GOG registry: {ex.Message}");
            }

            _loggingService.LogMessage("MechWarrior 5 not found in GOG registry");
            return null;
        }
    }
}
