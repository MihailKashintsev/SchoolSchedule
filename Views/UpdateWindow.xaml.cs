using Kiosk.Services;
using System;
using System.Windows;

namespace Kiosk.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly string _installerUrl;
        private readonly string _zipUrl;

        public UpdateWindow(string currentVersion, string latestVersion,
            string releaseNotes, string installerUrl, string zipUrl)
        {
            InitializeComponent();

            _installerUrl = installerUrl;
            _zipUrl       = zipUrl;

            CurrentVersionText.Text  = $"Текущая: v{currentVersion}";
            LatestVersionText.Text   = latestVersion;
            ReleaseNotesText.Text    = string.IsNullOrWhiteSpace(releaseNotes)
                ? "Нет описания для этого обновления."
                : releaseNotes;

            // Если установщика нет — скрываем кнопку
            if (string.IsNullOrEmpty(installerUrl))
                InstallButton.IsEnabled = false;

            if (string.IsNullOrEmpty(zipUrl))
                DownloadZipButton.IsEnabled = false;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDownload(_installerUrl,
                $"SchoolSchedule-Setup-{LatestVersionText.Text}.exe");
        }

        private async void DownloadZipButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDownload(_zipUrl,
                $"SchoolSchedule-{LatestVersionText.Text}.zip");
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async System.Threading.Tasks.Task StartDownload(string url, string fileName)
        {
            InstallButton.IsEnabled      = false;
            DownloadZipButton.IsEnabled  = false;
            SkipButton.IsEnabled         = false;
            ProgressPanel.Visibility     = Visibility.Visible;
            ProgressLabel.Text           = $"Скачивание {fileName}...";

            try
            {
                var progress = new Progress<int>(p =>
                {
                    ProgressBar.Value  = p;
                    ProgressLabel.Text = $"Скачивание... {p}%";
                });

                await AutoUpdateService.DownloadAndInstallAsync(url, fileName, progress);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка скачивания:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                InstallButton.IsEnabled     = !string.IsNullOrEmpty(_installerUrl);
                DownloadZipButton.IsEnabled = !string.IsNullOrEmpty(_zipUrl);
                SkipButton.IsEnabled        = true;
                ProgressPanel.Visibility    = Visibility.Collapsed;
            }
        }
    }
}
