using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LucidTune.Services
{
    public class CoverArtService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

        static CoverArtService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static async Task<string?> GetCoverUrlAsync(string artist, string title, byte[]? thumbnailBytes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title) || title == "Нет активного трека")
            {
                return null;
            }

            var cleanArtist = string.IsNullOrWhiteSpace(artist) || artist == "Неизвестный исполнитель" ? "" : artist.Trim();
            var cleanTitle = title.Trim();
            var cacheKey = $"{cleanArtist}|{cleanTitle}";

            if (Cache.TryGetValue(cacheKey, out var cachedUrl) && !string.IsNullOrEmpty(cachedUrl))
            {
                return cachedUrl;
            }

            // 1. If we have the exact album artwork bytes from the player/local file, upload it directly:
            if (thumbnailBytes != null && thumbnailBytes.Length > 0)
            {
                try
                {
                    var uploaded = await UploadThumbnailAsync(thumbnailBytes, ct);
                    if (!string.IsNullOrEmpty(uploaded))
                    {
                        Cache[cacheKey] = uploaded;
                        return uploaded;
                    }
                }
                catch { }
            }

            var query = string.IsNullOrEmpty(cleanArtist) ? cleanTitle : $"{cleanArtist} {cleanTitle}";
            var encodedQuery = Uri.EscapeDataString(query);

            // 2. Yandex Music Search API
            try
            {
                var ymUrl = $"https://api.music.yandex.net/search?text={encodedQuery}&page=0&type=track";
                using var ymReq = new HttpRequestMessage(HttpMethod.Get, ymUrl);
                ymReq.Headers.TryAddWithoutValidation("User-Agent", "YandexMusic/2023.12 (Android)");
                using var ymResp = await HttpClient.SendAsync(ymReq, ct);
                if (ymResp.IsSuccessStatusCode)
                {
                    var json = await ymResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("tracks", out var tracks) &&
                        tracks.TryGetProperty("results", out var results) &&
                        results.GetArrayLength() > 0)
                    {
                        var first = results[0];
                        if (first.TryGetProperty("coverUri", out var coverUriElement))
                        {
                            var uri = coverUriElement.GetString();
                            if (!string.IsNullOrEmpty(uri))
                            {
                                var fullUrl = "https://" + uri.Replace("%%", "400x400");
                                Cache[cacheKey] = fullUrl;
                                return fullUrl;
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. iTunes / Apple Music Search API
            try
            {
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
            catch { }

            // 4. Deezer Search API
            try
            {
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

            return null;
        }

        private static async Task<string?> UploadThumbnailAsync(byte[] bytes, CancellationToken ct)
        {
            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(bytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(byteContent, "file", "cover.jpg");

            using var resp = await HttpClient.PostAsync("https://tmpfiles.org/api/v1/upload", content, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("url", out var urlElement))
            {
                var rawUrl = urlElement.GetString();
                if (!string.IsNullOrEmpty(rawUrl))
                {
                    // Convert view url https://tmpfiles.org/123/cover.jpg to direct image download url https://tmpfiles.org/dl/123/cover.jpg
                    return rawUrl.Replace("tmpfiles.org/", "tmpfiles.org/dl/");
                }
            }

            return null;
        }
    }
}
