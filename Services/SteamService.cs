using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MechwarriorVRLauncher.Services
{
    public class SteamService
    {
        private readonly LoggingService _loggingService;

        public SteamService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public string? DetectMechWarriorModsDirectory()
        {
            // Try to find Steam installation
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            // Get all Steam library folders
            var libraryFolders = GetSteamLibraryFolders(steamPath);

            // Search for MechWarrior 5 in each library folder
            foreach (var libraryFolder in libraryFolders)
            {
                var mw5Path = FindMW5InLibrary(libraryFolder);
                if (!string.IsNullOrEmpty(mw5Path))
                {
                    // Mods directory is typically in the game root
                    var modsPath = Path.Combine(mw5Path, Constants.Mw5ModsFolder);

                    // Also check the common alternative location
                    if (!Directory.Exists(modsPath))
                    {
                        modsPath = Path.Combine(mw5Path, Constants.Mw5MercsFolder, Constants.Mw5ModsFolder);
                    }

                    if (Directory.Exists(modsPath))
                    {
                        return modsPath;
                    }

                    // Return the base path even if Mods folder doesn't exist yet
                    return Path.Combine(mw5Path, Constants.Mw5ModsFolder);
                }
            }

            return null;
        }

        public string? DetectSteamVRPath()
        {
            // Get Steam installation path
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            // Get all Steam library folders
            var libraryFolders = GetSteamLibraryFolders(steamPath);

            // Search for SteamVR in each library folder
            foreach (var libraryFolder in libraryFolders)
            {
                var steamVRPath = Path.Combine(libraryFolder, Constants.SteamCommonFolder, Constants.SteamVrFolder);
                if (Directory.Exists(steamVRPath))
                {
                    var vrMonitorPath = Path.Combine(steamVRPath, Constants.SteamVrBinPath, Constants.SteamVrMonitorExe);
                    if (File.Exists(vrMonitorPath))
                    {
                        return vrMonitorPath;
                    }
                }
            }

            return null;
        }

        private string? GetSteamInstallPath()
        {
            // Try to get Steam path from registry
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(Constants.SteamRegistryPathCurrentUser);
                if (key != null)
                {
                    var steamPath = key.GetValue(Constants.SteamRegistryValuePath) as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        return steamPath.Replace("/", "\\");
                    }
                }

                // Also try LocalMachine registry
                using var localKey = Registry.LocalMachine.OpenSubKey(Constants.SteamRegistryPathLocalMachine);
                if (localKey != null)
                {
                    var steamPath = localKey.GetValue(Constants.SteamRegistryValueInstallPath) as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        return steamPath;
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }

            // Try common default locations
            var defaultPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Constants.SteamFolderName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Constants.SteamFolderName)
            };

            foreach (var path in defaultPaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private List<string> GetSteamLibraryFolders(string steamPath)
        {
            var libraryFolders = new List<string>
            {
                Path.Combine(steamPath, Constants.SteamAppsFolder)
            };

            // Parse libraryfolders.vdf to find additional library locations
            var vdfPath = Path.Combine(steamPath, Constants.SteamAppsFolder, Constants.SteamLibraryFoldersFile);
            if (File.Exists(vdfPath))
            {
                try
                {
                    var content = File.ReadAllText(vdfPath);

                    // Match "path" entries in the VDF file
                    var pathMatches = Regex.Matches(content, Constants.SteamVdfPathPattern, RegexOptions.IgnoreCase);
                    foreach (Match match in pathMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            var steamAppsPath = Path.Combine(libraryPath, Constants.SteamAppsFolder);
                            if (Directory.Exists(steamAppsPath) && !libraryFolders.Contains(steamAppsPath))
                            {
                                libraryFolders.Add(steamAppsPath);
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails, just use the default library
                }
            }

            return libraryFolders;
        }

        private string? FindMW5InLibrary(string steamAppsPath)
        {
            // Check by App ID in appmanifest file - this is the authoritative method
            var manifestPath = Path.Combine(steamAppsPath, $"{Constants.SteamAppManifestPrefix}{Constants.Mw5AppId}{Constants.SteamAppManifestSuffix}");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var content = File.ReadAllText(manifestPath);
                    var match = Regex.Match(content, Constants.SteamVdfInstalldirPattern);
                    if (match.Success)
                    {
                        var installDir = match.Groups[1].Value;
                        var gamePath = Path.Combine(steamAppsPath, Constants.SteamCommonFolder, installDir);
                        if (Directory.Exists(gamePath))
                        {
                            return gamePath;
                        }
                    }
                }
                catch
                {
                    // If we can't read the manifest, fail detection
                }
            }

            return null;
        }

        public async Task<bool> StartSteamVRAsync()
        {
            try
            {
                // Try to detect SteamVR executable
                string? steamVrPath = DetectSteamVRPath();

                if (string.IsNullOrEmpty(steamVrPath))
                {
                    _loggingService.LogMessage("SteamVR executable not found via Steam detector");

                    // Try using Steam URI as fallback
                    _loggingService.LogMessage("Attempting to start SteamVR via Steam URI...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Constants.SteamVrUri,
                        UseShellExecute = true
                    });

                    await Task.Delay(2000); // Give Steam a moment to process
                    return true;
                }

                _loggingService.LogMessage($"Starting SteamVR from: {steamVrPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = steamVrPath,
                    WorkingDirectory = Path.GetDirectoryName(steamVrPath),
                    UseShellExecute = false
                };

                Process.Start(startInfo);
                _loggingService.LogMessage("SteamVR started successfully");

                // Wait for SteamVR to initialize
                await Task.Delay(5000);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"Error starting SteamVR: {ex.Message}");
                return false;
            }
        }
    }
}
