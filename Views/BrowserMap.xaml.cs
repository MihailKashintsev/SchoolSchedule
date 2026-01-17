using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Threading.Tasks;

namespace Kiosk.Views
{
    public partial class BrowserMap : Window
    {
        private const string DefaultNewsUrl = "http://ligapervihpheniks.tilda.ws/secretmapforkiosk1";
        private DispatcherTimer _resetTimer;
        private bool _isButtonAdded = false;

        public BrowserMap()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Инициализируем WebView2
                await webView.EnsureCoreWebView2Async(null);

                // Включаем поддержку сообщений из JavaScript
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                // Подписываемся на события
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.NavigationStarting += WebView_NavigationStarting;

                // Загружаем начальную страницу
                webView.Source = new System.Uri(DefaultNewsUrl);

                // Настраиваем таймер сброса
                InitializeResetTimer();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddBackButtonWithJavaScript()
        {
            try
            {
                // Скрипт для добавления кнопки
                string script = @"
                    // Удаляем старую кнопку, если она есть
                    var existingButton = document.getElementById('wpfBackButton');
                    if (existingButton) {
                        existingButton.remove();
                    }

                    // Создаем кнопку
                    var backButton = document.createElement('button');
                    backButton.id = 'wpfBackButton';
                    backButton.innerHTML = 'НАЗАД';
                    backButton.style.position = 'fixed';
                    backButton.style.top = '20px';
                    backButton.style.left = '20px';
                    backButton.style.zIndex = '9999';
                    backButton.style.padding = '12px 24px';
                    backButton.style.backgroundColor = '#2196F3';
                    backButton.style.color = 'white';
                    backButton.style.border = 'none';
                    backButton.style.borderRadius = '5px';
                    backButton.style.cursor = 'pointer';
                    backButton.style.fontSize = '16px';
                    backButton.style.fontWeight = 'bold';
                    backButton.style.boxShadow = '0 2px 10px rgba(0,0,0,0.3)';
                    backButton.style.transition = 'all 0.3s ease';
                    
                    // Эффекты при наведении
                    backButton.onmouseover = function() {
                        this.style.backgroundColor = '#1976D2';
                        this.style.transform = 'scale(1.05)';
                    };
                    backButton.onmouseout = function() {
                        this.style.backgroundColor = '#2196F3';
                        this.style.transform = 'scale(1)';
                    };
                    
                    // Добавляем кнопку на страницу
                    document.body.appendChild(backButton);
                    
                    // Обработчик клика
                    backButton.addEventListener('click', function() {
                        // Отправляем сообщение в C# для закрытия окна
                        window.chrome.webview.postMessage('closeWindow');
                    });

                    // Гарантируем, что кнопка всегда будет видна
                    setInterval(function() {
                        if (backButton.parentNode !== document.body) {
                            document.body.appendChild(backButton);
                        }
                        backButton.style.zIndex = '9999';
                    }, 1000);
                ";

                await webView.ExecuteScriptAsync(script);
            }
            catch (System.Exception ex)
            {
                // Если не удалось добавить кнопку, попробуем еще раз через секунду
                await Task.Delay(1000);
                await AddBackButtonWithJavaScript();
            }
        }


        private void InitializeResetTimer()
        {
            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = System.TimeSpan.FromMinutes(5);
            _resetTimer.Tick += async (sender, e) =>
            {
                await ResetToDefaultPage();
            };
            _resetTimer.Start();
        }

        private async Task ResetToDefaultPage()
        {
            // Принудительно возвращаем на нужную страницу
            if (webView != null && webView.CoreWebView2 != null)
            {
                _isButtonAdded = false;
                webView.CoreWebView2.Navigate(DefaultNewsUrl);
            }
        }

        private void WebView_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            // Сбрасываем флаг при начале навигации
            _isButtonAdded = false;
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            // Ждем немного, чтобы страница полностью загрузилась
            await Task.Delay(500);

            // Добавляем кнопку "Назад" на страницу через JavaScript
            if (!_isButtonAdded)
            {
                await AddBackButtonWithJavaScript();
                _isButtonAdded = true;
            }
        }

        

        private void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message == "closeWindow")
            {
                // Закрываем окно при получении сообщения из JavaScript
                this.Close();
            }
        }

        // Добавляем возможность закрытия окна по Escape
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            base.OnKeyDown(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _resetTimer?.Stop();
            base.OnClosing(e);
        }
    }
}