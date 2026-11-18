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
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly ConfigManager _configManager;
        private bool _isInitializing = true;
        private double _targetVerticalOffset;
        private bool _isSmoothScrolling;
        private string? _latestReleaseUrl;

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
            MainWindow.TrySetWindowIcon(this);
            _mainWindow = mainWindow;
            _configManager = configManager;

            PopulateAllBoxes();
            LoadCurrentSettings();
            LocalizationService.SetLanguage(_configManager.CurrentConfig.Language);
            ApplyLocalization();
            _isInitializing = false;

            _mainWindow.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
            Closed += (s, e) =>
            {
                StopSmoothScroll();
                _mainWindow.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
                _mainWindow.ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
                _mainWindow.ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
                _mainWindow.ApplyVolumeControl(_configManager.CurrentConfig.ShowVolumeControl);
                _mainWindow.ApplyCaptureAffinity(_configManager.CurrentConfig.ExcludeFromCapture);
                ThemeManager.ApplyTheme(_configManager.CurrentConfig.Theme, _configManager.CurrentConfig.AccentColor);
                _mainWindow.ApplyAdaptiveColor(_configManager.CurrentConfig.AdaptiveColorEnabled);
                _mainWindow.ApplyBackgroundShimmer(_configManager.CurrentConfig.AdaptiveBackgroundShimmer);
                _mainWindow.ApplyTaskbarMode(_configManager.CurrentConfig.TaskbarModeEnabled);
                _mainWindow.ApplyTaskbarPosition(_configManager.CurrentConfig.TaskbarPosition);
                _mainWindow.ApplyAudioSpectrum(_configManager.CurrentConfig.ShowAudioSpectrum);
            };
            UpdateOverlayStatus(_mainWindow.IsOverlayVisible);

            Loaded += async (s, e) =>
            {
                try
                {
                    var result = await UpdateCheckService.CheckForUpdatesAsync();
                    if (result.IsUpdateAvailable && !string.IsNullOrEmpty(result.ReleaseUrl))
                    {
                        _latestReleaseUrl = result.ReleaseUrl;
                        UpdateTitleText.Text = $"Доступна новая версия LucidTune v{result.LatestVersion}!";
                        UpdateBanner.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    // Ignore background update check failures
                }
            };
        }

        private void PopulateAllBoxes()
        {
            AccentComboBox.ItemsSource = ThemeManager.AvailableAccents;

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

            SelectLanguageItem(cfg.Language);
            SelectThemeItem(cfg.Theme);
            SelectAccentItem(cfg.AccentColor);
            SelectTaskbarPositionItem(cfg.TaskbarPosition);

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
            ShowAudioSpectrumCheckBox.IsChecked = cfg.ShowAudioSpectrum;
            ShowVolumeCheckBox.IsChecked = cfg.ShowVolumeControl;
            TaskbarModeCheckBox.IsChecked = cfg.TaskbarModeEnabled;
            TaskbarOptionsPanel.Visibility = cfg.TaskbarModeEnabled ? Visibility.Visible : Visibility.Collapsed;

            AdaptiveColorCheckBox.IsChecked = cfg.AdaptiveColorEnabled;
            AdaptiveShimmerCheckBox.IsChecked = cfg.AdaptiveBackgroundShimmer;
            AdaptiveShimmerCheckBox.IsEnabled = cfg.AdaptiveColorEnabled;
            AdaptiveShimmerCheckBox.Opacity = cfg.AdaptiveColorEnabled ? 1.0 : 0.45;

            SnapToEdgesCheckBox.IsChecked = cfg.SnapToScreenEdges;
            ExcludeFromCaptureCheckBox.IsChecked = cfg.ExcludeFromCapture;

            EnableDiscordRpcCheckBox.IsChecked = cfg.DiscordRpcEnabled;
            IgnoreBrowserVideosCheckBox.IsChecked = cfg.DiscordIgnoreBrowserVideos;
            IgnoreTelegramVoiceCheckBox.IsChecked = cfg.DiscordIgnoreTelegramVoice;

            UpdateAllPreviews();
        }

        private void SelectLanguageItem(string lang)
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), lang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageComboBox.SelectedItem = item;
                    return;
                }
            }
            if (LanguageComboBox.Items.Count > 0)
            {
                LanguageComboBox.SelectedIndex = 0;
            }
        }

        private void SelectTaskbarPositionItem(string pos)
        {
            foreach (ComboBoxItem item in TaskbarPositionComboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), pos, StringComparison.OrdinalIgnoreCase))
                {
                    TaskbarPositionComboBox.SelectedItem = item;
                    return;
                }
            }
            if (TaskbarPositionComboBox.Items.Count > 0)
            {
                TaskbarPositionComboBox.SelectedIndex = 0;
            }
        }

        private void SelectThemeItem(string theme)
        {
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), theme, StringComparison.OrdinalIgnoreCase))
                {
                    ThemeComboBox.SelectedItem = item;
                    return;
                }
            }
            if (ThemeComboBox.Items.Count > 0)
            {
                ThemeComboBox.SelectedIndex = 0;
            }
        }

        private void SelectAccentItem(string accent)
        {
            foreach (var opt in ThemeManager.AvailableAccents)
            {
                if (string.Equals(opt.Key, accent, StringComparison.OrdinalIgnoreCase))
                {
                    AccentComboBox.SelectedItem = opt;
                    return;
                }
            }
            if (AccentComboBox.Items.Count > 0)
            {
                AccentComboBox.SelectedIndex = 0;
            }
        }

        private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ThemeComboBox.SelectedItem is not ComboBoxItem item) return;
            string theme = item.Tag?.ToString() ?? ThemeManager.DarkTheme;
            string accent = (AccentComboBox?.SelectedItem as ThemeManager.AccentOption)?.Key ?? ThemeManager.CurrentAccent;
            ThemeManager.ApplyTheme(theme, accent);
            UpdateAllPreviews();
        }

        private void OnAccentSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || AccentComboBox.SelectedItem is not ThemeManager.AccentOption opt) return;
            string theme = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ThemeManager.CurrentTheme;
            ThemeManager.ApplyTheme(theme, opt.Key);
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

        private ComboBox? GetOpenComboBox()
        {
            if (ThemeComboBox?.IsDropDownOpen == true) return ThemeComboBox;
            if (AccentComboBox?.IsDropDownOpen == true) return AccentComboBox;
            if (ToggleModifierBox?.IsDropDownOpen == true) return ToggleModifierBox;
            if (ToggleKeyBox?.IsDropDownOpen == true) return ToggleKeyBox;
            if (PlayPauseModifierBox?.IsDropDownOpen == true) return PlayPauseModifierBox;
            if (PlayPauseKeyBox?.IsDropDownOpen == true) return PlayPauseKeyBox;
            if (NextModifierBox?.IsDropDownOpen == true) return NextModifierBox;
            if (NextKeyBox?.IsDropDownOpen == true) return NextKeyBox;
            if (PrevModifierBox?.IsDropDownOpen == true) return PrevModifierBox;
            if (PrevKeyBox?.IsDropDownOpen == true) return PrevKeyBox;
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private void CloseAllDropDowns()
        {
            if (ThemeComboBox != null && ThemeComboBox.IsDropDownOpen) ThemeComboBox.IsDropDownOpen = false;
            if (AccentComboBox != null && AccentComboBox.IsDropDownOpen) AccentComboBox.IsDropDownOpen = false;
            if (ToggleModifierBox != null && ToggleModifierBox.IsDropDownOpen) ToggleModifierBox.IsDropDownOpen = false;
            if (ToggleKeyBox != null && ToggleKeyBox.IsDropDownOpen) ToggleKeyBox.IsDropDownOpen = false;
            if (PlayPauseModifierBox != null && PlayPauseModifierBox.IsDropDownOpen) PlayPauseModifierBox.IsDropDownOpen = false;
            if (PlayPauseKeyBox != null && PlayPauseKeyBox.IsDropDownOpen) PlayPauseKeyBox.IsDropDownOpen = false;
            if (NextModifierBox != null && NextModifierBox.IsDropDownOpen) NextModifierBox.IsDropDownOpen = false;
            if (NextKeyBox != null && NextKeyBox.IsDropDownOpen) NextKeyBox.IsDropDownOpen = false;
            if (PrevModifierBox != null && PrevModifierBox.IsDropDownOpen) PrevModifierBox.IsDropDownOpen = false;
            if (PrevKeyBox != null && PrevKeyBox.IsDropDownOpen) PrevKeyBox.IsDropDownOpen = false;
        }

        private void OnContentScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ContentScrollViewer == null) return;

            var openCombo = GetOpenComboBox();
            if (openCombo != null)
            {
                bool isOver = false;
                if (openCombo.Template.FindName("Popup", openCombo) is System.Windows.Controls.Primitives.Popup popup &&
                    popup.IsOpen &&
                    popup.Child is FrameworkElement dropDown)
                {
                    isOver = dropDown.IsMouseOver;
                    if (!isOver)
                    {
                        try
                        {
                            Point p = Mouse.GetPosition(dropDown);
                            isOver = p.X >= 0 && p.Y >= 0 && p.X <= dropDown.ActualWidth && p.Y <= dropDown.ActualHeight;
                        }
                        catch
                        {
                            // Ignore coordinate mapping error
                        }
                    }

                    if (isOver)
                    {
                        e.Handled = true;
                        var sv = FindVisualChild<ScrollViewer>(dropDown);
                        if (sv != null && sv.ScrollableHeight > 0)
                        {
                            double step = (e.Delta / 120.0) * 32.0;
                            sv.ScrollToVerticalOffset(sv.VerticalOffset - step);
                        }
                        // Consume the wheel event over the dropdown so background settings never scroll
                        return;
                    }
                }

                // If wheel rolled outside the open dropdown, close it immediately
                CloseAllDropDowns();
            }

            e.Handled = true;

            double deltaLines = (double)e.Delta / Mouse.MouseWheelDeltaForOneLine;
            double scrollStep = -deltaLines * 45.0;

            double current = ContentScrollViewer.VerticalOffset;
            if (Math.Abs(current - _targetVerticalOffset) > 150)
            {
                _targetVerticalOffset = current;
            }

            _targetVerticalOffset = Math.Clamp(_targetVerticalOffset + scrollStep, 0, ContentScrollViewer.ScrollableHeight);

            if (!_isSmoothScrolling)
            {
                _isSmoothScrolling = true;
                CompositionTarget.Rendering += OnSmoothScrollTick;
            }
        }

        private void OnSmoothScrollTick(object? sender, EventArgs e)
        {
            if (ContentScrollViewer == null)
            {
                StopSmoothScroll();
                return;
            }

            double current = ContentScrollViewer.VerticalOffset;
            double diff = _targetVerticalOffset - current;

            if (Math.Abs(diff) < 0.5)
            {
                ContentScrollViewer.ScrollToVerticalOffset(_targetVerticalOffset);
                StopSmoothScroll();
                return;
            }

            ContentScrollViewer.ScrollToVerticalOffset(current + diff * 0.28);
        }

        private void StopSmoothScroll()
        {
            if (_isSmoothScrolling)
            {
                CompositionTarget.Rendering -= OnSmoothScrollTick;
                _isSmoothScrolling = false;
            }
        }

        private void OnContentScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                CloseAllDropDowns();
            }

            if (ContentScrollViewer != null && !_isSmoothScrolling && e.VerticalChange != 0)
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
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SettingsTextMutedBrush");
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "SettingsButtonSecondaryBackgroundBrush");
            }
            else if (string.IsNullOrWhiteSpace(mod) || mod.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                textBlock.Text = key;
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SettingsBadgeTextBrush");
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "SettingsBadgeBackgroundBrush");
            }
            else
            {
                textBlock.Text = $"{mod} + {key}";
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SettingsBadgeTextBrush");
                badgeBorder.SetResourceReference(Border.BackgroundProperty, "SettingsBadgeBackgroundBrush");
            }
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || LanguageComboBox.SelectedItem is not ComboBoxItem item) return;
            string lang = item.Tag?.ToString() ?? "ru";
            LocalizationService.SetLanguage(lang);
            ApplyLocalization();
            _mainWindow.ApplyLanguage(lang);
        }

        private void OnAudioSpectrumChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool show = ShowAudioSpectrumCheckBox.IsChecked == true;
            _mainWindow.ApplyAudioSpectrum(show);
        }

        private void OnTaskbarPositionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || TaskbarPositionComboBox.SelectedItem is not ComboBoxItem item) return;
            string pos = item.Tag?.ToString() ?? "Right";
            _mainWindow.ApplyTaskbarPosition(pos);
        }

        private void OnCompactModeChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isCompact = CompactModeCheckBox.IsChecked == true;
            _mainWindow.ApplyCompactMode(isCompact);
        }

        private void OnShowVolumeChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool showVolume = ShowVolumeCheckBox.IsChecked == true;
            _mainWindow.ApplyVolumeControl(showVolume);
        }

        private void OnTaskbarModeChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isTaskbar = TaskbarModeCheckBox.IsChecked == true;
            TaskbarOptionsPanel.Visibility = isTaskbar ? Visibility.Visible : Visibility.Collapsed;
            _mainWindow.ApplyTaskbarMode(isTaskbar);
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
                ToggleOverlayButton.Content = LocalizationService.T("Hide");
                OverlayStatusTitleText.Text = LocalizationService.T("OverlayActive");
                OverlayStatusDescText.Text = LocalizationService.T("OverlayActiveDesc");
            }
            else
            {
                ToggleOverlayButton.Content = LocalizationService.T("Show");
                OverlayStatusTitleText.Text = LocalizationService.T("OverlayHidden");
                OverlayStatusDescText.Text = LocalizationService.T("OverlayHiddenDesc");
            }
        }

        private void ApplyLocalization()
        {
            Title = LocalizationService.T("SettingsTitle");

            UpdateOverlayStatus(_mainWindow.IsOverlayVisible);
            CheckUpdatesButton.Content = LocalizationService.T("Updates");

            LanguageLabelTitle.Text = LocalizationService.T("LanguageTitle");
            LanguageLabelDesc.Text = LocalizationService.T("LanguageDesc");

            ThemeLabelTitle.Text = LocalizationService.T("ThemeTitle");
            ThemeLabelDesc.Text = LocalizationService.T("ThemeDesc");
            if (ThemeComboBox.Items.Count >= 2)
            {
                ((ComboBoxItem)ThemeComboBox.Items[0]).Content = LocalizationService.T("ThemeDark");
                ((ComboBoxItem)ThemeComboBox.Items[1]).Content = LocalizationService.T("ThemeLight");
            }

            AccentLabelTitle.Text = LocalizationService.T("AccentTitle");
            AccentLabelDesc.Text = LocalizationService.T("AccentDesc");

            AdaptiveColorTitleText.Text = LocalizationService.T("AdaptiveColorTitle");
            AdaptiveColorDescText.Text = LocalizationService.T("AdaptiveColorDesc");

            AdaptiveShimmerTitleText.Text = LocalizationService.T("AdaptiveShimmerTitle");
            AdaptiveShimmerDescText.Text = LocalizationService.T("AdaptiveShimmerDesc");

            OpacityLabelTitle.Text = LocalizationService.T("OpacityTitle");
            OpacityLabelDesc.Text = LocalizationService.T("OpacityDesc");

            CompactModeTitleText.Text = LocalizationService.T("CompactModeTitle");
            CompactModeDescText.Text = LocalizationService.T("CompactModeDesc");

            SpectrumTitleText.Text = LocalizationService.T("AudioSpectrumTitle");
            SpectrumDescText.Text = LocalizationService.T("AudioSpectrumDesc");

            ShowVolumeTitleText.Text = LocalizationService.T("ShowVolumeTitle");
            ShowVolumeDescText.Text = LocalizationService.T("ShowVolumeDesc");

            TaskbarModeTitleText.Text = LocalizationService.T("TaskbarModeTitle");
            TaskbarModeDescText.Text = LocalizationService.T("TaskbarModeDesc");

            TaskbarPosTitleText.Text = LocalizationService.T("TaskbarPositionTitle");
            TaskbarPosDescText.Text = LocalizationService.T("TaskbarPositionDesc");
            if (TaskbarPositionComboBox.Items.Count >= 2)
            {
                ((ComboBoxItem)TaskbarPositionComboBox.Items[0]).Content = LocalizationService.T("TaskbarPosRight");
                ((ComboBoxItem)TaskbarPositionComboBox.Items[1]).Content = LocalizationService.T("TaskbarPosLeft");
            }

            DiscordTitleText.Text = LocalizationService.T("DiscordSectionTitle");
            DiscordDescText.Text = LocalizationService.T("DiscordEnableDesc");
            EnableDiscordRpcCheckBox.Content = LocalizationService.T("DiscordEnableTitle");
            IgnoreBrowserVideosCheckBox.Content = LocalizationService.T("DiscordIgnoreBrowserVideosTitle");
            IgnoreTelegramVoiceTitleText.Text = LocalizationService.T("DiscordIgnoreTelegramVoiceTitle");
            IgnoreTelegramVoiceDescText.Text = LocalizationService.T("DiscordIgnoreTelegramVoiceDesc");

            EnableMediaHotkeysCheckBox.Content = LocalizationService.T("HotkeyMediaTitle");
            AutoStartCheckBox.Content = LocalizationService.T("AutoStartTitle");
            AutoHideInGamesCheckBox.Content = LocalizationService.T("AutoHideTitle");
            ShowSettingsOnStartupCheckBox.Content = LocalizationService.T("ShowSettingsOnStartupTitle");

            CloseFooterButton.Content = LocalizationService.T("Close");
            SaveFooterButton.Content = LocalizationService.T("Save");
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

        private void OnAdaptiveColorChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isAdaptive = AdaptiveColorCheckBox.IsChecked == true;
            AdaptiveShimmerCheckBox.IsEnabled = isAdaptive;
            AdaptiveShimmerCheckBox.Opacity = isAdaptive ? 1.0 : 0.45;
            _mainWindow.ApplyAdaptiveColor(isAdaptive);
        }

        private void OnAdaptiveShimmerChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isShimmer = AdaptiveShimmerCheckBox.IsChecked == true;
            _mainWindow.ApplyBackgroundShimmer(isShimmer);
        }

        private void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_latestReleaseUrl))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _latestReleaseUrl,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnDownloadUpdateClick Exception] {ex.Message}");
            }
        }

        private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
        {
            if (CheckUpdatesButton == null) return;
            CheckUpdatesButton.IsEnabled = false;
            CheckUpdatesButton.Content = "Проверка...";

            try
            {
                var result = await UpdateCheckService.CheckForUpdatesAsync();
                if (result.IsUpdateAvailable && !string.IsNullOrEmpty(result.ReleaseUrl))
                {
                    _latestReleaseUrl = result.ReleaseUrl;
                    UpdateTitleText.Text = $"Доступна новая версия LucidTune v{result.LatestVersion}!";
                    UpdateBanner.Visibility = Visibility.Visible;
                    CheckUpdatesButton.Content = "Обновление!";
                }
                else
                {
                    UpdateBanner.Visibility = Visibility.Collapsed;
                    CheckUpdatesButton.Content = "Актуально";
                    await Task.Delay(2500);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnCheckUpdatesClick Exception] {ex.Message}");
                CheckUpdatesButton.Content = "Ошибка";
                await Task.Delay(2000);
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Обновления";
            }
        }

        private void OnDismissUpdateClick(object sender, RoutedEventArgs e)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
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

            bool compact = CompactModeCheckBox.IsChecked == true;
            cfg.CompactMode = compact;
            _mainWindow.ApplyCompactMode(compact);

            bool showSpectrum = ShowAudioSpectrumCheckBox.IsChecked == true;
            cfg.ShowAudioSpectrum = showSpectrum;
            _mainWindow.ApplyAudioSpectrum(showSpectrum);

            cfg.ShowVolumeControl = ShowVolumeCheckBox.IsChecked == true;
            _mainWindow.ApplyVolumeControl(cfg.ShowVolumeControl);

            bool taskbarMode = TaskbarModeCheckBox.IsChecked == true;
            cfg.TaskbarModeEnabled = taskbarMode;
            _mainWindow.ApplyTaskbarMode(taskbarMode);

            string taskbarPos = (TaskbarPositionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Right";
            cfg.TaskbarPosition = taskbarPos;
            _mainWindow.ApplyTaskbarPosition(taskbarPos);

            cfg.AdaptiveColorEnabled = AdaptiveColorCheckBox.IsChecked == true;
            cfg.AdaptiveBackgroundShimmer = AdaptiveShimmerCheckBox.IsChecked == true;

            cfg.SnapToScreenEdges = SnapToEdgesCheckBox.IsChecked == true;

            cfg.ExcludeFromCapture = ExcludeFromCaptureCheckBox.IsChecked == true;
            _mainWindow.ApplyCaptureAffinity(cfg.ExcludeFromCapture);

            cfg.Theme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ThemeManager.DarkTheme;
            cfg.AccentColor = (AccentComboBox.SelectedItem as ThemeManager.AccentOption)?.Key ?? ThemeManager.AccentYellow;
            ThemeManager.ApplyTheme(cfg.Theme, cfg.AccentColor);
            _mainWindow.ApplyAdaptiveColor(cfg.AdaptiveColorEnabled);
            _mainWindow.ApplyBackgroundShimmer(cfg.AdaptiveBackgroundShimmer);

            cfg.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ru";
            _mainWindow.ApplyLanguage(cfg.Language);

            cfg.DiscordRpcEnabled = EnableDiscordRpcCheckBox.IsChecked == true;
            cfg.DiscordIgnoreBrowserVideos = IgnoreBrowserVideosCheckBox.IsChecked ?? true;
            cfg.DiscordIgnoreTelegramVoice = IgnoreTelegramVoiceCheckBox.IsChecked == true;

            _configManager.Save(cfg);

            AutoStartupService.SetAutoStart(autoStart);
            _mainWindow.RegisterAllConfiguredHotkeys();
            _mainWindow.UpdateDiscordRpcState();

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
            _mainWindow.ApplyLanguage(_configManager.CurrentConfig.Language);
            _mainWindow.ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
            _mainWindow.ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
            _mainWindow.ApplyAudioSpectrum(_configManager.CurrentConfig.ShowAudioSpectrum);
            _mainWindow.ApplyVolumeControl(_configManager.CurrentConfig.ShowVolumeControl);
            _mainWindow.ApplyCaptureAffinity(_configManager.CurrentConfig.ExcludeFromCapture);
            ThemeManager.ApplyTheme(_configManager.CurrentConfig.Theme, _configManager.CurrentConfig.AccentColor);
            _mainWindow.ApplyAdaptiveColor(_configManager.CurrentConfig.AdaptiveColorEnabled);
            _mainWindow.ApplyBackgroundShimmer(_configManager.CurrentConfig.AdaptiveBackgroundShimmer);
            _mainWindow.ApplyTaskbarMode(_configManager.CurrentConfig.TaskbarModeEnabled);
            _mainWindow.ApplyTaskbarPosition(_configManager.CurrentConfig.TaskbarPosition);
            Close();
        }
    }
}
