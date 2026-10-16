using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using LucidTune.Config;
using LucidTune.Models;

namespace LucidTune.Services
{
    public enum MusicSource
    {
        Generic,
        YandexMusic,
        Spotify,
        SoundCloud
    }

    public class DiscordRpcService : IDisposable
    {
        private readonly ConfigManager _configManager;
        private DiscordRpcClient? _client;
        private string? _currentAppId;
        private MediaTrackInfo? _lastTrack;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private CancellationTokenSource? _coverCts;
        private bool _isDisposed;

        public DiscordRpcService(ConfigManager configManager)
        {
            _configManager = configManager;
            UpdateClientState();
        }

        public void UpdateClientState()
        {
            var config = _configManager.CurrentConfig;
            if (!config.DiscordRpcEnabled)
            {
                ShutdownClient();
                return;
            }

            var targetAppId = GetAppIdForSource(MusicSource.Generic);
            EnsureClient(targetAppId);
        }

        private string GetAppIdForSource(MusicSource source)
        {
            var config = _configManager.CurrentConfig;
            var defaultId = string.IsNullOrWhiteSpace(config.DiscordApplicationId)
                ? "1560410469112750100"
                : config.DiscordApplicationId.Trim();

            return source switch
            {
                MusicSource.YandexMusic => !string.IsNullOrWhiteSpace(config.DiscordYandexMusicAppId) ? config.DiscordYandexMusicAppId.Trim() : defaultId,
                MusicSource.Spotify => !string.IsNullOrWhiteSpace(config.DiscordSpotifyAppId) ? config.DiscordSpotifyAppId.Trim() : defaultId,
                MusicSource.SoundCloud => !string.IsNullOrWhiteSpace(config.DiscordSoundCloudAppId) ? config.DiscordSoundCloudAppId.Trim() : defaultId,
                _ => defaultId
            };
        }

        private void EnsureClient(string appId)
        {
            if (_client != null && _currentAppId == appId)
            {
                return;
            }

            ShutdownClient();

            try
            {
                _currentAppId = appId;
                _client = new DiscordRpcClient(appId);
                _client.Initialize();
                if (_lastTrack != null)
                {
                    UpdatePresence(_lastTrack);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscordRpcService] Failed to initialize client: {ex.Message}");
                _client = null;
            }
        }

        public void OnTrackInfoChanged(MediaTrackInfo trackInfo)
        {
            _lastTrack = trackInfo;
            UpdatePresence(trackInfo);
        }

        public void OnProgressUpdated(TimeSpan position, TimeSpan duration, double progress)
        {
            if (_lastTrack != null)
            {
                _lastTrack.Position = position;
                _lastTrack.Duration = duration;
            }

            // Discord updates smoothly on its own via Timestamps.
            // Only re-calibrate periodically if playing.
            if ((DateTime.UtcNow - _lastUpdateTime).TotalSeconds >= 15 && _lastTrack?.IsPlaying == true)
            {
                UpdatePresence(_lastTrack);
            }
        }

        private void UpdatePresence(MediaTrackInfo track)
        {
            if (!_configManager.CurrentConfig.DiscordRpcEnabled)
            {
                ShutdownClient();
                return;
            }

            if (!track.HasActiveSession || string.IsNullOrWhiteSpace(track.Title) || track.Title == "Нет активного трека")
            {
                try { _client?.ClearPresence(); } catch { }
                return;
            }

            var source = DetectMusicSource(track);
            var targetAppId = GetAppIdForSource(source);

            if (_client == null || _currentAppId != targetAppId)
            {
                EnsureClient(targetAppId);
            }

            if (_client == null || !_client.IsInitialized)
            {
                return;
            }

            _coverCts?.Cancel();
            _coverCts = new CancellationTokenSource();
            var ct = _coverCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    string? coverUrl = await CoverArtService.GetCoverUrlAsync(track.Artist, track.Title, track.ThumbnailBytes, ct);
                    if (ct.IsCancellationRequested) return;

                    SendPresencePayload(track, coverUrl, source);
                }
                catch
                {
                    SendPresencePayload(track, null, source);
                }
            }, ct);
        }

        private void SendPresencePayload(MediaTrackInfo track, string? coverUrl, MusicSource source)
        {
            if (_client == null || !_client.IsInitialized) return;

            try
            {
                var title = Truncate(track.Title, 120);
                var artist = Truncate(track.Artist, 100);

                var stateText = string.IsNullOrWhiteSpace(artist) || artist == "Неизвестный исполнитель"
                    ? "via LucidTune"
                    : $"{artist} • via LucidTune";

                var sourceName = source switch
                {
                    MusicSource.YandexMusic => "Яндекс Музыка",
                    MusicSource.Spotify => "Spotify",
                    MusicSource.SoundCloud => "SoundCloud",
                    _ => "LucidTune"
                };

                var smallImageTooltip = sourceName == "LucidTune" ? "LucidTune" : $"{sourceName} via LucidTune";

                var presence = new RichPresence
                {
                    Type = ActivityType.Listening,
                    Details = title,
                    State = stateText,
                    Assets = new Assets
                    {
                        LargeImageKey = !string.IsNullOrEmpty(coverUrl) ? coverUrl : "logo",
                        LargeImageText = null,
                        SmallImageKey = "logo",
                        SmallImageText = smallImageTooltip
                    },
                    Buttons = new[]
                    {
                        new Button
                        {
                            Label = "Скачать LucidTune",
                            Url = "https://yug33n.github.io/#projects-deck"
                        }
                    }
                };

                if (track.IsPlaying && track.Duration > TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    var startTime = now - track.Position;
                    var endTime = startTime + track.Duration;

                    presence.Timestamps = new Timestamps
                    {
                        Start = startTime,
                        End = endTime
                    };
                }

                _client.SetPresence(presence);
                _lastUpdateTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscordRpcService] SendPresencePayload error: {ex.Message}");
            }
        }

        public static MusicSource DetectMusicSource(MediaTrackInfo track)
        {
            var appId = track.SourceAppUserModelId?.ToLowerInvariant() ?? "";
            if (appId.Contains("yandex") || appId.Contains("яндекс")) return MusicSource.YandexMusic;
            if (appId.Contains("spotify")) return MusicSource.Spotify;
            if (appId.Contains("soundcloud")) return MusicSource.SoundCloud;

            // If audio is playing through a web browser, check window titles of browser processes
            if (appId.Contains("chrome") || appId.Contains("edge") || appId.Contains("firefox") || appId.Contains("opera") || appId.Contains("brave") || appId.Contains("browser"))
            {
                try
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        var pName = p.ProcessName.ToLowerInvariant();
                        if (pName.Contains("chrome") || pName.Contains("msedge") || pName.Contains("firefox") || pName.Contains("opera") || pName.Contains("brave"))
                        {
                            var title = p.MainWindowTitle?.ToLowerInvariant() ?? "";
                            if (title.Contains("яндекс музыка") || title.Contains("yandex music")) return MusicSource.YandexMusic;
                            if (title.Contains("soundcloud")) return MusicSource.SoundCloud;
                            if (title.Contains("spotify")) return MusicSource.Spotify;
                        }
                    }
                }
                catch { }
            }

            return MusicSource.Generic;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 1) + "…";
        }

        private void ShutdownClient()
        {
            try
            {
                _coverCts?.Cancel();
                _coverCts?.Dispose();
                _coverCts = null;

                if (_client != null)
                {
                    _client.ClearPresence();
                    _client.Dispose();
                    _client = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            ShutdownClient();
        }
    }
}
