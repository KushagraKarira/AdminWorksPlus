using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdminWorks.Services
{
    public class UpdateService
    {
        private const string GitHubRepo = "KushagraKarira/AdminWorks";

        public event Action<string, string>? UpdateAvailable;

        public async Task CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AdminWorks", "1.0"));
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetStringAsync($"https://api.github.com/repos/{GitHubRepo}/releases/latest");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("tag_name", out var tagElement))
                {
                    var latestTag = tagElement.GetString();
                    if (!string.IsNullOrEmpty(latestTag))
                    {
                        var releaseUrl = root.TryGetProperty("html_url", out var urlElement)
                            ? urlElement.GetString() ?? $"https://github.com/{GitHubRepo}/releases"
                            : $"https://github.com/{GitHubRepo}/releases";

                        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                        var currentVersionStr = currentVersion != null ? $"v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}" : "v1.0.0";

                        if (!string.Equals(latestTag.TrimStart('v'), currentVersionStr.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateAvailable?.Invoke(latestTag, releaseUrl);
                        }
                    }
                }
            }
            catch
            {
                // Network or parsing errors during background update checks are handled gracefully
            }
        }
    }
}
