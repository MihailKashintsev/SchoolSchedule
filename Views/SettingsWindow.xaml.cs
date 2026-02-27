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

            UpdateBannerControls();
        }

        private void UpdateBannerControls()
        {
            bool isEnabled = EnableBannersCheckBox.IsChecked ?? false;
            BannerPathsTextBox.IsEnabled = isEnabled;
            BannerTimeoutTextBox.IsEnabled = isEnabled;
            BannerSwitchIntervalTextBox.IsEnabled = isEnabled;
        }

        private void EnableBannersCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateBannerControls();
        }

        private void BrowseBannerPaths_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
                Title = "Выберите файлы баннеров",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BannerPathsTextBox.Text = string.Join(";", openFileDialog.FileNames);
            }
        }

        private void BrowseReplacementsPath_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Word documents (*.docx)|*.docx|All files (*.*)|*.*",
                Title = "Выберите файл замен"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ReplacementsPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var changePasswordWindow = new ChangePasswordWindow();
            if (changePasswordWindow.ShowDialog() == true)
            {
                MessageBox.Show("Пароль успешно изменен", "Успех",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Выберите файл расписания"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SchedulePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
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

            if (int.TryParse(RefreshIntervalTextBox.Text, out int interval) && interval > 0)
            {
                App.Settings.RefreshInterval = interval;
            }

            App.Settings.ShowKeyboardForPassword = ShowKeyboardCheckBox.IsChecked ?? true;

            App.Settings.BannerImagePaths = BannerPathsTextBox.Text;

            if (int.TryParse(BannerTimeoutTextBox.Text, out int timeout) && timeout > 0)
            {
                App.Settings.BannerTimeout = timeout;
            }

            if (int.TryParse(BannerSwitchIntervalTextBox.Text, out int switchInterval) && switchInterval > 0)
            {
                App.Settings.BannerSwitchInterval = switchInterval;
            }

            App.Settings.EnableBanners = EnableBannersCheckBox.IsChecked ?? true;

            App.SaveSettings();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            // Обновляем названия школы на главном экране сразу после сохранения
            if (Owner is MainWindow mainWindow)
                mainWindow.UpdateSchoolNames();

            this.DialogResult = true;
            this.Close();
        }
    }
}
