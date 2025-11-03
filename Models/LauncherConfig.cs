using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MechwarriorVRLauncher.Models
{
    public class LauncherConfig
    {
        [JsonPropertyName("mechwarriorModsDirectory")]
        public string MechwarriorModsDirectory { get; set; } = string.Empty;

        [JsonPropertyName("uevrInstallDirectory")]
        public string UEVRInstallDirectory { get; set; } = string.Empty;

        [JsonPropertyName("uevrConfigDirectory")]
        public string UEVRConfigDirectory { get; set; } = string.Empty;

        [JsonPropertyName("uevrZipFile")]
        public string UEVRZipFile { get; set; } = string.Empty;

        [JsonPropertyName("uevrConfigFile")]
        public string UEVRConfigFile { get; set; } = string.Empty;

        [JsonPropertyName("showCursorKeyCode")]
        public int ShowCursorKeyCode { get; set; } = 36; // Default: Home key

        [JsonPropertyName("menuKeyCode")]
        public int MenuKeyCode { get; set; } = 45; // Default: Insert key

        [JsonPropertyName("resetViewKeyCode")]
        public int ResetViewKeyCode { get; set; } = 120; // Default: F9 key

        [JsonPropertyName("modZipFiles")]
        public List<string> ModZipFiles { get; set; } = new List<string>();

        [JsonPropertyName("startSteamVR")]
        public bool StartSteamVR { get; set; } = true; // Default: true

        [JsonPropertyName("autoExitAfterLaunch")]
        public bool AutoExitAfterLaunch { get; set; } = false; // Default: false

        [JsonPropertyName("installMods")]
        public bool InstallMods { get; set; } = true; // Default: true

        [JsonPropertyName("installUEVR")]
        public bool InstallUEVR { get; set; } = true; // Default: true

        [JsonPropertyName("installUEVRConfig")]
        public bool InstallUEVRConfig { get; set; } = true; // Default: true
    }
}
