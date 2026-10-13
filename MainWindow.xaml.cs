using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LucidTune.Config;
using LucidTune.Models;
using LucidTune.Services;

namespace LucidTune
{
    public partial class MainWindow : Window
    {
        private readonly SmtcMediaService _mediaService;
        private readonly ConfigManager _configManager;
        private readonly Geometry _playGeometry;
        private readonly Geometry _pauseGeometry;

        private double _currentProgress;

        public MainWindow()
        {
            InitializeComponent();

            _playGeometry = (Geometry)FindResource("PlayGeometry");
            _pauseGeometry = (Geometry)FindResource("PauseGeometry");

            _configManager = new ConfigManager();
            _mediaService = new SmtcMediaService();

            _mediaService.TrackInfoChanged += OnTrackInfoChanged;
            _mediaService.ProgressUpdated += OnProgressUpdated;

            Loaded += OnMainWindowLoaded;
            Closing += OnMainWindowClosing;
            ProgressBarTrack.SizeChanged += (s, e) => UpdateProgressBarWidth();
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            ApplySavedPosition();
            await _mediaService.InitializeAsync();
        }

        private void ApplySavedPosition()
        {
            var targetPos = _configManager.GetValidatedPosition(Width, Height);
            Left = targetPos.X;
            Top = targetPos.Y;
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
            }
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