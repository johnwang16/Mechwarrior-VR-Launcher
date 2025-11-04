using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MechwarriorVRLauncher.Models
{
    public class ModStatus
    {
        [JsonPropertyName("bEnabled")]
        public bool Enabled { get; set; }
    }

    public class ModListConfig
    {
        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; } = string.Empty;

        [JsonPropertyName("modStatus")]
        public Dictionary<string, ModStatus> ModStatus { get; set; } = new Dictionary<string, ModStatus>();
    }
}
