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
    /// <summary>
    /// Сервис автоматического обновления приложения через GitHub Releases.
    /// </summary>
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

        // ── Публичный метод: вызывается при старте приложения ──────────────
        public static async Task CheckForUpdatesAsync(bool silent = true)
        {
            try
            {
                var release = await FetchLatestReleaseAsync();
                if (release == null) return;

                var current = GetCurrentVersion();
                var latest  = ParseVersion(release.TagName);

                if (latest == null || latest <= current) return;

                // Есть новая версия — показываем окно
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

        // ── Получить данные о последнем релизе ─────────────────────────────
        private static async Task<GitHubRelease> FetchLatestReleaseAsync()
        {
            var json = await _http.GetStringAsync(ApiUrl);
            return JsonConvert.DeserializeObject<GitHubRelease>(json);
        }

        // ── Текущая версия из AssemblyInfo ─────────────────────────────────
        public static Version GetCurrentVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return new Version(v.Major, v.Minor, v.Build);
        }

        private static Version ParseVersion(string tag)
        {
            tag = tag?.TrimStart('v') ?? "";
            // Убираем суффикс вроде -beta
            var clean = tag.Split('-')[0];
            return Version.TryParse(clean, out var v) ? v : null;
        }

        // ── Скачать и запустить установщик ─────────────────────────────────
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

            // Если это установщик — запускаем и выходим
            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName  = tempPath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                });
                Application.Current.Shutdown();
            }
            else
            {
                // ZIP — открываем папку с файлом
                Process.Start("explorer.exe", $"/select,\"{tempPath}\"");
            }
        }

        // ── Найти URL установщика в ассетах релиза ─────────────────────────
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

        // ── Модели ответа GitHub API ────────────────────────────────────────
        private class GitHubRelease
        {
            [JsonProperty("tag_name")]    public string TagName  { get; set; }
            [JsonProperty("body")]        public string Body     { get; set; }
            [JsonProperty("assets")]      public GitHubAsset[] Assets { get; set; }
            [JsonProperty("html_url")]    public string HtmlUrl  { get; set; }
        }

        private class GitHubAsset
        {
            [JsonProperty("name")]                  public string Name                { get; set; }
            [JsonProperty("browser_download_url")]  public string BrowserDownloadUrl  { get; set; }
            [JsonProperty("size")]                  public long   Size                { get; set; }
        }
    }
}
