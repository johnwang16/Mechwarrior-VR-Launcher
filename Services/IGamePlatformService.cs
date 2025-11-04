namespace MechwarriorVRLauncher.Services
{
    /// <summary>
    /// Interface for game platform services (Steam, GOG, Epic, etc.)
    /// </summary>
    public interface IGamePlatformService
    {
        /// <summary>
        /// Detects the MechWarrior 5 Mods directory from the platform's installation
        /// </summary>
        /// <returns>Path to the Mods directory, or null if not found</returns>
        string? DetectMechWarriorModsDirectory();

        /// <summary>
        /// Gets the platform name (e.g., "Steam", "GOG")
        /// </summary>
        string PlatformName { get; }

        /// <summary>
        /// Checks if the platform launcher is installed
        /// </summary>
        /// <returns>True if the launcher is installed, false otherwise</returns>
        bool IsLauncherInstalled();
    }
}
