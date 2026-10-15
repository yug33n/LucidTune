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
            Timeout = TimeSpan.FromSeconds(5)
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

            // 1. If we have the exact album artwork bytes from the player or local file, upload it directly
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
            var sanitizedTitle = CleanTrackTitle(cleanTitle);
            var sanitizedQuery = string.IsNullOrEmpty(cleanArtist) ? sanitizedTitle : $"{cleanArtist} {sanitizedTitle}";

            // 2. Yandex Music Search API
            var ymCover = await SearchYandexMusicAsync(query, ct) ?? (sanitizedQuery != query ? await SearchYandexMusicAsync(sanitizedQuery, ct) : null);
            if (!string.IsNullOrEmpty(ymCover))
            {
                Cache[cacheKey] = ymCover;
                return ymCover;
            }

            // 3. Deezer Search API (Covers 100% of Spotify & international releases in 1000x1000 HD)
            var deezerCover = await SearchDeezerAsync(query, ct) ?? (sanitizedQuery != query ? await SearchDeezerAsync(sanitizedQuery, ct) : null);
            if (!string.IsNullOrEmpty(deezerCover))
            {
                Cache[cacheKey] = deezerCover;
                return deezerCover;
            }

            // 4. iTunes / Apple Music Search API
            var itunesCover = await SearchItunesAsync(query, ct) ?? (sanitizedQuery != query ? await SearchItunesAsync(sanitizedQuery, ct) : null);
            if (!string.IsNullOrEmpty(itunesCover))
            {
                Cache[cacheKey] = itunesCover;
                return itunesCover;
            }

            // 5. Genius Search API (Great for SoundCloud tracks, remixes, underground and Russian rap)
            var geniusCover = await SearchGeniusAsync(query, ct) ?? (sanitizedQuery != query ? await SearchGeniusAsync(sanitizedQuery, ct) : null);
            if (!string.IsNullOrEmpty(geniusCover))
            {
                Cache[cacheKey] = geniusCover;
                return geniusCover;
            }

            return null;
        }

        private static async Task<string?> SearchYandexMusicAsync(string query, CancellationToken ct)
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
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
                        string? uri = null;
                        if (first.TryGetProperty("coverUri", out var coverUriElement))
                        {
                            uri = coverUriElement.GetString();
                        }

                        if (string.IsNullOrEmpty(uri) && first.TryGetProperty("albums", out var albums) && albums.GetArrayLength() > 0)
                        {
                            var album = albums[0];
                            if (album.TryGetProperty("coverUri", out var albumCover))
                            {
                                uri = albumCover.GetString();
                            }
                        }

                        if (string.IsNullOrEmpty(uri) && first.TryGetProperty("ogImage", out var ogImage))
                        {
                            uri = ogImage.GetString();
                        }

                        if (!string.IsNullOrEmpty(uri))
                        {
                            return "https://" + uri.Replace("%%", "400x400");
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<string?> SearchDeezerAsync(string query, CancellationToken ct)
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
                var deezerUrl = $"https://api.deezer.com/search?q={encodedQuery}&limit=1";
                using var deezerResp = await HttpClient.GetAsync(deezerUrl, ct);
                if (deezerResp.IsSuccessStatusCode)
                {
                    var json = await deezerResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var first = data[0];
                        if (first.TryGetProperty("album", out var album))
                        {
                            if (album.TryGetProperty("cover_xl", out var coverXl))
                            {
                                var url = coverXl.GetString();
                                if (!string.IsNullOrEmpty(url)) return url;
                            }
                            if (album.TryGetProperty("cover_big", out var coverBig))
                            {
                                var url = coverBig.GetString();
                                if (!string.IsNullOrEmpty(url)) return url;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<string?> SearchItunesAsync(string query, CancellationToken ct)
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
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
                                return rawUrl.Replace("100x100bb", "600x600bb");
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static async Task<string?> SearchGeniusAsync(string query, CancellationToken ct)
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
                var geniusUrl = $"https://genius.com/api/search/song?q={encodedQuery}";
                using var req = new HttpRequestMessage(HttpMethod.Get, geniusUrl);
                using var resp = await HttpClient.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("response", out var response) &&
                        response.TryGetProperty("sections", out var sections) &&
                        sections.GetArrayLength() > 0)
                    {
                        foreach (var section in sections.EnumerateArray())
                        {
                            if (section.TryGetProperty("hits", out var hits) && hits.GetArrayLength() > 0)
                            {
                                var firstHit = hits[0];
                                if (firstHit.TryGetProperty("result", out var res))
                                {
                                    if (res.TryGetProperty("song_art_image_url", out var songArt))
                                    {
                                        var url = songArt.GetString();
                                        if (!string.IsNullOrEmpty(url)) return url;
                                    }
                                    if (res.TryGetProperty("header_image_url", out var headerImg))
                                    {
                                        var url = headerImg.GetString();
                                        if (!string.IsNullOrEmpty(url)) return url;
                                    }
                                }
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
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("6d207e02198a847aa98d0a2a901485a5"), "key");
                content.Add(new StringContent("upload"), "action");

                var byteContent = new ByteArrayContent(bytes);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(byteContent, "source", "cover.jpg");

                using var resp = await HttpClient.PostAsync("https://freeimage.host/api/1/upload", content, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("image", out var img) && img.TryGetProperty("url", out var urlProp))
                    {
                        var directUrl = urlProp.GetString();
                        if (!string.IsNullOrEmpty(directUrl) && directUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            return directUrl;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static string CleanTrackTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            var cleaned = Regex.Replace(title, @"\s*[\(\[][^\)\]]*(feat\.|prod\.|remaster|live|deluxe|version|edit|mix)[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*-\s*(feat\.|remaster|live|deluxe|version|edit|single).*", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }
    }
}
