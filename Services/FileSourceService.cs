using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kiosk.Services
{
    /// <summary>
    /// Универсальный сервис загрузки файлов.
    /// Поддерживает локальные пути и HTTP/HTTPS ссылки.
    /// Примеры ссылок:
    ///   Google Drive:  https://drive.google.com/uc?export=download&id=FILE_ID
    ///   Dropbox:       https://dl.dropboxusercontent.com/s/xxx/file.json
    ///   OneDrive:      https://onedrive.live.com/download?...
    ///   Любой HTTP:    https://example.com/schedule.json
    /// </summary>
    public static class FileSourceService
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly string _cacheDir;

        static FileSourceService()
        {
            _http.Timeout = TimeSpan.FromSeconds(30);
            _http.DefaultRequestHeaders.Add("User-Agent", "SchoolKiosk/1.0");

            // Кеш скачанных файлов в AppData
            _cacheDir = Path.Combine(Program.DataFolder, "cache");
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Возвращает локальный путь к файлу.
        /// Если source — URL, скачивает файл в кеш и возвращает путь к кешу.
        /// Если source — локальный путь, возвращает его напрямую.
        /// </summary>
        public static async Task<string> GetLocalPathAsync(string source, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            if (IsUrl(source))
                return await DownloadToCache(source, forceRefresh);

            return source; // локальный путь
        }

        /// <summary>
        /// Проверяет является ли строка URL
        /// </summary>
        public static bool IsUrl(string source)
        {
            return source != null &&
                   (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Скачивает файл по URL в локальный кеш.
        /// Возвращает путь к кешированному файлу.
        /// </summary>
        private static async Task<string> DownloadToCache(string url, bool forceRefresh)
        {
            // Преобразуем Google Drive ссылку вида /file/d/ID/view в прямую ссылку скачивания
            url = ConvertToDirectDownloadUrl(url);

            // Имя файла в кеше — хеш URL + оригинальное расширение
            var ext = GetExtensionFromUrl(url);
            var hash = Math.Abs(url.GetHashCode()).ToString();
            var cachePath = Path.Combine(_cacheDir, $"{hash}{ext}");

            // Если файл уже есть и свежий (< 15 мин) — не перекачиваем
            if (!forceRefresh && File.Exists(cachePath))
            {
                var age = DateTime.Now - File.GetLastWriteTime(cachePath);
                if (age.TotalMinutes < 15)
                    return cachePath;
            }

            // Скачиваем
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(cachePath, bytes);
            return cachePath;
        }

        /// <summary>
        /// Преобразует любые Google Drive/Docs ссылки в прямую ссылку скачивания файла
        /// </summary>
        public static string ConvertToDirectDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;

            // https://drive.google.com/file/d/FILE_ID/view → прямая ссылка
            var match = System.Text.RegularExpressions.Regex.Match(url,
                @"drive\.google\.com/file/d/([^/\?]+)");
            if (match.Success)
            {
                var fileId = match.Groups[1].Value;
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }

            // https://docs.google.com/document/d/FILE_ID/edit → скачать как docx
            // Это случай когда .docx загружен и открывается через Google Docs viewer
            match = System.Text.RegularExpressions.Regex.Match(url,
                @"docs\.google\.com/document/d/([^/\?]+)");
            if (match.Success)
            {
                var fileId = match.Groups[1].Value;
                return $"https://docs.google.com/document/d/{fileId}/export?format=docx";
            }

            // https://docs.google.com/spreadsheets/d/ID/... → экспорт в xlsx
            match = System.Text.RegularExpressions.Regex.Match(url,
                @"docs\.google\.com/spreadsheets/d/([^/\?]+)");
            if (match.Success)
            {
                var fileId = match.Groups[1].Value;
                return $"https://docs.google.com/spreadsheets/d/{fileId}/export?format=xlsx";
            }

            // https://drive.google.com/open?id=FILE_ID
            match = System.Text.RegularExpressions.Regex.Match(url,
                @"drive\.google\.com/open\?id=([^&]+)");
            if (match.Success)
            {
                var fileId = match.Groups[1].Value;
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }

            return url;
        }

        private static string GetExtensionFromUrl(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var ext = Path.GetExtension(path);
                if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
                    return ext;
            }
            catch { }
            return ".tmp";
        }

        /// <summary>
        /// Принудительно обновить кеш для указанного URL
        /// </summary>
        public static Task<string> RefreshAsync(string source)
            => GetLocalPathAsync(source, forceRefresh: true);
    }
}
