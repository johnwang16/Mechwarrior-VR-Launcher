using System;
using System.IO;

namespace MechwarriorVRLauncher.Services
{
    /// <summary>
    /// Abstract base class for game platform services that provides common functionality
    /// </summary>
    public abstract class GamePlatformService : IGamePlatformService
    {
        protected readonly LoggingService _loggingService;

        protected GamePlatformService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        /// <summary>
        /// Gets the platform name (e.g., "Steam", "GOG")
        /// </summary>
        public abstract string PlatformName { get; }

        /// <summary>
        /// Checks if the platform launcher is installed.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <returns>True if the launcher is installed, false otherwise</returns>
        protected abstract bool IsPlatformInstalled();

        /// <summary>
        /// Public method to check if the platform launcher is installed.
        /// </summary>
        /// <returns>True if the launcher is installed, false otherwise</returns>
        public bool IsLauncherInstalled()
        {
            return IsPlatformInstalled();
        }

        /// <summary>
        /// Gets the game installation path for this platform.
        /// Must be implemented by derived classes.
        /// </summary>
        /// <returns>Path to the game installation, or null if not found</returns>
        protected abstract string? GetGameInstallPath();

        /// <summary>
        /// Detects the MechWarrior 5 Mods directory from the platform's installation.
        /// Assumes launcher is already installed (check IsLauncherInstalled() first).
        /// </summary>
        /// <returns>Path to the Mods directory, or null if not found</returns>
        public string? DetectMechWarriorModsDirectory()
        {
            var gamePath = GetGameInstallPath();
            if (string.IsNullOrEmpty(gamePath))
            {
                return null;
            }

            // Mods directory is typically in the game root
            var modsPath = Path.Combine(gamePath, Constants.Mw5ModsFolder);

            if (Directory.Exists(modsPath))
            {
                return modsPath;
            }

            // Also check the common alternative location
            var alternativePath = Path.Combine(gamePath, Constants.Mw5MercsFolder, Constants.Mw5ModsFolder);
            if (Directory.Exists(alternativePath))
            {
                return alternativePath;
            }

            // Game found but Mods folder doesn't exist
            return null;
        }
    }
}
