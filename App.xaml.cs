using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Kiosk
{
    public partial class App : Application
    {
        public static Settings Settings { get; private set; }

        public static string Version
        {
            get
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var attr = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
                if (attr != null && !string.IsNullOrWhiteSpace(attr.InformationalVersion))
                    return attr.InformationalVersion;
                var v = asm.GetName().Version;
                return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Глобальные обработчики — пишем любую необработанную ошибку в лог
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogError("UnhandledException", ex.ExceptionObject as Exception);

            DispatcherUnhandledException += (s, ex) =>
            {
                LogError("DispatcherUnhandledException", ex.Exception);
                ex.Handled = true; // не даём приложению молча умереть
                MessageBox.Show(
                    $"Ошибка запуска:\n\n{ex.Exception.Message}\n\nПодробности в crash.log рядом с Kiosk.exe",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            };

            base.OnStartup(e);
            LoadSettings();

            // Проверяем обновления в фоне
            _ = Kiosk.Services.AutoUpdateService.CheckForUpdatesAsync(silent: true);
        }

        private static void LogError(string source, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n" +
                           $"{ex?.GetType().FullName}: {ex?.Message}\n" +
                           $"{ex?.StackTrace}\n" +
                           $"Inner: {ex?.InnerException?.Message}\n" +
                           new string('-', 60) + "\n";
                File.AppendAllText(logPath, text);
            }
            catch { }
        }

        private static string SettingsPath =>
            Path.Combine(Program.DataFolder, "settings.json");

        private void LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    Settings = JsonConvert.DeserializeObject<Settings>(json);
                }
                catch { Settings = new Settings(); }
            }
            else { Settings = new Settings(); }
        }

        public static void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
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
