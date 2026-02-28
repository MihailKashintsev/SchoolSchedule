using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace Kiosk.Services
{
    public class AutoUpdateService
    {
        private const string RepoOwner = "MihailKashintsev";
        private const string RepoName  = "SchoolSchedule";
        private const string ApiUrl    = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        private static readonly HttpClient _http = new HttpClient();

        static AutoUpdateService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "SchoolSchedule-AutoUpdater");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public static async Task CheckForUpdatesAsync(bool silent = true)
        {
            try
            {
                var release = await FetchLatestReleaseAsync();
                if (release == null) return;

                var current = GetCurrentVersion();
                var latest  = ParseVersion(release.TagName);

                if (latest == null || latest <= current) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var win = new Kiosk.Views.UpdateWindow(
                        currentVersion: current.ToString(),
                        latestVersion:  release.TagName,
                        releaseNotes:   release.Body ?? "",
                        installerUrl:   GetInstallerUrl(release),
                        zipUrl:         GetZipUrl(release, release.TagName)
                    );
                    win.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show($"Ошибка проверки обновлений:\n{ex.Message}",
                        "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Читаем InformationalVersion — именно его проставляет GitHub Actions
        public static Version GetCurrentVersion()
        {
            var asm = Assembly.GetExecutingAssembly();

            var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoAttr != null && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion))
            {
                var parsed = ParseVersion(infoAttr.InformationalVersion);
                if (parsed != null) return parsed;
            }

            // Fallback
            var v = asm.GetName().Version;
            return new Version(v?.Major ?? 0, v?.Minor ?? 0, v?.Build ?? 0);
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var clean = tag.TrimStart('v').Split('-')[0].Split('+')[0];
            if (clean.Split('.').Length == 2) clean += ".0";
            return Version.TryParse(clean, out var v) ? v : null;
        }

        public static async Task DownloadAndInstallAsync(string url, string fileName,
            IProgress<int> progress = null)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1L;
                var downloaded = 0L;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var file   = File.Create(tempPath))
                {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await file.WriteAsync(buffer, 0, read);
                        downloaded += read;
                        if (total > 0)
                            progress?.Report((int)(downloaded * 100 / total));
                    }
                }
            }

            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = tempPath,
                    Arguments       = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                });
                Application.Current.Shutdown();
            }
            else
            {
                Process.Start("explorer.exe", $"/select,\"{tempPath}\"");
            }
        }

        private static string GetInstallerUrl(GitHubRelease release)
        {
            foreach (var asset in release.Assets ?? Array.Empty<GitHubAsset>())
                if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return asset.BrowserDownloadUrl;
            return null;
        }

        private static string GetZipUrl(GitHubRelease release, string tag)
        {
            foreach (var asset in release.Assets ?? Array.Empty<GitHubAsset>())
                if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return asset.BrowserDownloadUrl;
            return null;
        }

        private class GitHubRelease
        {
            [JsonProperty("tag_name")]   public string TagName { get; set; }
            [JsonProperty("body")]       public string Body    { get; set; }
            [JsonProperty("assets")]     public GitHubAsset[] Assets { get; set; }
            [JsonProperty("html_url")]   public string HtmlUrl { get; set; }
        }

        private class GitHubAsset
        {
            [JsonProperty("name")]                 public string Name               { get; set; }
            [JsonProperty("browser_download_url")] public string BrowserDownloadUrl { get; set; }
            [JsonProperty("size")]                 public long   Size               { get; set; }
        }
    }
}
