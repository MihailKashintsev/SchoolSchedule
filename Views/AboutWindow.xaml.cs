using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kiosk.Views
{
    public partial class AboutWindow : Window
    {
        private bool _isFullScreen = true;

        public AboutWindow()
        {
            InitializeComponent();

            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;

            // Версия в статусбаре
            if (VersionText != null)
                VersionText.Text = $"Версия {App.Version}";

            LoadQrCodes();

            DeveloperText.Text =
                "Проект разработан командой энтузиастов для автоматизации школьного процесса.\r\n\r\n" +
                "Основные возможности:\r\n" +
                "• Отображение актуального расписания уроков\r\n" +
                "• Показ ежедневных замен\r\n" +
                "• Виджет погоды в реальном времени\r\n" +
                "• Интуитивный интерфейс для сенсорных киосков\r\n" +
                "• Автоматическое обновление приложения\r\n" +
                "• Интерактивная карта здания\r\n\r\n" +
                "Наша цель — сделать школьную информацию доступной и понятной для учащихся, учителей и родителей.";
        }

        private void LoadQrCodes()
        {
            try
            {
                QrCode1Text.Text = "https://rendergames.tilda.ws/";
                QrCode1Image.Source = LoadImage("qrcode1.png");

                QrCode2Text.Text = "https://ligapervihpheniks.tilda.ws/";
                QrCode2Image.Source = LoadImage("qrcode2.png");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки QR-кодов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private BitmapImage LoadImage(string filename)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri($"pack://application:,,,/Images/{filename}");
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                return img;
            }
            catch
            {
                return CreatePlaceholder();
            }
        }

        private BitmapImage CreatePlaceholder()
        {
            var rt = new RenderTargetBitmap(150, 150, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.White,
                    new Pen(Brushes.LightGray, 1), new Rect(0, 0, 150, 150));
                ctx.DrawText(
                    new FormattedText("QR\nNot Found",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"), 14, Brushes.Gray, 1.0),
                    new Point(20, 55));
            }
            rt.Render(dv);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rt));
            using var ms = new System.IO.MemoryStream();
            enc.Save(ms);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            return bmp;
        }

        private void QrCodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBlock tb) return;
            try
            {
                Clipboard.SetText(tb.Text);
                var tip = new ToolTip
                {
                    Content = "Ссылка скопирована!",
                    Background = Brushes.Green,
                    Foreground = Brushes.White,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                };
                tb.ToolTip = tip;
                tip.IsOpen = true;
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (s, _) => { tip.IsOpen = false; t.Stop(); };
                t.Start();
            }
            catch { }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            new MainWindow().Show();
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11) ToggleFullScreen();
            else if (e.Key == Key.Escape) BackButton_Click(sender, e);
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                _isFullScreen = false;
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                _isFullScreen = true;
            }
        }
    }
}
