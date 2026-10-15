using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LucidTune.Services
{
    public class CoverArtService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

        static CoverArtService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public static async Task<string?> GetCoverUrlAsync(string artist, string title, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title) || title == "Нет активного трека")
            {
                return null;
            }

            var cleanArtist = string.IsNullOrWhiteSpace(artist) || artist == "Неизвестный исполнитель" ? "" : artist.Trim();
            var cleanTitle = title.Trim();
            var cacheKey = $"{cleanArtist}|{cleanTitle}";

            if (Cache.TryGetValue(cacheKey, out var cachedUrl))
            {
                return cachedUrl;
            }

            var query = string.IsNullOrEmpty(cleanArtist) ? cleanTitle : $"{cleanArtist} {cleanTitle}";
            var encodedQuery = Uri.EscapeDataString(query);

            try
            {
                // 1. Try Deezer Search API (fuzzy matching, accurate album art)
                var deezerUrl = $"https://api.deezer.com/search?q={encodedQuery}&limit=1";
                using var deezerResp = await HttpClient.GetAsync(deezerUrl, ct);
                if (deezerResp.IsSuccessStatusCode)
                {
                    var json = await deezerResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var first = data[0];
                        if (first.TryGetProperty("album", out var album) && album.TryGetProperty("cover_big", out var coverBig))
                        {
                            var url = coverBig.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                Cache[cacheKey] = url;
                                return url;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                // 2. Fallback to iTunes Search API
                var itunesUrl = $"https://itunes.apple.com/search?term={encodedQuery}&entity=song&limit=1";
                using var response = await HttpClient.GetAsync(itunesUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var firstResult = results[0];
                        if (firstResult.TryGetProperty("artworkUrl100", out var artworkElement))
                        {
                            var rawUrl = artworkElement.GetString();
                            if (!string.IsNullOrEmpty(rawUrl))
                            {
                                var highResUrl = rawUrl.Replace("100x100bb", "600x600bb");
                                Cache[cacheKey] = highResUrl;
                                return highResUrl;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to null on network or timeout issues
            }

            Cache[cacheKey] = null;
            return null;
        }
    }
}
