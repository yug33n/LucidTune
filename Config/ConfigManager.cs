using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace LucidTune.Config
{
    public class ConfigManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _configFilePath;
        private readonly string? _legacyConfigFilePath;

        public AppConfig CurrentConfig { get; private set; }

        public ConfigManager(string? fileName = "config.json")
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LucidTune");
            _configFilePath = Path.Combine(appDataDir, fileName ?? "config.json");

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _legacyConfigFilePath = Path.Combine(baseDir, fileName ?? "config.json");

            CurrentConfig = Load();
        }

        public AppConfig Load()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        TryCleanLegacyFile();
                        return config;
                    }
                }

                // Check legacy path (e.g. desktop or next to executable) if AppData file does not exist yet
                if (!string.IsNullOrEmpty(_legacyConfigFilePath) && File.Exists(_legacyConfigFilePath))
                {
                    var json = File.ReadAllText(_legacyConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        Save(config);
                        TryCleanLegacyFile();
                        return config;
                    }
                }

                var desktopConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "config.json");
                if (File.Exists(desktopConfig))
                {
                    var json = File.ReadAllText(desktopConfig);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        Save(config);
                        TryCleanLegacyFile();
                        return config;
                    }
                }
            }
            catch
            {
                // Fallback to default configuration on error
            }

            return new AppConfig();
        }

        public void Save(AppConfig config)
        {
            CurrentConfig = config;
            try
            {
                var dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_configFilePath, json);

                TryCleanLegacyFile();
            }
            catch
            {
                // Suppress file write issues
            }
        }

        private void TryCleanLegacyFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(_legacyConfigFilePath) &&
                    !string.Equals(_legacyConfigFilePath, _configFilePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(_legacyConfigFilePath))
                {
                    File.Delete(_legacyConfigFilePath);
                }

                var desktopConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "config.json");
                if (File.Exists(desktopConfig))
                {
                    File.Delete(desktopConfig);
                }
            }
            catch
            {
                // Suppress file delete issues
            }
        }

        public Point GetValidatedPosition(double defaultWidth, double defaultHeight)
        {
            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;

            double defaultX = (SystemParameters.PrimaryScreenWidth - defaultWidth) / 2;
            double defaultY = SystemParameters.PrimaryScreenHeight - defaultHeight - 80;

            if (!CurrentConfig.WindowX.HasValue || !CurrentConfig.WindowY.HasValue)
            {
                return new Point(defaultX, defaultY);
            }

            double x = CurrentConfig.WindowX.Value;
            double y = CurrentConfig.WindowY.Value;

            bool isWithinHorizontal = x >= screenLeft - 50 && x <= (screenLeft + screenWidth - 50);
            bool isWithinVertical = y >= screenTop - 50 && y <= (screenTop + screenHeight - 50);

            if (!isWithinHorizontal || !isWithinVertical)
            {
                return new Point(defaultX, defaultY);
            }

            return new Point(x, y);
        }
    }
}
