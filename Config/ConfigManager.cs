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

        public AppConfig CurrentConfig { get; private set; }

        public ConfigManager(string? fileName = "config.json")
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configFilePath = Path.Combine(baseDir, fileName ?? "config.json");
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
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_configFilePath, json);
            }
            catch
            {
                // Suppress file write issues
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
