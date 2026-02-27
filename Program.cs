using System;
using System.IO;
using System.Windows;

namespace Kiosk
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "crash.log");

            try
            {
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting...\n");

                var app = new App();
                app.InitializeComponent();

                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App initialized, running...\n");

                app.Run();

                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App exited normally.\n");
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
                MessageBox.Show(msg, "Критическая ошибка запуска",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
