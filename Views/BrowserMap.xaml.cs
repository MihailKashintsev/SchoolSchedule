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
        private static CoreWebView2Environment _sharedEnvironment;
        private static readonly object _envLock = new object();
        private bool _isInitialized = false;

        // Переменные для управления масштабом
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double DefaultZoom = 1.0;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;

        private BrowserMapOverlay _overlay;

        public BrowserMap()
        {
            InitializeComponent();
            InitializeAsync();

            // Создаём оверлей поверх WebView2
            _overlay = new BrowserMapOverlay(this);
            _overlay.Show();

            this.Closed += (s, e) => _overlay?.Close();
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

                // Инициализируем отображение масштаба
                UpdateZoomDisplay();

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

            // Включаем жесты масштабирования двумя пальцами
            webView.CoreWebView2.Settings.IsPinchZoomEnabled = true;
        }

        private async Task InitializeZoomScript()
        {
            try
            {
                string zoomScript = @"
                    // Глобальный объект для управления масштабом
                    window.wpfMapZoom = {
                        current: 1.0,
                        min: 0.1,
                        max: 5.0,
                        step: 0.1,
                        default: 1.0,
                        
                        // Функция установки масштаба
                        setZoom: function(zoomLevel) {
                            zoomLevel = Math.max(this.min, Math.min(this.max, zoomLevel));
                            this.current = zoomLevel;
                            
                            // Применяем масштаб к body
                            document.body.style.transform = 'scale(' + zoomLevel + ')';
                            document.body.style.transformOrigin = 'top left';
                            document.body.style.width = (100 / zoomLevel) + '%';
                            document.body.style.height = (100 / zoomLevel) + '%';
                            
                            // Отправляем сообщение в C# для обновления отображения
                            window.chrome.webview.postMessage('zoomChanged:' + zoomLevel);
                            
                            return zoomLevel;
                        },
                        
                        // Сброс масштаба
                        reset: function() {
                            return this.setZoom(this.default);
                        },
                        
                        // Увеличение
                        zoomIn: function() {
                            return this.setZoom(this.current + this.step);
                        },
                        
                        // Уменьшение
                        zoomOut: function() {
                            return this.setZoom(this.current - this.step);
                        }
                    };
                    
                    // Восстанавливаем сохраненный масштаб из localStorage
                    if (window.localStorage) {
                        var savedZoom = localStorage.getItem('mapZoomLevel');
                        if (savedZoom) {
                            var zoom = parseFloat(savedZoom);
                            if (!isNaN(zoom)) {
                                window.wpfMapZoom.setZoom(zoom);
                            }
                        }
                    }
                    
                    // Сохраняем масштаб при изменении
                    var originalSetZoom = window.wpfMapZoom.setZoom;
                    window.wpfMapZoom.setZoom = function(zoomLevel) {
                        var result = originalSetZoom.call(this, zoomLevel);
                        if (window.localStorage) {
                            localStorage.setItem('mapZoomLevel', result);
                        }
                        return result;
                    };
                    
                    // Обработка жестов масштабирования
                    let gestureScale = 1;
                    document.addEventListener('gesturestart', function(e) {
                        gestureScale = window.wpfMapZoom.current;
                        e.preventDefault();
                    });
                    
                    document.addEventListener('gesturechange', function(e) {
                        if (e.scale && !isNaN(e.scale)) {
                            let newScale = gestureScale * e.scale;
                            window.wpfMapZoom.setZoom(newScale);
                            e.preventDefault();
                        }
                    });
                    
                    document.addEventListener('gestureend', function(e) {
                        e.preventDefault();
                    });
                ";

                await webView.ExecuteScriptAsync(zoomScript);
                _currentZoom = DefaultZoom;
                UpdateZoomDisplay();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка инициализации скрипта масштабирования: {ex.Message}");
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
                webView.CoreWebView2.Navigate(DefaultMapUrl);
            }
        }

        private void WebView_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            // Ничего не делаем при начале навигации
        }

        private async void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            // Ждем немного, чтобы страница полностью загрузилась
            await Task.Delay(1000);

            // Инициализируем скрипт масштабирования
            await InitializeZoomScript();
        }

        private void WebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();

            if (message.StartsWith("zoomChanged:"))
            {
                // Обновляем текущий масштаб
                if (double.TryParse(message.Substring("zoomChanged:".Length), out double zoomLevel))
                {
                    _currentZoom = zoomLevel;
                    UpdateZoomDisplay();
                }
            }
        }

        // Обновление отображения масштаба
        private void UpdateZoomDisplay()
        {
            Dispatcher.Invoke(() =>
            {
                var text = $"{(_currentZoom * 100):0}%";
                _overlay?.UpdateZoomText(text);
            });
        }

        // Публичные методы для вызова из оверлея
        public async void ZoomIn() => ZoomInButton_Click(null, null);
        public async void ZoomOut() => ZoomOutButton_Click(null, null);
        public async void ResetZoom() => ResetZoomButton_Click(null, null);
        public void GoBack() => CloseWindow();

        // Обработчики кнопок масштабирования
        private async void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (webView.CoreWebView2 != null)
            {
                try
                {
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.zoomIn();");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка увеличения масштаба: {ex.Message}");
                    // Если скрипт не инициализирован, инициализируем его и пробуем снова
                    await InitializeZoomScript();
                    await Task.Delay(100);
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.zoomIn();");
                }
            }
        }

        private async void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (webView.CoreWebView2 != null)
            {
                try
                {
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.zoomOut();");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка уменьшения масштаба: {ex.Message}");
                    // Если скрипт не инициализирован, инициализируем его и пробуем снова
                    await InitializeZoomScript();
                    await Task.Delay(100);
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.zoomOut();");
                }
            }
        }

        private async void ResetZoomButton_Click(object sender, RoutedEventArgs e)
        {
            if (webView.CoreWebView2 != null)
            {
                try
                {
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.reset();");
                    _currentZoom = DefaultZoom;
                    UpdateZoomDisplay();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка сброса масштаба: {ex.Message}");
                    // Если скрипт не инициализирован, инициализируем его и пробуем снова
                    await InitializeZoomScript();
                    await Task.Delay(100);
                    await webView.ExecuteScriptAsync("window.wpfMapZoom.reset();");
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void CloseWindow()
        {
            this.Close();

            // Просто закрываем это окно, главное окно уже должно быть видимо
            // или показываем его, если оно скрыто
            Application.Current.MainWindow?.Show();
            Application.Current.MainWindow?.Activate();
        }

        // Добавляем возможность закрытия окна по Escape
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseWindow();
            }
            else if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                // Увеличение по клавише +
                ZoomInButton_Click(null, null);
            }
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                // Уменьшение по клавише -
                ZoomOutButton_Click(null, null);
            }
            else if (e.Key == Key.Home || e.Key == Key.D0)
            {
                // Сброс по клавише Home или 0
                ResetZoomButton_Click(null, null);
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