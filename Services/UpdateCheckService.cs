using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LucidTune.Services
{
    public sealed class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; init; }
        public string LatestVersion { get; init; } = string.Empty;
        public string ReleaseUrl { get; init; } = string.Empty;
        public string ReleaseTitle { get; init; } = string.Empty;
    }

    public static class UpdateCheckService
    {
        public const string CurrentVersion = "2.0.0";
        private const string ReleasesApiUrl = "https://api.github.com/repos/yug33n/LucidTune/releases/latest";
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        static UpdateCheckService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LucidTune-App/" + CurrentVersion);
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                using var response = await HttpClient.GetAsync(ReleasesApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult { IsUpdateAvailable = false };
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? (tagElem.GetString() ?? string.Empty) : string.Empty;
                string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? (urlElem.GetString() ?? string.Empty) : string.Empty;
                string releaseTitle = root.TryGetProperty("name", out var nameElem) ? (nameElem.GetString() ?? string.Empty) : string.Empty;

                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return new UpdateCheckResult { IsUpdateAvailable = false };
                }

                bool isNewer = IsVersionNewer(tagName, CurrentVersion);

                return new UpdateCheckResult
                {
                    IsUpdateAvailable = isNewer,
                    LatestVersion = tagName.TrimStart('v', 'V'),
                    ReleaseUrl = htmlUrl,
                    ReleaseTitle = !string.IsNullOrWhiteSpace(releaseTitle) ? releaseTitle : tagName
                };
            }
            catch
            {
                return new UpdateCheckResult { IsUpdateAvailable = false };
            }
        }

        public static bool IsVersionNewer(string remoteTag, string localVer)
        {
            try
            {
                var remoteParts = ParseVersionParts(remoteTag);
                var localParts = ParseVersionParts(localVer);

                int length = Math.Max(remoteParts.Length, localParts.Length);
                for (int i = 0; i < length; i++)
                {
                    int r = i < remoteParts.Length ? remoteParts[i] : 0;
                    int l = i < localParts.Length ? localParts[i] : 0;

                    if (r > l) return true;
                    if (r < l) return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static int[] ParseVersionParts(string versionStr)
        {
            string clean = versionStr.Trim().TrimStart('v', 'V');
            int dashIndex = clean.IndexOf('-');
            if (dashIndex > 0)
            {
                clean = clean[..dashIndex];
            }

            var tokens = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var parts = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (int.TryParse(tokens[i], out int val))
                {
                    parts[i] = val;
                }
            }
            return parts;
        }
    }
}
