using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        private TaskbarWidgetWindow? _taskbarWindow;
        private double _currentProgress;
        private bool _isWindowVisible = true;
        private bool _hiddenByFullscreen;
        private bool _isAnimating;
        private SettingsWindow? _settingsWindow;
        private MediaTrackInfo? _lastTrackInfo;
        private Storyboard? _shimmerStoryboard;
        private string? _currentTrackKey;
        private ExtractedPalette? _currentPalette;

        private DispatcherTimer? _spectrumTimer;
        private DateTime _spectrumStartTime;
        private readonly double[] _specBarCurrentHeights = [2.0, 2.0, 2.0, 2.0, 2.0];

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

            _spectrumStartTime = DateTime.UtcNow;
            _spectrumTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(35)
            };
            _spectrumTimer.Tick += OnSpectrumTimerTick;
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LocalizationService.SetLanguage(_configManager.CurrentConfig.Language);
                ApplySavedPosition();
                ApplyWidgetOpacity(_configManager.CurrentConfig.WidgetOpacity);
                ApplyCompactMode(_configManager.CurrentConfig.CompactMode);
                ApplyVolumeControl(_configManager.CurrentConfig.ShowVolumeControl);
                SyncVolumeFromSystem();

                var hwnd = new WindowInteropHelper(this).Handle;
                ApplyCaptureAffinity(_configManager.CurrentConfig.ExcludeFromCapture);

                _hotkeyService.Initialize(hwnd);
                RegisterAllConfiguredHotkeys();
                _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

                _fullscreenService.Start(hwnd);

                _taskbarWindow = new TaskbarWidgetWindow(_mediaService, _configManager, _volumeService, this);

                _trayManager = new TrayIconManager(_configManager, ToggleVisibility, OpenSettingsWindow, () => _isWindowVisible, ApplyTaskbarMode);
                _trayManager.Initialize(hwnd);

                if (_configManager.CurrentConfig.TaskbarModeEnabled)
                {
                    ApplyTaskbarMode(true);
                }

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
                try
                {
                    var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LucidTune");
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [OnMainWindowLoaded] {ex}\n");
                }
                catch { }
            }
        }

        private void ApplySavedPosition()
        {
            var targetPos = _configManager.GetValidatedPosition(Width, Height);
            Left = targetPos.X;
            Top = targetPos.Y;
        }

        public void ApplyTaskbarMode(bool enabled)
        {
            _configManager.CurrentConfig.TaskbarModeEnabled = enabled;

            if (enabled)
            {
                Hide();
                _isWindowVisible = false;
                OverlayVisibilityChanged?.Invoke(false);

                if (_taskbarWindow == null)
                {
                    _taskbarWindow = new TaskbarWidgetWindow(_mediaService, _configManager, _volumeService, this);
                }

                _taskbarWindow.Show();
                if (_lastTrackInfo != null)
                {
                    _taskbarWindow.UpdateTrackInfo(_lastTrackInfo);
                }
                if (_currentPalette != null)
                {
                    _taskbarWindow.UpdatePalette(_currentPalette);
                }
                _taskbarWindow.RepositionOnTaskbar();
            }
            else
            {
                _taskbarWindow?.Hide();
                Show();
                _isWindowVisible = true;
                OverlayVisibilityChanged?.Invoke(true);
                ApplySavedPosition();
            }

            _trayManager?.UpdateState();
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
                Width = 360;
            }
            else
            {
                ArtBlock.Visibility = Visibility.Visible;
                TrackInfoGrid.Margin = new Thickness(10, 0, 8, 0);
                ArtColumn.Width = new GridLength(62);
                IslandContainer.Width = 310;
                Width = 360;
            }

            UpdateSpectrumVisibility();
        }

        public void ApplyAudioSpectrum(bool enabled)
        {
            _configManager.CurrentConfig.ShowAudioSpectrum = enabled;
            UpdateSpectrumVisibility();
        }

        public void ApplyTaskbarPosition(string position)
        {
            _configManager.CurrentConfig.TaskbarPosition = position;
            _taskbarWindow?.RepositionOnTaskbar();
        }

        public void ApplyTaskbarVideoBehavior(string behavior)
        {
            _configManager.CurrentConfig.TaskbarFullscreenVideoBehavior = behavior;
            _taskbarWindow?.RepositionOnTaskbar();
        }

        public void ApplyLanguage(string language)
        {
            _configManager.CurrentConfig.Language = language;
            LocalizationService.SetLanguage(language);
            _trayManager?.UpdateState();
            _taskbarWindow?.UpdateLocalization();
            UpdateSpectrumVisibility();
        }

        public void UpdateSpectrumVisibility()
        {
            bool canShow = _configManager.CurrentConfig.ShowAudioSpectrum &&
                           !_configManager.CurrentConfig.CompactMode &&
                           _lastTrackInfo?.HasActiveSession == true;

            if (canShow)
            {
                AudioSpectrumPanel.Visibility = Visibility.Visible;
                if (_lastTrackInfo?.IsPlaying == true)
                {
                    if (_spectrumTimer?.IsEnabled != true) _spectrumTimer?.Start();
                }
                else
                {
                    _spectrumTimer?.Stop();
                    ResetSpectrumBars();
                }
            }
            else
            {
                AudioSpectrumPanel.Visibility = Visibility.Collapsed;
                _spectrumTimer?.Stop();
                ResetSpectrumBars();
            }
        }

        private void OnSpectrumTimerTick(object? sender, EventArgs e)
        {
            if (SpecBar1 == null) return;

            bool isPlaying = _lastTrackInfo?.IsPlaying == true;
            float normPeak = isPlaying ? _volumeService.GetNormalizedPeakSample() : 0f;

            // If music is paused, silenced, or in a quiet break (peak near 0)
            if (!isPlaying || normPeak < 0.002f)
            {
                for (int i = 0; i < 5; i++)
                {
                    _specBarCurrentHeights[i] = Math.Max(2.0, _specBarCurrentHeights[i] * 0.78);
                }
            }
            else
            {
                double t = (DateTime.UtcNow - _spectrumStartTime).TotalSeconds;
                // Non-linear perceptual loudness response (square root)
                double intensity = Math.Sqrt(Math.Clamp(normPeak, 0.0, 1.0));

                // Targets for 5 frequency bands (Bass, Low-Mid, Mid, High-Mid, Treble)
                double[] targets = [
                    2.0 + intensity * 12.0 * (0.80 + 0.20 * Math.Sin(t * 11.0 + 0.3)),
                    2.0 + intensity * 12.0 * (0.95 + 0.18 * Math.Sin(t * 14.5 + 1.2)),
                    2.0 + intensity * 12.0 * (1.00 + 0.22 * Math.Sin(t * 17.0 + 2.5)),
                    2.0 + intensity * 12.0 * (0.90 + 0.19 * Math.Sin(t * 13.5 + 3.8)),
                    2.0 + intensity * 12.0 * (0.75 + 0.16 * Math.Sin(t * 15.5 + 5.0))
                ];

                for (int i = 0; i < 5; i++)
                {
                    double target = Math.Clamp(targets[i], 2.0, 14.0);
                    if (target > _specBarCurrentHeights[i])
                    {
                        // Fast attack for beats and transients
                        _specBarCurrentHeights[i] = _specBarCurrentHeights[i] * 0.20 + target * 0.80;
                    }
                    else
                    {
                        // Smooth decay
                        _specBarCurrentHeights[i] = Math.Max(2.0, _specBarCurrentHeights[i] * 0.82);
                    }
                }
            }

            SpecBar1.Height = _specBarCurrentHeights[0];
            SpecBar2.Height = _specBarCurrentHeights[1];
            SpecBar3.Height = _specBarCurrentHeights[2];
            SpecBar4.Height = _specBarCurrentHeights[3];
            SpecBar5.Height = _specBarCurrentHeights[4];
        }

        private void ResetSpectrumBars()
        {
            if (SpecBar1 == null) return;
            for (int i = 0; i < 5; i++) _specBarCurrentHeights[i] = 2.0;
            SpecBar1.Height = 2;
            SpecBar2.Height = 2;
            SpecBar3.Height = 2;
            SpecBar4.Height = 2;
            SpecBar5.Height = 2;
        }

        public void ApplyCaptureAffinity(bool exclude)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                if (exclude)
                {
                    if (!SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
                    {
                        SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
                    }
                }
                else
                {
                    SetWindowDisplayAffinity(hwnd, WDA_NONE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApplyCaptureAffinity Exception] {ex.Message}");
            }
        }

        public void ApplyAdaptiveColor(bool enabled)
        {
            if (enabled && _lastTrackInfo?.Thumbnail != null)
            {
                if (_currentPalette == null)
                {
                    _currentPalette = ColorExtractor.ExtractPalette(_lastTrackInfo.Thumbnail);
                }

                if (_currentPalette != null)
                {
                    ThemeManager.ApplyDynamicAccent(_currentPalette.PrimaryAccent);
                    if (_configManager.CurrentConfig.AdaptiveBackgroundShimmer)
                    {
                        ApplyShimmerPalette(_currentPalette);
                    }
                    else
                    {
                        ResetShimmerPalette();
                    }
                    _taskbarWindow?.UpdatePalette(_currentPalette);
                    return;
                }
            }

            _currentPalette = null;
            ThemeManager.RestoreStaticAccent();
            ResetShimmerPalette();
            _taskbarWindow?.UpdatePalette(null);
        }

        public void ApplyBackgroundShimmer(bool enabled)
        {
            if (enabled && _configManager.CurrentConfig.AdaptiveColorEnabled && _lastTrackInfo?.Thumbnail != null)
            {
                if (_currentPalette == null)
                {
                    _currentPalette = ColorExtractor.ExtractPalette(_lastTrackInfo.Thumbnail);
                }

                if (_currentPalette != null)
                {
                    ApplyShimmerPalette(_currentPalette);
                    return;
                }
            }

            ResetShimmerPalette();
        }

        private void ApplyShimmerPalette(ExtractedPalette palette)
        {
            if (ShimmerStop1 == null || ShimmerStop2 == null || ShimmerStop3 == null || AmbientShimmerLayer == null) return;

            ShimmerStop1.Color = palette.AmbientShimmer1;
            ShimmerStop2.Color = palette.AmbientShimmer2;
            ShimmerStop3.Color = palette.AmbientShimmer3;

            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AmbientShimmerLayer.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            if (_lastTrackInfo?.IsPlaying == true)
            {
                StartShimmerAnimation();
            }
            else
            {
                StopShimmerAnimation();
            }
        }

        private void ResetShimmerPalette()
        {
            StopShimmerAnimation();
            if (AmbientShimmerLayer == null) return;

            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            AmbientShimmerLayer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void StartShimmerAnimation()
        {
            if (_shimmerStoryboard != null || ShimmerTranslate == null) return;

            var animX = new DoubleAnimation(-0.35, 0.35, TimeSpan.FromSeconds(8))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var animY = new DoubleAnimation(-0.25, 0.25, TimeSpan.FromSeconds(11))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(animX, ShimmerTranslate);
            Storyboard.SetTargetProperty(animX, new PropertyPath(TranslateTransform.XProperty));

            Storyboard.SetTarget(animY, ShimmerTranslate);
            Storyboard.SetTargetProperty(animY, new PropertyPath(TranslateTransform.YProperty));

            _shimmerStoryboard = new Storyboard();
            _shimmerStoryboard.Children.Add(animX);
            _shimmerStoryboard.Children.Add(animY);
            _shimmerStoryboard.Begin();
        }

        private void StopShimmerAnimation()
        {
            if (_shimmerStoryboard == null) return;
            _shimmerStoryboard.Stop();
            _shimmerStoryboard = null;
            if (ShimmerTranslate != null)
            {
                ShimmerTranslate.X = 0;
                ShimmerTranslate.Y = 0;
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
            if (_configManager.CurrentConfig.TaskbarModeEnabled)
            {
                if (_taskbarWindow != null)
                {
                    if (_taskbarWindow.IsVisible)
                    {
                        _taskbarWindow.Hide();
                        _isWindowVisible = false;
                    }
                    else
                    {
                        _taskbarWindow.Show();
                        _taskbarWindow.RepositionOnTaskbar();
                        _isWindowVisible = true;
                    }
                    OverlayVisibilityChanged?.Invoke(_isWindowVisible);
                }
                return;
            }

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

        private void OnFullscreenStateChanged(FullscreenInfo info)
        {
            _taskbarWindow?.OnFullscreenChanged(info);

            if (!_configManager.CurrentConfig.AutoHideInFullscreen) return;

            Dispatcher.Invoke(() =>
            {
                if (info.IsFullscreen)
                {
                    if (_isWindowVisible && !_configManager.CurrentConfig.TaskbarModeEnabled)
                    {
                        _hiddenByFullscreen = true;
                        HideWithAnimation();
                    }
                }
                else
                {
                    if (_hiddenByFullscreen && !_configManager.CurrentConfig.TaskbarModeEnabled)
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
                    try
                    {
                        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LucidTune");
                        System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [OpenSettingsWindow] {ex}\n");
                    }
                    catch { }
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
                // Unique key to distinguish different songs
                string newTrackKey = $"{track.SourceAppUserModelId}|{track.Artist}|{track.Title}".Trim();
                bool isNewTrack = !string.Equals(newTrackKey, _currentTrackKey, StringComparison.OrdinalIgnoreCase);

                // Prevent seeking/scrubbing glitches:
                // If it is the SAME track and incoming thumbnail is null, retain previous valid thumbnail
                if (!isNewTrack && track.Thumbnail == null && _lastTrackInfo?.Thumbnail != null)
                {
                    track.Thumbnail = _lastTrackInfo.Thumbnail;
                    track.ThumbnailBytes = _lastTrackInfo.ThumbnailBytes;
                }

                _lastTrackInfo = track;
                TitleText.Text = track.Title;
                ArtistText.Text = track.Artist;

                if (track.Thumbnail != null)
                {
                    if (isNewTrack || AlbumArtImage.Source == null)
                    {
                        AlbumArtImage.Source = track.Thumbnail;
                    }
                    AlbumArtImage.Visibility = Visibility.Visible;
                    PlaceholderArt.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AlbumArtImage.Source = null;
                    AlbumArtImage.Visibility = Visibility.Collapsed;
                    PlaceholderArt.Visibility = Visibility.Visible;
                }

                // ONLY update Adaptive Accent & Palette if the song changed OR palette hasn't been extracted for this thumbnail yet
                bool needsPaletteUpdate = isNewTrack || (_currentPalette == null && track.Thumbnail != null);

                if (needsPaletteUpdate)
                {
                    _currentTrackKey = newTrackKey;

                    if (_configManager.CurrentConfig.AdaptiveColorEnabled)
                    {
                        if (track.Thumbnail != null)
                        {
                            _currentPalette = ColorExtractor.ExtractPalette(track.Thumbnail);
                            if (_currentPalette != null)
                            {
                                ThemeManager.ApplyDynamicAccent(_currentPalette.PrimaryAccent);
                                if (_configManager.CurrentConfig.AdaptiveBackgroundShimmer)
                                {
                                    ApplyShimmerPalette(_currentPalette);
                                }
                                else
                                {
                                    ResetShimmerPalette();
                                }
                            }
                            else
                            {
                                _currentPalette = null;
                                ThemeManager.RestoreStaticAccent();
                                ResetShimmerPalette();
                            }
                        }
                        else
                        {
                            _currentPalette = null;
                            ThemeManager.RestoreStaticAccent();
                            ResetShimmerPalette();
                        }
                    }
                    else
                    {
                        _currentPalette = null;
                        ThemeManager.RestoreStaticAccent();
                        ResetShimmerPalette();
                    }
                }
                else
                {
                    // Same track: just sync shimmer play/pause state without flickering colors!
                    if (_configManager.CurrentConfig.AdaptiveColorEnabled &&
                        _configManager.CurrentConfig.AdaptiveBackgroundShimmer &&
                        _currentPalette != null)
                    {
                        if (AmbientShimmerLayer != null && AmbientShimmerLayer.Opacity < 0.1)
                        {
                            ApplyShimmerPalette(_currentPalette);
                        }
                        else if (track.IsPlaying)
                        {
                            StartShimmerAnimation();
                        }
                        else
                        {
                            StopShimmerAnimation();
                        }
                    }
                    else
                    {
                        if (AmbientShimmerLayer != null && AmbientShimmerLayer.Opacity > 0.0)
                        {
                            ResetShimmerPalette();
                        }
                    }
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
                UpdateSpectrumVisibility();

                _taskbarWindow?.UpdateTrackInfo(track);
                _taskbarWindow?.UpdatePalette(_currentPalette);

                if (isNewTrack)
                {
                    App.TrimWorkingSet();
                }
            });
        }

        private void OnProgressUpdated(TimeSpan current, TimeSpan duration, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                _currentProgress = progress;
                UpdateProgressBarWidth();
                _taskbarWindow?.UpdateProgress(current, duration, progress);
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
                if (_configManager.CurrentConfig.SnapToScreenEdges)
                {
                    SnapToScreenEdges();
                }
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
            _spectrumTimer?.Stop();
            _taskbarWindow?.Close();
            _taskbarWindow = null;
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

        private void SnapToScreenEdges()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (hMonitor == IntPtr.Zero) return;

                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(hMonitor, ref mi)) return;

                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX;
                double dpiY = dpi.DpiScaleY;

                double workLeft = mi.rcWork.Left / dpiX;
                double workTop = mi.rcWork.Top / dpiY;
                double workRight = mi.rcWork.Right / dpiX;
                double workBottom = mi.rcWork.Bottom / dpiY;
                double workWidth = workRight - workLeft;

                double cardWidth = IslandContainer.ActualWidth > 0 ? IslandContainer.ActualWidth : IslandContainer.Width;
                double cardHeight = IslandContainer.ActualHeight > 0 ? IslandContainer.ActualHeight : IslandContainer.Height;

                double cardOffsetX = (Width - cardWidth) / 2.0;
                double cardOffsetY = 20.0;

                double visibleLeft = Left + cardOffsetX;
                double visibleRight = visibleLeft + cardWidth;
                double visibleTop = Top + cardOffsetY;
                double visibleBottom = visibleTop + cardHeight;

                const double snapThreshold = 24.0;
                const double screenMargin = 12.0;

                double newVisibleLeft = visibleLeft;
                double newVisibleTop = visibleTop;

                // Left Edge Snap
                if (Math.Abs(visibleLeft - (workLeft + screenMargin)) <= snapThreshold ||
                    (visibleLeft < workLeft + screenMargin && visibleLeft >= workLeft - 30))
                {
                    newVisibleLeft = workLeft + screenMargin;
                }
                // Right Edge Snap
                else if (Math.Abs(visibleRight - (workRight - screenMargin)) <= snapThreshold ||
                         (visibleRight > workRight - screenMargin && visibleRight <= workRight + 30))
                {
                    newVisibleLeft = workRight - screenMargin - cardWidth;
                }
                // Horizontal Center Snap
                else
                {
                    double workCenterX = workLeft + workWidth / 2.0;
                    double visibleCenterX = visibleLeft + cardWidth / 2.0;
                    if (Math.Abs(visibleCenterX - workCenterX) <= snapThreshold)
                    {
                        newVisibleLeft = workCenterX - cardWidth / 2.0;
                    }
                }

                // Top Edge Snap
                if (Math.Abs(visibleTop - (workTop + screenMargin)) <= snapThreshold ||
                    (visibleTop < workTop + screenMargin && visibleTop >= workTop - 30))
                {
                    newVisibleTop = workTop + screenMargin;
                }
                // Bottom Edge Snap
                else if (Math.Abs(visibleBottom - (workBottom - screenMargin)) <= snapThreshold ||
                         (visibleBottom > workBottom - screenMargin && visibleBottom <= workBottom + 30))
                {
                    newVisibleTop = workBottom - screenMargin - cardHeight;
                }

                Left = newVisibleLeft - cardOffsetX;
                Top = newVisibleTop - cardOffsetY;
            }
            catch
            {
                // Ignore snapping exception
            }
        }

        private void SaveCurrentPosition()
        {
            var config = _configManager.CurrentConfig;
            config.WindowX = Left;
            config.WindowY = Top;
            _configManager.Save(config);
        }

        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_MONITOR = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }
}