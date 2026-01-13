using System.Windows;
using System.Windows.Controls;

namespace Kiosk
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow()
        {
            InitializeComponent();
            NewPasswordBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(newPassword))
            {
                ShowError("Пароль не может быть пустым");
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowError("Пароли не совпадают");
                return;
            }

            if (newPassword.Length < 4)
            {
                ShowError("Пароль должен содержать не менее 4 символов");
                return;
            }

            App.Settings.AdminPassword = newPassword;
            App.SaveSettings();

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}