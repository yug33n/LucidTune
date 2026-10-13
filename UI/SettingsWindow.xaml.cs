using System;
using System.Collections.Generic;
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

        private static readonly string[] ModifiersList = ["Alt", "Ctrl", "Shift", "Ctrl+Alt", "Win"];
        private static readonly List<string> CommonKeys = [];

        static SettingsWindow()
        {
            CommonKeys.AddRange(["Space", "Right", "Left", "Up", "Down", "M", "P", "L", "J", "Tab", "Enter"]);
            for (char c = 'A'; c <= 'Z'; c++)
            {
                string s = c.ToString();
                if (!CommonKeys.Contains(s)) CommonKeys.Add(s);
            }
            for (int f = 1; f <= 12; f++)
            {
                CommonKeys.Add($"F{f}");
            }
        }

        public SettingsWindow(MainWindow mainWindow, ConfigManager configManager)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _configManager = configManager;

            PopulateAllBoxes();
            LoadCurrentSettings();
            _isInitializing = false;
        }

        private void PopulateAllBoxes()
        {
            SetupComboBox(ToggleModifierBox, ToggleKeyBox);
            SetupComboBox(PlayPauseModifierBox, PlayPauseKeyBox);
            SetupComboBox(NextModifierBox, NextKeyBox);
            SetupComboBox(PrevModifierBox, PrevKeyBox);
        }

        private static void SetupComboBox(ComboBox modifierBox, ComboBox keyBox)
        {
            modifierBox.Items.Clear();
            foreach (var mod in ModifiersList)
            {
                modifierBox.Items.Add(mod);
            }

            keyBox.Items.Clear();
            foreach (var k in CommonKeys)
            {
                keyBox.Items.Add(k);
            }
        }

        private void LoadCurrentSettings()
        {
            var cfg = _configManager.CurrentConfig;

            SelectBoxItem(ToggleModifierBox, cfg.HotkeyModifiers, "Alt");
            SelectBoxItem(ToggleKeyBox, cfg.HotkeyKey, "M");

            SelectBoxItem(PlayPauseModifierBox, cfg.PlayPauseModifiers, "Alt");
            SelectBoxItem(PlayPauseKeyBox, cfg.PlayPauseKey, "Space");

            SelectBoxItem(NextModifierBox, cfg.NextTrackModifiers, "Alt");
            SelectBoxItem(NextKeyBox, cfg.NextTrackKey, "Right");

            SelectBoxItem(PrevModifierBox, cfg.PrevTrackModifiers, "Alt");
            SelectBoxItem(PrevKeyBox, cfg.PrevTrackKey, "Left");

            EnableMediaHotkeysCheckBox.IsChecked = cfg.MediaHotkeysEnabled;
            MediaHotkeysPanel.IsEnabled = cfg.MediaHotkeysEnabled;
            MediaHotkeysPanel.Opacity = cfg.MediaHotkeysEnabled ? 1.0 : 0.45;

            AutoStartCheckBox.IsChecked = AutoStartupService.IsAutoStartEnabled();
            AutoHideInGamesCheckBox.IsChecked = cfg.AutoHideInFullscreen;
            ShowSettingsOnStartupCheckBox.IsChecked = cfg.ShowSettingsOnStartup;

            UpdateAllPreviews();
        }

        private static void SelectBoxItem(ComboBox box, string value, string defaultValue)
        {
            foreach (var item in box.Items)
            {
                if (string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = item;
                    return;
                }
            }

            foreach (var item in box.Items)
            {
                if (string.Equals(item?.ToString(), defaultValue, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = item;
                    return;
                }
            }

            if (box.Items.Count > 0) box.SelectedIndex = 0;
        }

        private void OnMediaHotkeysToggle(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isEnabled = EnableMediaHotkeysCheckBox.IsChecked == true;
            MediaHotkeysPanel.IsEnabled = isEnabled;
            MediaHotkeysPanel.Opacity = isEnabled ? 1.0 : 0.45;
        }

        private void OnHotkeyInputsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateAllPreviews();
        }

        private void UpdateAllPreviews()
        {
            PreviewToggleText.Text = $"{ToggleModifierBox.SelectedItem} + {ToggleKeyBox.SelectedItem}";
            PreviewPlayPauseText.Text = $"{PlayPauseModifierBox.SelectedItem} + {PlayPauseKeyBox.SelectedItem}";
            PreviewNextText.Text = $"{NextModifierBox.SelectedItem} + {NextKeyBox.SelectedItem}";
            PreviewPrevText.Text = $"{PrevModifierBox.SelectedItem} + {PrevKeyBox.SelectedItem}";
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
            var cfg = _configManager.CurrentConfig;

            cfg.HotkeyModifiers = ToggleModifierBox.SelectedItem?.ToString() ?? "Alt";
            cfg.HotkeyKey = ToggleKeyBox.SelectedItem?.ToString() ?? "M";

            cfg.MediaHotkeysEnabled = EnableMediaHotkeysCheckBox.IsChecked == true;
            cfg.PlayPauseModifiers = PlayPauseModifierBox.SelectedItem?.ToString() ?? "Alt";
            cfg.PlayPauseKey = PlayPauseKeyBox.SelectedItem?.ToString() ?? "Space";
            cfg.NextTrackModifiers = NextModifierBox.SelectedItem?.ToString() ?? "Alt";
            cfg.NextTrackKey = NextKeyBox.SelectedItem?.ToString() ?? "Right";
            cfg.PrevTrackModifiers = PrevModifierBox.SelectedItem?.ToString() ?? "Alt";
            cfg.PrevTrackKey = PrevKeyBox.SelectedItem?.ToString() ?? "Left";

            bool autoStart = AutoStartCheckBox.IsChecked == true;
            cfg.AutoStart = autoStart;
            cfg.AutoHideInFullscreen = AutoHideInGamesCheckBox.IsChecked == true;
            cfg.ShowSettingsOnStartup = ShowSettingsOnStartupCheckBox.IsChecked == true;

            _configManager.Save(cfg);

            AutoStartupService.SetAutoStart(autoStart);
            _mainWindow.RegisterAllConfiguredHotkeys();

            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
