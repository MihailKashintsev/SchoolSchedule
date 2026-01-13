using System.Windows;
using System.Windows.Controls;

namespace Kiosk
{
    public partial class PasswordWindow : Window
    {
        public bool IsPasswordCorrect { get; private set; } = false;

        public PasswordWindow()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            PasswordBox.Password += button.Content.ToString();
            PasswordBox.Focus();
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password.Length > 0)
            {
                PasswordBox.Password = PasswordBox.Password.Substring(0, PasswordBox.Password.Length - 1);
            }
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CheckPassword();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CheckPassword()
        {
            var enteredPassword = PasswordBox.Password;
            var correctPassword = App.Settings.AdminPassword;

            if (enteredPassword == correctPassword)
            {
                IsPasswordCorrect = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                ShowError("Неверный пароль");
                PasswordBox.Password = "";
                PasswordBox.Focus();
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}