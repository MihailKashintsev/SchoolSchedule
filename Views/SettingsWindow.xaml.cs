using Microsoft.Win32;
using System.Windows;

namespace Kiosk
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            VersionLabel.Text = $"Версия {App.Version}";
        }

        private void LoadSettings()
        {
            SchoolFullNameBox.Text = App.Settings.SchoolFullName;
            SchoolShortNameBox.Text = App.Settings.SchoolShortName;

            SchedulePathTextBox.Text = App.Settings.ScheduleFilePath;
            MapUrl.Text = App.Settings.MapUrl;
            NewsUrl.Text = App.Settings.NewsUrl;
            AutoRefreshCheckBox.IsChecked = App.Settings.AutoRefresh;
            RefreshIntervalTextBox.Text = App.Settings.RefreshInterval.ToString();
            ShowKeyboardCheckBox.IsChecked = App.Settings.ShowKeyboardForPassword;
            ReplacementsPathTextBox.Text = App.Settings.ReplacementsFilePath;

            BannerPathsTextBox.Text = App.Settings.BannerImagePaths;
            BannerTimeoutTextBox.Text = App.Settings.BannerTimeout.ToString();
            BannerSwitchIntervalTextBox.Text = App.Settings.BannerSwitchInterval.ToString();
            EnableBannersCheckBox.IsChecked = App.Settings.EnableBanners;

            // Погода
            WeatherEnabledCheckBox.IsChecked = App.Settings.WeatherEnabled;
            WeatherCityBox.Text = App.Settings.WeatherCity ?? "";
            WeatherLatBox.Text = App.Settings.WeatherLat.HasValue
                ? App.Settings.WeatherLat.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                : "";
            WeatherLonBox.Text = App.Settings.WeatherLon.HasValue
                ? App.Settings.WeatherLon.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                : "";

            UpdateBannerControls();
            UpdateWeatherControls();
        }

        private void UpdateBannerControls()
        {
            bool on = EnableBannersCheckBox.IsChecked ?? false;
            BannerPathsTextBox.IsEnabled = on;
            BannerTimeoutTextBox.IsEnabled = on;
            BannerSwitchIntervalTextBox.IsEnabled = on;
        }

        private void UpdateWeatherControls()
        {
            bool on = WeatherEnabledCheckBox.IsChecked ?? true;
            WeatherCityBox.IsEnabled = on;
            WeatherLatBox.IsEnabled = on;
            WeatherLonBox.IsEnabled = on;
        }

        private void EnableBannersCheckBox_Changed(object sender, RoutedEventArgs e)
            => UpdateBannerControls();

        private void WeatherEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
            => UpdateWeatherControls();

        private void BrowseBannerPaths_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
                Title = "Выберите файлы баннеров",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
                BannerPathsTextBox.Text = string.Join(";", dlg.FileNames);
        }

        private void BrowseReplacementsPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Word documents (*.docx)|*.docx|All files (*.*)|*.*",
                Title = "Выберите файл замен"
            };
            if (dlg.ShowDialog() == true)
                ReplacementsPathTextBox.Text = dlg.FileName;
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var w = new ChangePasswordWindow();
            if (w.ShowDialog() == true)
                MessageBox.Show("Пароль успешно изменён", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Выберите файл расписания"
            };
            if (dlg.ShowDialog() == true)
                SchedulePathTextBox.Text = dlg.FileName;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveSettings()
        {
            if (!string.IsNullOrWhiteSpace(SchoolFullNameBox.Text))
                App.Settings.SchoolFullName = SchoolFullNameBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(SchoolShortNameBox.Text))
                App.Settings.SchoolShortName = SchoolShortNameBox.Text.Trim();

            App.Settings.ScheduleFilePath = SchedulePathTextBox.Text;
            App.Settings.MapUrl = MapUrl.Text;
            App.Settings.NewsUrl = NewsUrl.Text;
            App.Settings.ReplacementsFilePath = ReplacementsPathTextBox.Text;
            App.Settings.AutoRefresh = AutoRefreshCheckBox.IsChecked ?? true;
            App.Settings.ShowKeyboardForPassword = ShowKeyboardCheckBox.IsChecked ?? true;

            if (int.TryParse(RefreshIntervalTextBox.Text, out int interval) && interval > 0)
                App.Settings.RefreshInterval = interval;

            App.Settings.BannerImagePaths = BannerPathsTextBox.Text;
            App.Settings.EnableBanners = EnableBannersCheckBox.IsChecked ?? true;
            if (int.TryParse(BannerTimeoutTextBox.Text, out int timeout) && timeout > 0)
                App.Settings.BannerTimeout = timeout;
            if (int.TryParse(BannerSwitchIntervalTextBox.Text, out int sw) && sw > 0)
                App.Settings.BannerSwitchInterval = sw;

            // Погода
            App.Settings.WeatherEnabled = WeatherEnabledCheckBox.IsChecked ?? true;
            App.Settings.WeatherCity = WeatherCityBox.Text.Trim();

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            App.Settings.WeatherLat = double.TryParse(
                WeatherLatBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any, culture, out double lat) ? lat : (double?)null;
            App.Settings.WeatherLon = double.TryParse(
                WeatherLonBox.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any, culture, out double lon) ? lon : (double?)null;

            App.SaveSettings();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            if (Owner is MainWindow mw)
                mw.UpdateSchoolNames();
            DialogResult = true;
            Close();
        }
    }
}
