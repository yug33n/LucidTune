using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LucidTune.Config;
using LucidTune.Services;

namespace LucidTune.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly ConfigManager _configManager;
        private bool _isInitializing = true;

        public SettingsWindow(MainWindow mainWindow, ConfigManager configManager)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _configManager = configManager;

            PopulateKeys();
            LoadCurrentSettings();
            _isInitializing = false;
        }

        private void PopulateKeys()
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                KeyComboBox.Items.Add(c.ToString());
            }

            for (int f = 1; f <= 12; f++)
            {
                KeyComboBox.Items.Add($"F{f}");
            }

            KeyComboBox.Items.Add("Space");
            KeyComboBox.Items.Add("Tab");
        }

        private void LoadCurrentSettings()
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
                KeyComboBox.SelectedIndex = 12; // 'M'
            }

            AutoStartCheckBox.IsChecked = AutoStartupService.IsAutoStartEnabled();
            AutoHideInGamesCheckBox.IsChecked = cfg.AutoHideInFullscreen;
            ShowSettingsOnStartupCheckBox.IsChecked = cfg.ShowSettingsOnStartup;

            UpdatePreview();
        }

        private void OnHotkeyInputsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            string modifier = (ModifierComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Alt";
            string key = KeyComboBox.SelectedItem?.ToString() ?? "M";
            HotkeyPreviewText.Text = $"{modifier} + {key}";
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnToggleOverlayClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.ToggleVisibility();
        }

        private void OnResetPositionClick(object sender, RoutedEventArgs e)
        {
            double defaultX = (SystemParameters.PrimaryScreenWidth - _mainWindow.Width) / 2;
            double defaultY = SystemParameters.PrimaryScreenHeight - _mainWindow.Height - 80;

            _mainWindow.Left = defaultX;
            _mainWindow.Top = defaultY;

            var cfg = _configManager.CurrentConfig;
            cfg.WindowX = defaultX;
            cfg.WindowY = defaultY;
            _configManager.Save(cfg);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string modifier = (ModifierComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Alt";
            string key = KeyComboBox.SelectedItem?.ToString() ?? "M";
            bool autoStart = AutoStartCheckBox.IsChecked == true;
            bool autoHide = AutoHideInGamesCheckBox.IsChecked == true;
            bool showStartup = ShowSettingsOnStartupCheckBox.IsChecked == true;

            var cfg = _configManager.CurrentConfig;
            cfg.HotkeyModifiers = modifier;
            cfg.HotkeyKey = key;
            cfg.AutoStart = autoStart;
            cfg.AutoHideInFullscreen = autoHide;
            cfg.ShowSettingsOnStartup = showStartup;
            _configManager.Save(cfg);

            AutoStartupService.SetAutoStart(autoStart);
            _mainWindow.ApplyNewHotkey(modifier, key);

            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
