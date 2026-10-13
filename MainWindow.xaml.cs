using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        private readonly Geometry _playGeometry;
        private readonly Geometry _pauseGeometry;

        private TrayIconManager? _trayManager;
        private double _currentProgress;
        private bool _isWindowVisible = true;
        private bool _hiddenByFullscreen;

        public MainWindow()
        {
            InitializeComponent();

            _playGeometry = (Geometry)FindResource("PlayGeometry");
            _pauseGeometry = (Geometry)FindResource("PauseGeometry");

            _configManager = new ConfigManager();
            _mediaService = new SmtcMediaService();
            _hotkeyService = new HotkeyService();
            _fullscreenService = new FullscreenDetectorService();

            _mediaService.TrackInfoChanged += OnTrackInfoChanged;
            _mediaService.ProgressUpdated += OnProgressUpdated;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _fullscreenService.FullscreenStateChanged += OnFullscreenStateChanged;

            Loaded += OnMainWindowLoaded;
            Closing += OnMainWindowClosing;
            ProgressBarTrack.SizeChanged += (s, e) => UpdateProgressBarWidth();
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            ApplySavedPosition();

            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyService.Initialize(hwnd);
            _hotkeyService.Register(_configManager.CurrentConfig.HotkeyModifiers, _configManager.CurrentConfig.HotkeyKey);

            _fullscreenService.Start(hwnd);

            _trayManager = new TrayIconManager(_configManager, ToggleVisibility, OpenHotkeySettings);
            _trayManager.Initialize(hwnd);

            await _mediaService.InitializeAsync();
        }

        private void ApplySavedPosition()
        {
            var targetPos = _configManager.GetValidatedPosition(Width, Height);
            Left = targetPos.X;
            Top = targetPos.Y;
        }

        public void ToggleVisibility()
        {
            _hiddenByFullscreen = false;
            if (_isWindowVisible)
            {
                Hide();
                _isWindowVisible = false;
            }
            else
            {
                Show();
                Topmost = true;
                Activate();
                _isWindowVisible = true;
            }
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
                        Hide();
                        _isWindowVisible = false;
                    }
                }
                else
                {
                    if (_hiddenByFullscreen)
                    {
                        _hiddenByFullscreen = false;
                        Show();
                        Topmost = true;
                        _isWindowVisible = true;
                    }
                }
            });
        }

        private void OpenHotkeySettings()
        {
            var dialog = new HotkeySettingsWindow(_configManager)
            {
                Owner = this
            };

            dialog.HotkeyChanged += (mod, key) =>
            {
                _hotkeyService.Register(mod, key);
            };

            dialog.ShowDialog();
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

                bool controlsEnabled = track.HasActiveSession;
                PrevButton.IsEnabled = controlsEnabled;
                PlayPauseButton.IsEnabled = controlsEnabled;
                NextButton.IsEnabled = controlsEnabled;
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

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            ToggleVisibility();
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
            _hotkeyService.Dispose();
            _fullscreenService.Dispose();
            _trayManager?.Dispose();
            _mediaService.Dispose();
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