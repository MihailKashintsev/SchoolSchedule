using System;
using System.IO;
using System.Windows;

namespace Kiosk
{
    public class Program
    {
        // Папка где приложение хранит данные (settings.json, crash.log)
        // C:\Users\<user>\AppData\Roaming\SchoolKiosk\
        public static readonly string DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolKiosk");

        [STAThread]
        public static void Main(string[] args)
        {
            // Создаём папку для данных если нет
            Directory.CreateDirectory(DataFolder);

            var logPath = Path.Combine(DataFolder, "crash.log");

            try
            {
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting...\n");

                var app = new App();
                app.InitializeComponent();

                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App initialized, running...\n");

                app.Run();
            }
            catch (Exception ex)
            {
                var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH\n" +
                          $"{ex.GetType().FullName}: {ex.Message}\n" +
                          $"{ex.StackTrace}\n" +
                          $"Inner: {ex.InnerException?.GetType().FullName}: {ex.InnerException?.Message}\n" +
                          $"Inner stack: {ex.InnerException?.StackTrace}\n" +
                          new string('-', 60) + "\n";
                File.AppendAllText(logPath, msg);
                MessageBox.Show(
                    $"Ошибка запуска:\n\n{ex.Message}\n\nПодробности:\n{logPath}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
