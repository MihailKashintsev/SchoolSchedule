using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Kiosk
{
    public partial class App : Application
    {
        public static Settings Settings { get; private set; }

        /// <summary>Текущая версия приложения из сборки (Major.Minor.Build)</summary>
        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoadSettings();
        }

        private void LoadSettings()
        {
            string settingsPath = "settings.json";
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    Settings = JsonConvert.DeserializeObject<Settings>(json);
                }
                catch { Settings = new Settings(); }
            }
            else { Settings = new Settings(); }
        }

        public static void SaveSettings()
        {
            string settingsPath = "settings.json";
            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(settingsPath, json);
        }
    }

    public class Settings
    {
        public string ScheduleFilePath { get; set; } = "schedule.json";
        public string MapUrl { get; set; } = "https://example.com/map";
        public string NewsUrl { get; set; } = "https://example.com/news";
        public string ReplacementsFilePath { get; set; } = "replacements.docx";
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 300;
        public bool ShowKeyboardForPassword { get; set; } = true;
        public string AdminPassword { get; set; } = "1234";

        // Название школы
        public string SchoolFullName { get; set; } = "Муниципальное общеобразовательное учреждение";
        public string SchoolShortName { get; set; } = "МОУ";

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
    }
}
