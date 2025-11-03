using MechwarriorVRLauncher.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MechwarriorVRLauncher.Services
{
    public class ConfigService
    {

        public async Task<LauncherConfig> LoadConfigAsync(string? configPath = null)
        {
            var path = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.DefaultConfigFileName);

            if (!File.Exists(path))
            {
                return new LauncherConfig();
            }

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var config = JsonSerializer.Deserialize<LauncherConfig>(json);
                return config ?? new LauncherConfig();
            }
            catch
            {
                return new LauncherConfig();
            }
        }

        public async Task SaveConfigAsync(LauncherConfig config, string? configPath = null)
        {
            var path = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.DefaultConfigFileName);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(path, json);
        }

        public string GetDefaultConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.DefaultConfigFileName);
        }
    }
}
