using Kiosk.Services;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Kiosk
{
    public partial class App : Application
    {
        public static Settings Settings { get; private set; }

        /// <summary>Версия из AssemblyInformationalVersion (проставляется при релизе)</summary>
        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                return string.IsNullOrWhiteSpace(v) ? "1.0.0" : v;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Данные приложения — в AppData, а не рядом с exe
            Directory.CreateDirectory(Program.DataFolder);

            LoadSettings();

            // Автообновление
            _ = AutoUpdateService.CheckForUpdatesAsync(silent: true);
        }

        private void LoadSettings()
        {
            string settingsPath = Path.Combine(Program.DataFolder, "settings.json");

            // Совместимость: если старый файл рядом с exe — переносим
            var legacyPath = "settings.json";
            if (!File.Exists(settingsPath) && File.Exists(legacyPath))
            {
                File.Copy(legacyPath, settingsPath);
            }

            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    Settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
                }
                catch
                {
                    Settings = new Settings();
                }
            }
            else
            {
                Settings = new Settings();
            }
        }

        public static void SaveSettings()
        {
            string settingsPath = Path.Combine(Program.DataFolder, "settings.json");
            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(settingsPath, json);
        }
    }

    public class Settings
    {
        // Основные
        public string ScheduleFilePath { get; set; } = "schedule.json";
        public string MapUrl { get; set; } = "https://example.com/map";
        public string NewsUrl { get; set; } = "https://example.com/news";
        public string ReplacementsFilePath { get; set; } = "replacements.docx";
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 300;
        public bool ShowKeyboardForPassword { get; set; } = true;
        public string AdminPassword { get; set; } = "1234";

        // Названия школы
        public string SchoolFullName { get; set; } = "Название школы";
        public string SchoolShortName { get; set; } = "Школа";

        // Баннеры
        public string BannerImagePaths { get; set; } = "";
        public int BannerTimeout { get; set; } = 30;
        public int BannerSwitchInterval { get; set; } = 5;
        public bool EnableBanners { get; set; } = true;

        // Погода
        public bool WeatherEnabled { get; set; } = true;
        public string WeatherCity { get; set; } = "";
        public double? WeatherLat { get; set; } = null;
        public double? WeatherLon { get; set; } = null;

        // Новости — белый список URL
        public string AllowedNewsUrls { get; set; } = "";

        // GigaChat API
        public string GigaChatApiKey { get; set; } = "";
    }
}