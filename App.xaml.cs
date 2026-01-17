using System.Windows;

namespace Kiosk
{
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; } = new AppSettings();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Settings = AppSettings.Load();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Save();
            base.OnExit(e);
        }

        // Добавьте этот метод
        public static void SaveSettings()
        {
            Settings.Save();
        }
    }

    public class AppSettings
    {
        public string ScheduleFilePath { get; set; } = "schedule.json";
        public string ReplacementsFilePath { get; set; } = "replacements.docx"; // НОВОЕ ПОЛЕ
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 300;
        public string AdminPassword { get; set; } = "1234";
        public bool ShowKeyboardForPassword { get; set; } = true;
        public string MapUrl { get; set; } = "http://ligapervihpheniks.tilda.ws/secretmapforkiosk1";
        public string NewsUrl { get; set; } = "https://vk.com/school_liga_khimki";
        public int IdleTimeBeforeBanner { get; set; } = 30; // секунды

        private static readonly string SettingsPath = "kiosk_settings.json";

        public static AppSettings Load()
        {
            try
            {
                if (System.IO.File.Exists(SettingsPath))
                {
                    string json = System.IO.File.ReadAllText(SettingsPath);
                    return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Ignore errors and return default settings
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(SettingsPath, json);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}