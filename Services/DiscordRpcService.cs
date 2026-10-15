using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using LucidTune.Config;
using LucidTune.Models;

namespace LucidTune.Services
{
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

            var appId = string.IsNullOrWhiteSpace(config.DiscordApplicationId) 
                ? "1560410469112750100" 
                : config.DiscordApplicationId.Trim();

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
                System.Diagnostics.Debug.WriteLine($"[DiscordRpcService] Failed to initialize client: {ex.Message}");
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
            // Only re-send every 15 seconds to keep timestamps calibrated if drifting.
            if ((DateTime.UtcNow - _lastUpdateTime).TotalSeconds >= 15 && _lastTrack?.IsPlaying == true)
            {
                UpdatePresence(_lastTrack);
            }
        }

        private void UpdatePresence(MediaTrackInfo track)
        {
            if (_client == null || !_client.IsInitialized || !_configManager.CurrentConfig.DiscordRpcEnabled)
            {
                return;
            }

            if (!track.HasActiveSession || string.IsNullOrWhiteSpace(track.Title) || track.Title == "Нет активного трека")
            {
                try { _client.ClearPresence(); } catch { }
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

                    SendPresencePayload(track, coverUrl);
                }
                catch
                {
                    SendPresencePayload(track, null);
                }
            }, ct);
        }

        private void SendPresencePayload(MediaTrackInfo track, string? coverUrl)
        {
            if (_client == null || !_client.IsInitialized) return;

            try
            {
                var title = Truncate(track.Title, 120);
                var artist = Truncate(track.Artist, 100);

                var stateText = string.IsNullOrWhiteSpace(artist) || artist == "Неизвестный исполнитель"
                    ? "via LucidTune"
                    : $"{artist} • via LucidTune";

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
                        SmallImageText = "LucidTune"
                    },
                    Buttons = new[]
                    {
                        new Button
                        {
                            Label = "Скачать LucidTune",
                            Url = "https://github.com/yug33n/LucidTune"
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
                System.Diagnostics.Debug.WriteLine($"[DiscordRpcService] SendPresencePayload error: {ex.Message}");
            }
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
