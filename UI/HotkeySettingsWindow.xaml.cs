using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LucidTune.Config;

namespace LucidTune.UI
{
    public partial class HotkeySettingsWindow : Window
    {
        private readonly ConfigManager _configManager;

        public event Action<string, string>? HotkeyChanged;

        public HotkeySettingsWindow(ConfigManager configManager)
        {
            InitializeComponent();
            _configManager = configManager;

            PopulateKeys();
            LoadCurrentValues();
        }

        private void PopulateKeys()
        {
            // Populate common keys A-Z and F1-F12
            for (char c = 'A'; c <= 'Z'; c++)
            {
                KeyComboBox.Items.Add(c.ToString());
            }

            for (int f = 1; f <= 12; f++)
            {
                KeyComboBox.Items.Add($"F{f}");
            }

            KeyComboBox.Items.Add("Space");
        }

        private void LoadCurrentValues()
        {
            var cfg = _configManager.CurrentConfig;

            foreach (ComboBoxItem item in ModifierComboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), cfg.HotkeyModifiers, StringComparison.OrdinalIgnoreCase))
                {
                    ModifierComboBox.SelectedItem = item;
                    break;
                }
            }

            foreach (var item in KeyComboBox.Items)
            {
                if (string.Equals(item?.ToString(), cfg.HotkeyKey, StringComparison.OrdinalIgnoreCase))
                {
                    KeyComboBox.SelectedItem = item;
                    break;
                }
            }

            if (KeyComboBox.SelectedItem == null && KeyComboBox.Items.Count > 0)
            {
                KeyComboBox.SelectedIndex = 12; // Default to 'M'
            }

            AutoHideCheckBox.IsChecked = cfg.AutoHideInFullscreen;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string modifier = (ModifierComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Alt";
            string key = KeyComboBox.SelectedItem?.ToString() ?? "M";

            var cfg = _configManager.CurrentConfig;
            cfg.HotkeyModifiers = modifier;
            cfg.HotkeyKey = key;
            cfg.AutoHideInFullscreen = AutoHideCheckBox.IsChecked == true;
            _configManager.Save(cfg);

            HotkeyChanged?.Invoke(modifier, key);
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
