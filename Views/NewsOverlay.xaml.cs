using System.Windows;

namespace Kiosk.Views
{
    public partial class NewsOverlay : Window
    {
        private NewsBrowserWindow _owner;

        public NewsOverlay(NewsBrowserWindow owner)
        {
            InitializeComponent();
            _owner = owner;

            _owner.LocationChanged += (s, e) => UpdatePosition();
            _owner.StateChanged += (s, e) => UpdatePosition();
            Loaded += (s, e) => UpdatePosition();
        }

        private void UpdatePosition()
        {
            Left = _owner.Left + 20;
            Top = _owner.Top + 20;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _owner.Close();
        }
    }
}
