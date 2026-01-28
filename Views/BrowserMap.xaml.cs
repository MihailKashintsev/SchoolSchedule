using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;

namespace Kiosk.Views
{
    public partial class BrowserMap : Window
    {
        public string DefaultMapUrl = App.Settings.MapUrl;
        private DispatcherTimer _resetTimer;
        private bool _isButtonAdded = false;
        private static CoreWebView2Environment _sharedEnvironment;
        private static readonly object _envLock = new object();
        private bool _isInitialized = false;

        public BrowserMap()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Используем общее окружение WebView2 для всех окон
                var environment = await GetSharedEnvironment();

                // Инициализируем WebView2 с общим окружением
                await webView.EnsureCoreWebView2Async(environment);

                // Настройки для ускорения загрузки и кэширования
                ConfigureWebViewSettings();

                // Подписываемся на события
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.NavigationStarting += WebView_NavigationStarting;

                // Загружаем начальную страницу
                if (!string.IsNullOrEmpty(DefaultMapUrl))
                {
                    webView.Source = new Uri(DefaultMapUrl);
                }

                // Настраиваем таймер сброса
                InitializeResetTimer();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<CoreWebView2Environment> GetSharedEnvironment()
        {
            if (_sharedEnvironment == null)
            {
                lock (_envLock)
                {
                    if (_sharedEnvironment == null)
                    {
                        var cacheFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "KioskApp",
                            "WebView2Cache",
                            "Maps");

                        Directory.CreateDirectory(cacheFolder);

                        var task = CoreWebView2Environment.CreateAsync(
                            browserExecutableFolder: null,
                            userDataFolder: cacheFolder,
                            options: new CoreWebView2EnvironmentOptions()
                            {
                                AdditionalBrowserArguments =
                                    "--disk-cache-size=268435456 " +
                                    "--media-cache-size=268435456 " +
                                    "--disable-background-networking " +
                                    "--no-first-run " +
                                    "--disable-features=TranslateUI"
                            });

                        _sharedEnvironment = task.GetAwaiter().GetResult();
                    }
                }
            }
            return _sharedEnvironment;
        }

        private void ConfigureWebViewSettings()
        {
            // Включаем кэширование для ускорения загрузки
            webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            // Включаем аппаратное ускорение для производительности
            webView.CoreWebView2.Settings.IsReputationCheckingRequired = false;

            // Отключаем ненужные функции для экономии ресурсов
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
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
            catch (Exception ex)
            {
                // Если не удалось добавить кнопку, попробуем еще раз через секунду
                await Task.Delay(1000);
                await AddBackButtonWithJavaScript();
            }
        }

        private void InitializeResetTimer()
        {
            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = TimeSpan.FromMinutes(5);
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
                webView.CoreWebView2.Navigate(DefaultMapUrl);
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
                // Возвращаемся в главное меню
                this.Close();
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowMainWindow();
            }
        }

        // Добавляем возможность закрытия окна по Escape
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowMainWindow();
            }
            base.OnKeyDown(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _resetTimer?.Stop();

                // Правильно очищаем WebView2
                if (webView != null && _isInitialized)
                {
                    // Отписываемся от событий
                    webView.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
                    webView.NavigationCompleted -= WebView_NavigationCompleted;
                    webView.NavigationStarting -= WebView_NavigationStarting;

                    // Останавливаем навигацию
                    webView.Stop();

                    // Очищаем источник
                    webView.Source = null;

                    // Выгружаем содержимое
                    webView.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при закрытии BrowserMap: {ex.Message}");
            }

            base.OnClosing(e);
        }
    }
}