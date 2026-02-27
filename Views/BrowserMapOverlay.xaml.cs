using System;
using System.Windows;
using System.Windows.Controls;

namespace Kiosk.Views
{
    public partial class BrowserMapOverlay : Window
    {
        private BrowserMap _owner;

        public BrowserMapOverlay(BrowserMap owner)
        {
            InitializeComponent();
            _owner = owner;

            // Привязываем позицию к владельцу
            _owner.LocationChanged += (s, e) => UpdatePosition();
            _owner.StateChanged += (s, e) => UpdatePosition();
            Loaded += (s, e) => UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_owner.WindowState == WindowState.Maximized)
            {
                Left = _owner.Left + 20;
                Top = _owner.Top + 20;
            }
            else
            {
                Left = _owner.Left + 20;
                Top = _owner.Top + 20;
            }
        }

        public void UpdateZoomText(string text)
        {
            ZoomLevelText.Text = text;
            ResetZoomButton.Content = text;
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _owner.ZoomOut();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _owner.ZoomIn();
        }

        private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            _owner.ResetZoom();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _owner.GoBack();
        }
    }
}
