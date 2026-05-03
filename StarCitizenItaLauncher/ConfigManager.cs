using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace StarCitizenItaLauncher
{
    public class AppConfig
    {
        public string BaseGameFolder { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, string> LastUpdates { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        public string SelectedChannel { get; set; } = "LIVE";
        public bool StartWithWindows { get; set; } = false;
        public bool RunInBackground { get; set; } = false;
        public bool AllowCustomFile { get; set; } = false;
        public System.Collections.Generic.List<string> ChannelsWithCustom { get; set; } = new System.Collections.Generic.List<string>();
    }

    public static class ConfigManager
    {
        private static readonly string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MrRevoLauncher", "config.json");

        public static AppConfig LeggiConfig()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
            return new AppConfig();
        }

        public static void SalvaConfig(AppConfig config)
        {
            string? cartellaDestinazione = Path.GetDirectoryName(_configPath);
            if (cartellaDestinazione != null)
            {
                Directory.CreateDirectory(cartellaDestinazione);
            }

            string json = JsonSerializer.Serialize(config);
            File.WriteAllText(_configPath, json);
        }

        public static void ImpostaAvvioAutomatico(bool abilita, bool inBackground)
        {
            string appName = "MrRevoSCUpdater";
            string exePath = Environment.ProcessPath ?? "";
            string command = inBackground ? $"\"{exePath}\" -silent" : $"\"{exePath}\"";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!)
            {
                if (abilita)
                    key.SetValue(appName, command);
                else
                    key.DeleteValue(appName, false);
            }
        }
    }
}