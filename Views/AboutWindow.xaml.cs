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

            // Устанавливаем полноэкранный режим
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;

            // Загружаем QR-коды
            LoadQrCodes();

            // Устанавливаем текст о разработчиках
            DeveloperText.Text = @"Проект разработан командой энтузиастов для автоматизации школьного процесса.

Основные возможности:
• Отображение актуального расписания уроков
• Показ ежедневных замен
• Интуитивный интерфейс для сенсорных киосков
• Автоматическое обновление данных
• Интеграция с существующими системами

Наша цель - сделать школьную информацию доступной и понятной для учащихся, учителей и родителей.";
        }

        private void LoadQrCodes()
        {
            try
            {
                // QR-код 1 - Сайт команды
                QrCode1Text.Text = "https://rendergames.tilda.ws/";
                QrCode1Image.Source = LoadImageFromResources("qrcode1.png");

                // QR-код 2 - Сайт школы
                QrCode2Text.Text = "https://ligapervihpheniks.tilda.ws/";
                QrCode2Image.Source = LoadImageFromResources("qrcode2.png");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки QR-кодов: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private BitmapImage LoadImageFromResources(string filename)
        {
            try
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri($"pack://application:,,,/Images/{filename}");
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
            catch (Exception ex)
            {
                // Если файл не найден, создаем placeholder
                MessageBox.Show($"Не удалось загрузить изображение {filename}: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return CreatePlaceholderImage();
            }
        }

        private BitmapImage CreatePlaceholderImage()
        {
            // Создаем простой placeholder если изображения не найдены
            var width = 150;
            var height = 150;

            var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

            var visual = new System.Windows.Media.DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Фон
                context.DrawRectangle(
                    System.Windows.Media.Brushes.White,
                    new System.Windows.Media.Pen(System.Windows.Media.Brushes.LightGray, 1),
                    new System.Windows.Rect(0, 0, width, height));

                // Текст
                var text = new System.Windows.Media.FormattedText(
                    "QR Code\nNot Found",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new System.Windows.Media.Typeface("Arial"),
                    12,
                    System.Windows.Media.Brushes.Gray,
                    1.0);

                context.DrawText(text, new System.Windows.Point(10, height / 2 - 10));
            }

            renderTarget.Render(visual);
            return ConvertRenderTargetToBitmapImage(renderTarget);
        }

        private BitmapImage ConvertRenderTargetToBitmapImage(RenderTargetBitmap renderTarget)
        {
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTarget));

            using (var stream = new System.IO.MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private void QrCodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                try
                {
                    Clipboard.SetText(textBlock.Text);

                    // Показываем сообщение об успешном копировании
                    var tooltip = new ToolTip
                    {
                        Content = "Ссылка скопирована в буфер обмена!",
                        Background = Brushes.Green,
                        Foreground = Brushes.White,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
                    };

                    textBlock.ToolTip = tooltip;
                    tooltip.IsOpen = true;

                    // Закрываем подсказку через 2 секунды
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, args) =>
                    {
                        tooltip.IsOpen = false;
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось скопировать ссылку: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
            else if (e.Key == Key.Escape)
            {
                BackButton_Click(sender, e);
            }
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