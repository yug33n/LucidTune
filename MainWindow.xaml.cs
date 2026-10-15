using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LucidTune.Config;
using LucidTune.Models;
using LucidTune.Services;
using LucidTune.UI;

namespace LucidTune
{
    public partial class MainWindow : Window
    {
        private readonly SmtcMediaService _mediaService;
        private readonly ConfigManager _configManager;
        private readonly HotkeyService _hotkeyService;
        private readonly FullscreenDetectorService _fullscreenService;
        private readonly DiscordRpcService _discordRpcService;
        private readonly Geometry _playGeometry;
        private readonly Geometry _pauseGeometry;

        private readonly AudioVolumeService _volumeService = new();
        private bool _isUpdatingVolumeFromSystem;

        private TrayIconManager? _trayManager;
        private double _currentProgress;
        private bool _isWindowVisible = true;
        private bool _hiddenByFullscreen;
        private bool _isAnimating;
        private SettingsWindow? _settingsWindow;

        public bool IsOverlayVisible => _isWindowVisible;
        public event Action<bool>? OverlayVisibilityChanged;

        public MainWindow()
        {
            InitializeComponent();
            TrySetWindowIcon(this);

            _playGeometry = (Geometry)FindResource("PlayGeometry");
            _pauseGeometry = (Geometry)FindResource("PauseGeometry");

            _configManager = new ConfigManager();
            _mediaService = new SmtcMediaService();
            _hotkeyService = new HotkeyService();
            _fullscreenService = new FullscreenDetectorService();
            _discordRpcService = new DiscordRpcService(_configManager);

            _mediaService.TrackInfoChanged += OnTrackInfoChanged;
            _mediaService.TrackInfoChanged += _discordRpcService.OnTrackInfoChanged;
            _mediaService.ProgressUpdated += OnProgressUpdated;
            _mediaService.ProgressUpdated += _discordRpcService.OnProgressUpdated;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _fullscreenService.FullscreenStateChanged += OnFullscreenStateChanged;

            Loaded += OnMainWindowLoaded;
            Closing += OnMainWindowClosing;
            ProgressBarTrack.SizeChanged += (s, e) => UpdateProgressBarWidth();
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplySavedPosition();
                ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
                ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
                ApplyVolumeControl(_configManager.CurrentConfig.ShowVolumeControl);
                SyncVolumeFromSystem();

                var hwnd = new WindowInteropHelper(this).Handle;
                _hotkeyService.Initialize(hwnd);
                RegisterAllConfiguredHotkeys();
                _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

                _fullscreenService.Start(hwnd);

                _trayManager = new TrayIconManager(_configManager, ToggleVisibility, OpenSettingsWindow, () => _isWindowVisible);
                _trayManager.Initialize(hwnd);

                if (_configManager.CurrentConfig.AutoStart)
                {
                    AutoStartupService.SetAutoStart(true);
                }

                if (!App.IsAutoStartLaunch && _configManager.CurrentConfig.ShowSettingsOnStartup)
                {
                    OpenSettingsWindow();
                }

                await _mediaService.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnMainWindowLoaded Exception] {ex}");
            }
        }

        private void ApplySavedPosition()
        {
            var targetPos = _configManager.GetValidatedPosition(Width, Height);
            Left = targetPos.X;
            Top = targetPos.Y;
        }

        public void ApplyWidgetOpacity(double opacity)
        {
            Opacity = Math.Clamp(opacity, 0.2, 1.0);
        }

        public void ApplyCompactMode(bool isCompact)
        {
            if (isCompact)
            {
                ArtColumn.Width = new GridLength(0);
                ArtBlock.Visibility = Visibility.Collapsed;
                TrackInfoGrid.Margin = new Thickness(14, 0, 8, 0);
                IslandContainer.Width = 248;
            }
            else
            {
                ArtColumn.Width = new GridLength(62);
                ArtBlock.Visibility = Visibility.Visible;
                TrackInfoGrid.Margin = new Thickness(10, 0, 8, 0);
                IslandContainer.Width = 310;
            }
        }

        public void ApplyVolumeControl(bool showVolume)
        {
            VolumeControlRow.Visibility = showVolume ? Visibility.Visible : Visibility.Collapsed;
            IslandContainer.Height = showVolume ? 92 : 66;
            ContentLayoutGrid.Margin = showVolume ? new Thickness(0, 5, 0, 7) : new Thickness(0, 7, 0, 7);
        }

        public void ToggleVisibility()
        {
            _hiddenByFullscreen = false;
            if (_isWindowVisible)
            {
                HideWithAnimation();
            }
            else
            {
                ShowWithAnimation();
            }
        }

        public void HideWithAnimation()
        {
            if (_isAnimating || !_isWindowVisible) return;
            _isAnimating = true;

            var duration = TimeSpan.FromMilliseconds(220);
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            var animY = new DoubleAnimation(0, 48, duration) { EasingFunction = easeIn };
            var animScaleX = new DoubleAnimation(1.0, 0.85, duration) { EasingFunction = easeIn };
            var animScaleY = new DoubleAnimation(1.0, 0.85, duration) { EasingFunction = easeIn };
            var animOpacity = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easeIn };

            animOpacity.Completed += (s, e) =>
            {
                Hide();
                _isWindowVisible = false;
                _isAnimating = false;
                OverlayVisibilityChanged?.Invoke(false);

                ContainerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                IslandContainer.BeginAnimation(UIElement.OpacityProperty, null);

                ContainerTranslate.Y = 0;
                ContainerScale.ScaleX = 1.0;
                ContainerScale.ScaleY = 1.0;
                IslandContainer.Opacity = 1.0;

                HideButton.BeginAnimation(UIElement.OpacityProperty, null);
                HideButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                HideButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                HideButton.Opacity = 0.0;
                HideButton.IsHitTestVisible = false;
                HideButtonScale.ScaleX = 0.82;
                HideButtonScale.ScaleY = 0.82;
            };

            ContainerTranslate.BeginAnimation(TranslateTransform.YProperty, animY);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
            IslandContainer.BeginAnimation(UIElement.OpacityProperty, animOpacity);
        }

        public void ShowWithAnimation()
        {
            if (_isAnimating || _isWindowVisible) return;
            _isAnimating = true;

            ContainerTranslate.Y = 48;
            ContainerScale.ScaleX = 0.85;
            ContainerScale.ScaleY = 0.85;
            IslandContainer.Opacity = 0.0;

            Show();
            Topmost = true;
            Activate();
            _isWindowVisible = true;
            OverlayVisibilityChanged?.Invoke(true);

            var duration = TimeSpan.FromMilliseconds(250);
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            var animY = new DoubleAnimation(48, 0, duration) { EasingFunction = easeOut };
            var animScaleX = new DoubleAnimation(0.85, 1.0, duration) { EasingFunction = easeOut };
            var animScaleY = new DoubleAnimation(0.85, 1.0, duration) { EasingFunction = easeOut };
            var animOpacity = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = easeOut };

            animOpacity.Completed += (s, e) =>
            {
                _isAnimating = false;

                ContainerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                IslandContainer.BeginAnimation(UIElement.OpacityProperty, null);

                ContainerTranslate.Y = 0;
                ContainerScale.ScaleX = 1.0;
                ContainerScale.ScaleY = 1.0;
                IslandContainer.Opacity = 1.0;
            };

            ContainerTranslate.BeginAnimation(TranslateTransform.YProperty, animY);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
            IslandContainer.BeginAnimation(UIElement.OpacityProperty, animOpacity);
        }

        private void OnFullscreenStateChanged(bool isFullscreen)
        {
            if (!_configManager.CurrentConfig.AutoHideInFullscreen) return;

            Dispatcher.Invoke(() =>
            {
                if (isFullscreen)
                {
                    if (_isWindowVisible)
                    {
                        _hiddenByFullscreen = true;
                        HideWithAnimation();
                    }
                }
                else
                {
                    if (_hiddenByFullscreen)
                    {
                        _hiddenByFullscreen = false;
                        ShowWithAnimation();
                    }
                }
            });
        }

        public void OpenSettingsWindow()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_settingsWindow != null && _settingsWindow.IsLoaded)
                    {
                        _settingsWindow.Activate();
                        return;
                    }

                    _settingsWindow = new SettingsWindow(this, _configManager);
                    _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                    _settingsWindow.Show();
                    _settingsWindow.Activate();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open settings window: {ex.Message}");
                }
            });
        }

        public void RegisterAllConfiguredHotkeys()
        {
            var cfg = _configManager.CurrentConfig;
            _hotkeyService.UnregisterAll();

            _hotkeyService.Register(HotkeyAction.ToggleVisibility, cfg.HotkeyModifiers, cfg.HotkeyKey);

            if (cfg.MediaHotkeysEnabled)
            {
                _hotkeyService.Register(HotkeyAction.PlayPause, cfg.PlayPauseModifiers, cfg.PlayPauseKey);
                _hotkeyService.Register(HotkeyAction.NextTrack, cfg.NextTrackModifiers, cfg.NextTrackKey);
                _hotkeyService.Register(HotkeyAction.PreviousTrack, cfg.PrevTrackModifiers, cfg.PrevTrackKey);
            }
        }

        private void OnHotkeyTriggered(HotkeyAction action)
        {
            Dispatcher.Invoke(async () =>
            {
                switch (action)
                {
                    case HotkeyAction.ToggleVisibility:
                        ToggleVisibility();
                        break;
                    case HotkeyAction.PlayPause:
                        await _mediaService.TogglePlayPauseAsync();
                        break;
                    case HotkeyAction.NextTrack:
                        await _mediaService.SkipNextAsync();
                        break;
                    case HotkeyAction.PreviousTrack:
                        await _mediaService.SkipPreviousAsync();
                        break;
                }
            });
        }

        public void ApplyNewHotkey(string modifier, string key)
        {
            RegisterAllConfiguredHotkeys();
        }

        private void OnHotkeyPressed()
        {
            Dispatcher.Invoke(ToggleVisibility);
        }

        private void OnTrackInfoChanged(MediaTrackInfo track)
        {
            Dispatcher.Invoke(() =>
            {
                TitleText.Text = track.Title;
                ArtistText.Text = track.Artist;

                if (track.Thumbnail != null)
                {
                    AlbumArtImage.Source = track.Thumbnail;
                    AlbumArtImage.Visibility = Visibility.Visible;
                    PlaceholderArt.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AlbumArtImage.Source = null;
                    AlbumArtImage.Visibility = Visibility.Collapsed;
                    PlaceholderArt.Visibility = Visibility.Visible;
                }

                ArtContainer.Opacity = track.IsPlaying ? 1.0 : 0.65;
                PlayPauseIcon.Data = track.IsPlaying ? _pauseGeometry : _playGeometry;
                PlayPauseIcon.Margin = track.IsPlaying ? new Thickness(0) : new Thickness(2, 0, 0, 0);

                bool controlsEnabled = track.HasActiveSession;
                PrevButton.IsEnabled = controlsEnabled;
                PlayPauseButton.IsEnabled = controlsEnabled;
                NextButton.IsEnabled = controlsEnabled;

                _volumeService.SetTargetApp(track.SourceAppUserModelId);
                SyncVolumeFromSystem();
            });
        }

        private void OnProgressUpdated(TimeSpan current, TimeSpan duration, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                _currentProgress = progress;
                UpdateProgressBarWidth();
            });
        }

        private void UpdateProgressBarWidth()
        {
            double trackWidth = ProgressBarTrack.ActualWidth;
            if (trackWidth > 0)
            {
                ProgressBarFill.Width = Math.Clamp(trackWidth * _currentProgress, 0, trackWidth);
            }
        }

        private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                SaveCurrentPosition();
            }
        }

        private void OnHideButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            ToggleVisibility();
        }

        private void OnMainCardMouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isWindowVisible || _isAnimating) return;

            SyncVolumeFromSystem();
            HideButton.IsHitTestVisible = true;

            var duration = TimeSpan.FromMilliseconds(220);
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            var opacityAnim = new DoubleAnimation(1.0, duration) { EasingFunction = easeOut };
            var scaleAnim = new DoubleAnimation(1.0, duration) { EasingFunction = easeOut };

            HideButton.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            HideButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            HideButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void OnMainCardMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isWindowVisible || _isAnimating) return;

            var duration = TimeSpan.FromMilliseconds(200);
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            var opacityAnim = new DoubleAnimation(0.0, duration) { EasingFunction = easeIn };
            var scaleAnim = new DoubleAnimation(0.82, duration) { EasingFunction = easeIn };

            opacityAnim.Completed += (s, args) =>
            {
                if (!MainCard.IsMouseOver)
                {
                    HideButton.IsHitTestVisible = false;
                }
            };

            HideButton.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            HideButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            HideButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void SyncVolumeFromSystem()
        {
            if (_isUpdatingVolumeFromSystem || VolumeSlider == null) return;
            try
            {
                _isUpdatingVolumeFromSystem = true;
                float vol = _volumeService.GetVolume();
                bool muted = _volumeService.IsMuted();
                VolumeSlider.Value = Math.Round(vol * 100);
                UpdateVolumeVisuals(muted, VolumeSlider.Value);
            }
            catch
            {
                // Ignore volume query errors
            }
            finally
            {
                _isUpdatingVolumeFromSystem = false;
            }
        }

        private void UpdateVolumeVisuals(bool isMuted, double volume)
        {
            if (VolumeIconPath == null) return;

            if (isMuted || volume <= 0.5)
            {
                VolumeIconPath.Data = (Geometry)FindResource("VolumeMutedGeometry");
                VolumeIconPath.Opacity = 0.55;
            }
            else if (volume < 50)
            {
                VolumeIconPath.Data = (Geometry)FindResource("VolumeLowGeometry");
                VolumeIconPath.Opacity = 0.85;
            }
            else
            {
                VolumeIconPath.Data = (Geometry)FindResource("VolumeHighGeometry");
                VolumeIconPath.Opacity = 1.0;
            }

            if (VolumeSlider != null)
            {
                string? target = _volumeService.TargetAppDisplayName;
                VolumeSlider.ToolTip = !string.IsNullOrEmpty(target) ? $"Громкость: {target}" : "Громкость (Windows)";
            }
        }

        private void OnVolumeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingVolumeFromSystem) return;

            float level = (float)(e.NewValue / 100.0);
            _volumeService.SetVolume(level);
            UpdateVolumeVisuals(_volumeService.IsMuted(), e.NewValue);
        }

        private void OnVolumeMuteButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _volumeService.ToggleMute();
            UpdateVolumeVisuals(_volumeService.IsMuted(), VolumeSlider.Value);
        }

        private void OnVolumeSliderMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double step = (e.Delta > 0) ? 2.0 : -2.0;
            VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + step, 0, 100);
        }

        private async void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void OnPrevButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.SkipPreviousAsync();
        }

        private async void OnNextButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await _mediaService.SkipNextAsync();
        }

        private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveCurrentPosition();
            _discordRpcService.Dispose();
            _hotkeyService.Dispose();
            _fullscreenService.Dispose();
            _trayManager?.Dispose();
            _mediaService.Dispose();
        }

        public void UpdateDiscordRpcState()
        {
            _discordRpcService.UpdateClientState();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static void TrySetWindowIcon(Window window)
        {
            try
            {
                var procPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(procPath) && System.IO.File.Exists(procPath))
                {
                    if (ExtractIconEx(procPath, 0, out var hLarge, out _, 1) > 0 && hLarge != IntPtr.Zero)
                    {
                        window.Icon = Imaging.CreateBitmapSourceFromHIcon(hLarge, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        DestroyIcon(hLarge);
                        return;
                    }
                }
            }
            catch { }

            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    window.Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch { }
        }

        private void SaveCurrentPosition()
        {
            var config = _configManager.CurrentConfig;
            config.WindowX = Left;
            config.WindowY = Top;
            _configManager.Save(config);
        }
    }
}