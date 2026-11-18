using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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

            // If configured to ignore browser videos (YouTube, Twitch, etc.), clear Discord presence
            if (_configManager.CurrentConfig.DiscordIgnoreBrowserVideos && IsBrowserVideo(track))
            {
                try { _client?.ClearPresence(); } catch { }
                return;
            }

            // If configured to ignore Telegram voice messages / audio notes, clear Discord presence
            if (_configManager.CurrentConfig.DiscordIgnoreTelegramVoice && IsTelegramVoiceMessage(track))
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
                            Label = "View App on Website",
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static string? _lastIdentifiedVideoKey;

        public static bool IsBrowserApp(string? sourceAppUserModelId)
        {
            if (string.IsNullOrWhiteSpace(sourceAppUserModelId)) return false;
            var id = sourceAppUserModelId.ToLowerInvariant();
            return id.Contains("chrome") ||
                   id.Contains("edge") ||
                   id.Contains("msedge") ||
                   id.Contains("firefox") ||
                   id.Contains("opera") ||
                   id.Contains("brave") ||
                   id.Contains("vivaldi") ||
                   id.Contains("browser") ||
                   id.Contains("arc") ||
                   (id.Contains("yandex") && !id.Contains("yandexmusic") && !id.Contains("music"));
        }

        private static bool IsBrowserProcessName(string? pName)
        {
            if (string.IsNullOrWhiteSpace(pName)) return false;
            var name = pName.ToLowerInvariant();
            return name.Contains("chrome") ||
                   name.Contains("msedge") ||
                   name.Contains("edge") ||
                   name.Contains("firefox") ||
                   name.Contains("opera") ||
                   name.Contains("brave") ||
                   name.Contains("vivaldi") ||
                   name.Contains("browser") ||
                   name.Contains("arc") ||
                   name.Contains("yandex");
        }

        private static List<string> GetBrowserWindowTitles()
        {
            var titles = new List<string>();
            var browserPids = new HashSet<uint>();

            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (IsBrowserProcessName(p.ProcessName))
                        {
                            browserPids.Add((uint)p.Id);
                            var mainTitle = p.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(mainTitle))
                            {
                                titles.Add(mainTitle);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch { }

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (IsWindowVisible(hWnd))
                        {
                            GetWindowThreadProcessId(hWnd, out var pid);
                            if (pid > 0 && browserPids.Contains(pid))
                            {
                                var sb = new StringBuilder(512);
                                if (GetWindowText(hWnd, sb, sb.Capacity) > 0)
                                {
                                    var text = sb.ToString();
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        titles.Add(text);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            return titles;
        }

        public static bool IsBrowserVideo(MediaTrackInfo track)
        {
            if (!IsBrowserApp(track.SourceAppUserModelId))
            {
                return false;
            }

            var currentTrackKey = $"{track.SourceAppUserModelId}:{track.Title}:{track.Artist}";

            // 1. If SMTC explicitly declared this media as Video:
            if (string.Equals(track.PlaybackType, "Video", StringComparison.OrdinalIgnoreCase))
            {
                _lastIdentifiedVideoKey = currentTrackKey;
                return true;
            }

            var lowerTitle = track.Title?.ToLowerInvariant() ?? "";
            var lowerArtist = track.Artist?.ToLowerInvariant() ?? "";

            // 2. Explicit video indicators in track metadata:
            if (lowerArtist == "youtube" || lowerArtist == "twitch" || lowerArtist == "rutube" ||
                lowerArtist.Contains("vk видео") || lowerArtist.Contains("vk video"))
            {
                _lastIdentifiedVideoKey = currentTrackKey;
                return true;
            }

            if (lowerTitle.Contains("youtube.com") || lowerTitle.Contains("youtu.be") || lowerTitle.Contains("twitch.tv"))
            {
                _lastIdentifiedVideoKey = currentTrackKey;
                return true;
            }

            // Check if track title itself contains YouTube indicators (e.g. "Video Title — YouTube", "Video - YouTube", "Video | YouTube"):
            if ((lowerTitle.Contains("— youtube") || lowerTitle.Contains("– youtube") ||
                 lowerTitle.Contains("- youtube") || lowerTitle.Contains("| youtube") ||
                 lowerTitle.Contains("[youtube]") || lowerTitle.Contains("(youtube)")) &&
                !lowerTitle.Contains("youtube music"))
            {
                _lastIdentifiedVideoKey = currentTrackKey;
                return true;
            }

            // 3. Scan browser window titles (active tabs across all browser windows)
            try
            {
                var titles = GetBrowserWindowTitles();

                bool hasMusicService = false;
                bool hasVideoService = false;

                foreach (var rawTitle in titles)
                {
                    var t = rawTitle.ToLowerInvariant();

                    // Dedicated music web players are never treated as videos:
                    if (t.Contains("youtube music") ||
                        t.Contains("яндекс музыка") ||
                        t.Contains("yandex music") ||
                        t.Contains("spotify") ||
                        t.Contains("soundcloud") ||
                        t.Contains("deezer") ||
                        t.Contains("apple music") ||
                        t.Contains("tidal") ||
                        t.Contains("vk музыка") ||
                        t.Contains("музыка вконтакте") ||
                        t.Contains("bandcamp"))
                    {
                        hasMusicService = true;
                    }

                    // Video platforms in browser (handling em-dash —, en-dash –, hyphen -, pipe |, and raw keywords):
                    if ((t.Contains("youtube") && !t.Contains("youtube music")) ||
                        t.Contains("youtu.be") ||
                        t.Contains("twitch") ||
                        t.Contains("кинопоиск") ||
                        t.Contains("kinopoisk") ||
                        t.Contains("rutube") ||
                        t.Contains("netflix") ||
                        t.Contains("vk видео") ||
                        t.Contains("vk video") ||
                        t.Contains("vimeo") ||
                        t.Contains("dailymotion") ||
                        t.Contains("coub") ||
                        t.Contains("tiktok") ||
                        t.Contains("jut.su") ||
                        t.Contains("anilibria") ||
                        t.Contains("shikimori") ||
                        t.Contains("anime") ||
                        t.Contains("hdrezka") ||
                        t.Contains("filmix") ||
                        t.Contains("lordfilm") ||
                        t.Contains("kinogo") ||
                        t.Contains("kick.com") ||
                        t.Contains("bilibili"))
                    {
                        hasVideoService = true;
                    }
                }

                if (hasVideoService && !hasMusicService)
                {
                    _lastIdentifiedVideoKey = currentTrackKey;
                    return true;
                }

                if (hasVideoService && hasMusicService)
                {
                    // If video window title contains the current track's title, it's definitely the video
                    if (!string.IsNullOrWhiteSpace(lowerTitle))
                    {
                        foreach (var rawTitle in titles)
                        {
                            var t = rawTitle.ToLowerInvariant();
                            if ((t.Contains("youtube") && !t.Contains("youtube music")) || t.Contains("twitch"))
                            {
                                if (t.Contains(lowerTitle))
                                {
                                    _lastIdentifiedVideoKey = currentTrackKey;
                                    return true;
                                }
                            }
                        }
                    }

                    _lastIdentifiedVideoKey = currentTrackKey;
                    return true;
                }

                if (hasMusicService)
                {
                    _lastIdentifiedVideoKey = null;
                    return false;
                }
            }
            catch { }

            // 4. If this track was already identified as a video during this playback session (e.g. user switched to another tab):
            if (!string.IsNullOrEmpty(_lastIdentifiedVideoKey) && _lastIdentifiedVideoKey == currentTrackKey)
            {
                return true;
            }

            return false;
        }

        public static bool IsTelegramVoiceMessage(MediaTrackInfo track)
        {
            var appId = track.SourceAppUserModelId?.ToLowerInvariant() ?? "";
            if (!appId.Contains("telegram"))
            {
                return false;
            }

            var title = track.Title?.ToLowerInvariant() ?? "";
            var artist = track.Artist?.ToLowerInvariant() ?? "";

            // Explicit voice/video note terms (Russian and English):
            if (title.Contains("голосовое сообщение") || title.Contains("voice message") ||
                title.Contains("видеосообщение") || title.Contains("video message") ||
                title.Contains("аудиосообщение") || title.Contains("audio message") ||
                artist.Contains("голосовое сообщение") || artist.Contains("voice message"))
            {
                return true;
            }

            // Audio file formats and prefixes typical for Telegram voice notes:
            if (title.EndsWith(".ogg") || title.EndsWith(".oga") || title.EndsWith(".opus") ||
                title.StartsWith("audio_") || title.StartsWith("voice_") ||
                title.StartsWith("audio ") || title.StartsWith("voice "))
            {
                return true;
            }

            // If Telegram reports no thumbnail and title is generic chat audio:
            if ((artist == "telegram" || string.IsNullOrWhiteSpace(artist)) &&
                track.Thumbnail == null && !title.Contains(" - ") && !title.EndsWith(".mp3") && !title.EndsWith(".flac"))
            {
                return true;
            }

            return false;
        }

        public static MusicSource DetectMusicSource(MediaTrackInfo track)
        {
            var appId = track.SourceAppUserModelId?.ToLowerInvariant() ?? "";
            if (appId.Contains("yandex") || appId.Contains("яндекс")) return MusicSource.YandexMusic;
            if (appId.Contains("spotify")) return MusicSource.Spotify;
            if (appId.Contains("soundcloud")) return MusicSource.SoundCloud;

            // If audio is playing through a web browser, check window titles of browser processes
            if (IsBrowserApp(appId))
            {
                try
                {
                    var titles = GetBrowserWindowTitles();
                    foreach (var rawTitle in titles)
                    {
                        var title = rawTitle.ToLowerInvariant();
                        if (title.Contains("яндекс музыка") || title.Contains("yandex music")) return MusicSource.YandexMusic;
                        if (title.Contains("soundcloud")) return MusicSource.SoundCloud;
                        if (title.Contains("spotify")) return MusicSource.Spotify;
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
