using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using LucidTune.Models;

namespace LucidTune.Services
{
    public class SmtcMediaService : IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly DispatcherTimer _progressTimer;
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        private readonly object _sessionLock = new();

        private DateTime _lastTimelineUpdateUtc;
        private TimeSpan _lastReportedPosition = TimeSpan.Zero;
        private TimeSpan _trackDuration = TimeSpan.Zero;
        private bool _isPlaying;
        private bool _disposed;

        public MediaTrackInfo CurrentTrack { get; private set; } = new();

        public event Action<MediaTrackInfo>? TrackInfoChanged;
        public event Action<TimeSpan, TimeSpan, double>? ProgressUpdated;

        public SmtcMediaService()
        {
            _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += OnProgressTimerTick;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    AttachSession(_sessionManager.GetCurrentSession());
                }
            }
            catch
            {
                NotifyEmptySession();
            }
        }

        private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
        {
            lock (_sessionLock)
            {
                var oldSession = _currentSession;
                _currentSession = session;

                if (oldSession != null)
                {
                    try { oldSession.PlaybackInfoChanged -= OnPlaybackInfoChanged; } catch { }
                    try { oldSession.MediaPropertiesChanged -= OnMediaPropertiesChanged; } catch { }
                    try { oldSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged; } catch { }
                }

                if (_currentSession != null)
                {
                    try { _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged; } catch { }
                    try { _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged; } catch { }
                    try { _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged; } catch { }
                    _ = UpdateMediaStateAsync();
                }
                else
                {
                    NotifyEmptySession();
                }
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            try
            {
                AttachSession(sender.GetCurrentSession());
            }
            catch
            {
                NotifyEmptySession();
            }
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _ = UpdateMediaStateAsync();
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = UpdateMediaStateAsync();
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            UpdateTimelineState();
        }

        private async Task UpdateMediaStateAsync()
        {
            if (!await _updateLock.WaitAsync(100)) return;

            try
            {
                var session = _currentSession;
                if (session == null)
                {
                    NotifyEmptySession();
                    return;
                }

                var playbackInfo = session.GetPlaybackInfo();
                var playbackStatus = playbackInfo?.PlaybackStatus ?? GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
                _isPlaying = playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var mediaProperties = await session.TryGetMediaPropertiesAsync();
                BitmapImage? artwork = null;
                byte[]? thumbnailBytes = null;

                if (mediaProperties?.Thumbnail != null)
                {
                    (artwork, thumbnailBytes) = await LoadThumbnailAsync(mediaProperties.Thumbnail);
                }

                UpdateTimelineState();

                var trackInfo = new MediaTrackInfo
                {
                    Title = string.IsNullOrWhiteSpace(mediaProperties?.Title) ? "Нет активного трека" : mediaProperties.Title.Trim(),
                    Artist = string.IsNullOrWhiteSpace(mediaProperties?.Artist) ? "Неизвестный исполнитель" : mediaProperties.Artist.Trim(),
                    Thumbnail = artwork,
                    ThumbnailBytes = thumbnailBytes,
                    IsPlaying = _isPlaying,
                    HasActiveSession = playbackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed,
                    Position = _lastReportedPosition,
                    Duration = _trackDuration,
                    SourceAppUserModelId = session.SourceAppUserModelId,
                    PlaybackType = mediaProperties?.PlaybackType?.ToString()
                };

                CurrentTrack = trackInfo;
                TrackInfoChanged?.Invoke(trackInfo);

                if (_isPlaying && _trackDuration > TimeSpan.Zero)
                {
                    if (!_progressTimer.IsEnabled) _progressTimer.Start();
                }
                else
                {
                    if (_progressTimer.IsEnabled) _progressTimer.Stop();
                }
            }
            catch
            {
                NotifyEmptySession();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        private void UpdateTimelineState()
        {
            try
            {
                var session = _currentSession;
                if (session == null) return;

                var timeline = session.GetTimelineProperties();
                if (timeline != null)
                {
                    _lastReportedPosition = timeline.Position;
                    _trackDuration = timeline.EndTime - timeline.StartTime;
                    _lastTimelineUpdateUtc = DateTime.UtcNow;

                    double progress = 0.0;
                    if (_trackDuration > TimeSpan.Zero)
                    {
                        progress = Math.Clamp(_lastReportedPosition.TotalMilliseconds / _trackDuration.TotalMilliseconds, 0.0, 1.0);
                    }

                    ProgressUpdated?.Invoke(_lastReportedPosition, _trackDuration, progress);
                }
            }
            catch
            {
                // Ignore timeline polling errors
            }
        }

        private void OnProgressTimerTick(object? sender, EventArgs e)
        {
            if (!_isPlaying || _trackDuration <= TimeSpan.Zero)
            {
                _progressTimer.Stop();
                return;
            }

            var elapsed = DateTime.UtcNow - _lastTimelineUpdateUtc;
            var currentPos = _lastReportedPosition + elapsed;

            if (currentPos > _trackDuration)
            {
                currentPos = _trackDuration;
            }

            double progress = Math.Clamp(currentPos.TotalMilliseconds / _trackDuration.TotalMilliseconds, 0.0, 1.0);
            ProgressUpdated?.Invoke(currentPos, _trackDuration, progress);
        }

        private static async Task<(BitmapImage? Image, byte[]? Bytes)> LoadThumbnailAsync(IRandomAccessStreamReference streamRef)
        {
            try
            {
                using var readStream = await streamRef.OpenReadAsync();
                var size = (int)readStream.Size;
                if (size <= 0) return (null, null);

                var bytes = new byte[size];
                using var dataReader = new DataReader(readStream);
                await dataReader.LoadAsync((uint)size);
                dataReader.ReadBytes(bytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.EndInit();
                bitmap.Freeze();

                return (bitmap, bytes);
            }
            catch
            {
                return (null, null);
            }
        }

        private void NotifyEmptySession()
        {
            _isPlaying = false;
            _progressTimer.Stop();
            _lastReportedPosition = TimeSpan.Zero;
            _trackDuration = TimeSpan.Zero;

            var emptyInfo = new MediaTrackInfo
            {
                Title = "Нет активного трека",
                Artist = "Запустите музыку",
                Thumbnail = null,
                IsPlaying = false,
                HasActiveSession = false,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero
            };

            CurrentTrack = emptyInfo;
            TrackInfoChanged?.Invoke(emptyInfo);
            ProgressUpdated?.Invoke(TimeSpan.Zero, TimeSpan.Zero, 0.0);
        }

        public async Task<bool> TogglePlayPauseAsync()
        {
            var session = _currentSession;
            if (session == null) return false;
            try
            {
                return await session.TryTogglePlayPauseAsync();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SkipNextAsync()
        {
            var session = _currentSession;
            if (session == null) return false;
            try
            {
                return await session.TrySkipNextAsync();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SkipPreviousAsync()
        {
            var session = _currentSession;
            if (session == null) return false;
            try
            {
                return await session.TrySkipPreviousAsync();
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _progressTimer.Stop();
            if (_sessionManager != null)
            {
                try { _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged; } catch { }
            }
            lock (_sessionLock)
            {
                if (_currentSession != null)
                {
                    try { _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged; } catch { }
                    try { _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged; } catch { }
                    try { _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged; } catch { }
                    _currentSession = null;
                }
            }
            _updateLock.Dispose();
        }
    }
}
