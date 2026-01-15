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
            SchedulePathTextBox.Text = App.Settings.ScheduleFilePath;
            AutoRefreshCheckBox.IsChecked = App.Settings.AutoRefresh;
            RefreshIntervalTextBox.Text = App.Settings.RefreshInterval.ToString();
            ShowKeyboardCheckBox.IsChecked = App.Settings.ShowKeyboardForPassword;
            ReplacementsPathTextBox.Text = App.Settings.ReplacementsFilePath;
            
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
            App.Settings.ScheduleFilePath = SchedulePathTextBox.Text;
            App.Settings.ReplacementsFilePath = ReplacementsPathTextBox.Text;
            App.Settings.AutoRefresh = AutoRefreshCheckBox.IsChecked ?? true;

            if (int.TryParse(RefreshIntervalTextBox.Text, out int interval) && interval > 0)
            {
                App.Settings.RefreshInterval = interval;
            }

            App.Settings.ShowKeyboardForPassword = ShowKeyboardCheckBox.IsChecked ?? true;

            App.SaveSettings();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            this.DialogResult = true;
            this.Close();
        }
    }
}