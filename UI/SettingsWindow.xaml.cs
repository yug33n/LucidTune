using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LucidTune.Config;
using LucidTune.Services;

namespace LucidTune.UI
{
    public static class ScrollViewerExtensions
    {
        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedVerticalOffset",
                typeof(double),
                typeof(ScrollViewerExtensions),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public static double GetAnimatedVerticalOffset(DependencyObject target) =>
            (double)target.GetValue(AnimatedVerticalOffsetProperty);

        public static void SetAnimatedVerticalOffset(DependencyObject target, double value) =>
            target.SetValue(AnimatedVerticalOffsetProperty, value);

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }

    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly ConfigManager _configManager;
        private bool _isInitializing = true;
        private double _targetVerticalOffset;

        private static readonly string[] ModifiersList = ["Alt", "Ctrl", "Shift", "Ctrl+Alt", "Win", "None"];
        private static readonly List<string> CommonKeys = ["None", "Space", "Right", "Left", "Up", "Down", "M", "P", "L", "J", "Tab", "Enter"];

        static SettingsWindow()
        {
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

            _mainWindow.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
            Closed += (s, e) =>
            {
                _mainWindow.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
                _mainWindow.ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
                _mainWindow.ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
            };
            UpdateOverlayStatus(_mainWindow.IsOverlayVisible);
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

            int opacityPercent = (int)Math.Round(cfg.WidgetOpacity * 100);
            if (opacityPercent < 20) opacityPercent = 20;
            if (opacityPercent > 100) opacityPercent = 100;
            OpacitySlider.Value = opacityPercent;
            OpacityValueText.Text = $"{opacityPercent}%";

            CompactModeCheckBox.IsChecked = cfg.CompactMode;

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

        private void OnContentScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ContentScrollViewer == null) return;
            e.Handled = true;

            // Dampened smooth scrolling step (36px per notch)
            double deltaLines = (double)e.Delta / Mouse.MouseWheelDeltaForOneLine;
            double scrollStep = -deltaLines * 36.0;

            double currentTarget = _targetVerticalOffset;
            if (Math.Abs(ContentScrollViewer.VerticalOffset - currentTarget) > 120)
            {
                currentTarget = ContentScrollViewer.VerticalOffset;
            }

            _targetVerticalOffset = Math.Clamp(currentTarget + scrollStep, 0, ContentScrollViewer.ScrollableHeight);

            var animation = new DoubleAnimation
            {
                To = _targetVerticalOffset,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ContentScrollViewer.BeginAnimation(ScrollViewerExtensions.AnimatedVerticalOffsetProperty, animation);
        }

        private void OnContentScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ContentScrollViewer != null && e.VerticalChange != 0 && Math.Abs(ContentScrollViewer.VerticalOffset - _targetVerticalOffset) > 120)
            {
                _targetVerticalOffset = ContentScrollViewer.VerticalOffset;
            }
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

        private void OnClearToggleClick(object sender, RoutedEventArgs e)
        {
            SelectBoxItem(ToggleModifierBox, "None", "None");
            SelectBoxItem(ToggleKeyBox, "None", "None");
            UpdateAllPreviews();
        }

        private void OnClearPlayPauseClick(object sender, RoutedEventArgs e)
        {
            SelectBoxItem(PlayPauseModifierBox, "None", "None");
            SelectBoxItem(PlayPauseKeyBox, "None", "None");
            UpdateAllPreviews();
        }

        private void OnClearNextClick(object sender, RoutedEventArgs e)
        {
            SelectBoxItem(NextModifierBox, "None", "None");
            SelectBoxItem(NextKeyBox, "None", "None");
            UpdateAllPreviews();
        }

        private void OnClearPrevClick(object sender, RoutedEventArgs e)
        {
            SelectBoxItem(PrevModifierBox, "None", "None");
            SelectBoxItem(PrevKeyBox, "None", "None");
            UpdateAllPreviews();
        }

        private void UpdateAllPreviews()
        {
            UpdatePreview(PreviewToggleText, BadgeToggle, ToggleModifierBox, ToggleKeyBox);
            UpdatePreview(PreviewPlayPauseText, BadgePlayPause, PlayPauseModifierBox, PlayPauseKeyBox);
            UpdatePreview(PreviewNextText, BadgeNext, NextModifierBox, NextKeyBox);
            UpdatePreview(PreviewPrevText, BadgePrev, PrevModifierBox, PrevKeyBox);
        }

        private static void UpdatePreview(TextBlock? textBlock, Border? badgeBorder, ComboBox? modBox, ComboBox? keyBox)
        {
            if (textBlock == null || badgeBorder == null) return;

            string mod = modBox?.SelectedItem?.ToString() ?? "None";
            string key = keyBox?.SelectedItem?.ToString() ?? "None";

            if (string.IsNullOrWhiteSpace(key) || key.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Text = "Не назначено";
                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98));
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2C));
            }
            else if (string.IsNullOrWhiteSpace(mod) || mod.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Text = key;
                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0x75, 0xFF));
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x38));
            }
            else
            {
                textBlock.Text = $"{mod} + {key}";
                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0x75, 0xFF));
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x38));
            }
        }

        private void OnCompactModeChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isCompact = CompactModeCheckBox.IsChecked == true;
            _mainWindow.ApplyCompactMode(isCompact);
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnOverlayVisibilityChanged(bool isVisible)
        {
            Dispatcher.Invoke(() => UpdateOverlayStatus(isVisible));
        }

        private void UpdateOverlayStatus(bool isVisible)
        {
            if (ToggleOverlayButton == null || OverlayStatusTitleText == null || OverlayStatusDescText == null)
            {
                return;
            }

            if (isVisible)
            {
                ToggleOverlayButton.Content = "Скрыть";
                OverlayStatusTitleText.Text = "Оверлей рабочего стола активен";
                OverlayStatusDescText.Text = "Готов к перехвату медиа (Spotify, браузеры, Telegram)";
            }
            else
            {
                ToggleOverlayButton.Content = "Показать";
                OverlayStatusTitleText.Text = "Оверлей скрыт";
                OverlayStatusDescText.Text = "Нажмите «Показать» или используйте горячую клавишу для активации.";
            }
        }

        private void OnToggleOverlayClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.ToggleVisibility();
            UpdateOverlayStatus(_mainWindow.IsOverlayVisible);
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

            cfg.WidgetOpacity = OpacitySlider.Value / 100.0;
            _mainWindow.ApplyWidgetOpacity(cfg.WidgetOpacity);

            cfg.CompactMode = CompactModeCheckBox.IsChecked == true;
            _mainWindow.ApplyCompactMode(cfg.CompactMode);

            _configManager.Save(cfg);

            AutoStartupService.SetAutoStart(autoStart);
            _mainWindow.RegisterAllConfiguredHotkeys();

            Close();
        }

        private void OnOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || OpacityValueText == null) return;
            int percent = (int)Math.Round(e.NewValue);
            OpacityValueText.Text = $"{percent}%";
            _mainWindow.ApplyWidgetOpacity(percent / 100.0);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            _mainWindow.ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
            _mainWindow.ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
            Close();
        }
    }
}
