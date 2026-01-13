using System.Windows;

namespace Kiosk
{
    public partial class SimplePasswordDialog : Window
    {
        public bool IsPasswordCorrect { get; private set; }

        public SimplePasswordDialog()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password == App.Settings.AdminPassword)
            {
                IsPasswordCorrect = true;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Неверный пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordBox.Password = "";
                PasswordBox.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}