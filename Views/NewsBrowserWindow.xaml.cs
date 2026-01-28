using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;

namespace Kiosk.Views
{
    public partial class NewsBrowserWindow : Window
    {
        public string DefaultNewsUrl = App.Settings.NewsUrl;

        // БЕЛЫЙ СПИСОК: только эти страницы разрешены
        private readonly HashSet<string> _allowedUrlPatterns = new HashSet<string>
        {
            "vk.com/school_liga_khimki",
            "vk.com/feed",
            "vk.com/school_liga_khimki?w=",
            "vk.com/wall",
            "vk.com/video",
            "vk.com/photo",
            "m.vk.com/school_liga_khimki"
        };

        private DispatcherTimer _resetTimer;
        private DispatcherTimer _sessionCleanerTimer;
        private bool _isRedirecting = false;
        private bool _buttonsInjected = false;
        private static CoreWebView2Environment _sharedNewsEnvironment;
        private static readonly object _newsEnvLock = new object();
        private bool _isInitialized = false;

        public NewsBrowserWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Используем общее окружение для новостей
                var environment = await GetSharedNewsEnvironment();

                // Инициализируем WebView2 с общим окружением
                await webView.EnsureCoreWebView2Async(environment);

                // Настройки для ускорения загрузки
                ConfigureWebViewSettings();

                // Подписываемся на события
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.NavigationStarting += WebView_NavigationStarting;
                webView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
                webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // Загружаем начальную страницу
                if (!string.IsNullOrEmpty(DefaultNewsUrl))
                {
                    webView.Source = new Uri(DefaultNewsUrl);
                }

                // Настраиваем таймеры
                InitializeResetTimer();
                InitializeSessionCleanerTimer();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<CoreWebView2Environment> GetSharedNewsEnvironment()
        {
            if (_sharedNewsEnvironment == null)
            {
                lock (_newsEnvLock)
                {
                    if (_sharedNewsEnvironment == null)
                    {
                        var cacheFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "KioskApp",
                            "WebView2Cache",
                            "News");

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

                        _sharedNewsEnvironment = task.GetAwaiter().GetResult();
                    }
                }
            }
            return _sharedNewsEnvironment;
        }

        private void ConfigureWebViewSettings()
        {
            // Включаем кэширование для ускорения загрузки
            webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            // Отключаем ненужные функции
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }

        private void InitializeResetTimer()
        {
            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = TimeSpan.FromMinutes(2); // 2 минуты
            _resetTimer.Tick += async (sender, e) =>
            {
                await ResetToDefaultPage();
            };
            _resetTimer.Start();
        }

        private void InitializeSessionCleanerTimer()
        {
            _sessionCleanerTimer = new DispatcherTimer();
            _sessionCleanerTimer.Interval = TimeSpan.FromMinutes(1);
            _sessionCleanerTimer.Tick += async (sender, e) =>
            {
                await ClearBrowserData();
            };
            _sessionCleanerTimer.Start();
        }

        private async void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            // Вставляем кнопки сразу после загрузки DOM, до загрузки всех ресурсов
            await AddNavigationButtonsWithJavaScript();
            _buttonsInjected = true;

            // Проверяем URL после загрузки DOM
            CheckAndBlockCurrentUrl();
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_isRedirecting) return;

            await Task.Delay(100);

            // Если кнопки еще не были добавлены (например, если DOMContentLoaded не сработал)
            if (!_buttonsInjected)
            {
                await AddNavigationButtonsWithJavaScript();
                _buttonsInjected = true;
            }

            CheckAndBlockCurrentUrl();
        }

        private async Task AddNavigationButtonsWithJavaScript()
        {
            try
            {
                string script = @"
                    // Удаляем старые элементы
                    var existingContainer = document.getElementById('wpfButtonContainer');
                    if (existingContainer) existingContainer.remove();

                    // Создаем контейнер для кнопок
                    var buttonContainer = document.createElement('div');
                    buttonContainer.id = 'wpfButtonContainer';
                    buttonContainer.style.position = 'fixed';
                    buttonContainer.style.top = '20px';
                    buttonContainer.style.left = '20px';
                    buttonContainer.style.zIndex = '9999';
                    buttonContainer.style.display = 'flex';
                    buttonContainer.style.gap = '10px';
                    buttonContainer.style.flexDirection = 'column';
                    buttonContainer.style.alignItems = 'center';

                    // Кнопка НАЗАД
                    var backButton = document.createElement('button');
                    backButton.id = 'wpfBackButton';
                    backButton.innerHTML = 'НАЗАД';
                    backButton.style.padding = '15px 30px';
                    backButton.style.backgroundColor = '#2196F3';
                    backButton.style.color = 'white';
                    backButton.style.border = 'none';
                    backButton.style.borderRadius = '8px';
                    backButton.style.cursor = 'pointer';
                    backButton.style.fontSize = '18px';
                    backButton.style.fontWeight = 'bold';
                    backButton.style.boxShadow = '0 4px 12px rgba(33, 150, 243, 0.4)';
                    backButton.style.transition = 'all 0.3s ease';
                    backButton.style.width = '150px';
                    backButton.style.textAlign = 'center';
                    backButton.style.minHeight = '60px';
                    
                    // Кнопка ОБНОВИТЬ
                    var refreshButton = document.createElement('button');
                    refreshButton.id = 'wpfRefreshButton';
                    refreshButton.innerHTML = 'ОБНОВИТЬ';
                    refreshButton.style.padding = '15px 30px';
                    refreshButton.style.backgroundColor = '#4CAF50';
                    refreshButton.style.color = 'white';
                    refreshButton.style.border = 'none';
                    refreshButton.style.borderRadius = '8px';
                    refreshButton.style.cursor = 'pointer';
                    refreshButton.style.fontSize = '18px';
                    refreshButton.style.fontWeight = 'bold';
                    refreshButton.style.boxShadow = '0 4px 12px rgba(76, 175, 80, 0.4)';
                    refreshButton.style.transition = 'all 0.3s ease';
                    refreshButton.style.width = '150px';
                    refreshButton.style.textAlign = 'center';
                    refreshButton.style.minHeight = '60px';

                    // Эффекты при наведении для обеих кнопок
                    function setupButtonHover(button, originalColor, hoverColor) {
                        button.onmouseover = function() {
                            this.style.backgroundColor = hoverColor;
                            this.style.transform = 'scale(1.05)';
                            this.style.boxShadow = '0 6px 16px rgba(0,0,0,0.3)';
                        };
                        button.onmouseout = function() {
                            this.style.backgroundColor = originalColor;
                            this.style.transform = 'scale(1)';
                            this.style.boxShadow = '0 4px 12px rgba(0,0,0,0.2)';
                        };
                        
                        // Эффект при нажатии
                        button.onmousedown = function() {
                            this.style.transform = 'scale(0.95)';
                        };
                        button.onmouseup = function() {
                            this.style.transform = 'scale(1.05)';
                        };
                    }

                    setupButtonHover(backButton, '#2196F3', '#1976D2');
                    setupButtonHover(refreshButton, '#4CAF50', '#388E3C');

                    // Добавляем кнопки в контейнер
                    buttonContainer.appendChild(backButton);
                    buttonContainer.appendChild(refreshButton);
                    
                    // Добавляем контейнер на страницу
                    if (document.body) {
                        document.body.appendChild(buttonContainer);
                    } else {
                        // Если body еще не загружен, ждем его
                        var observer = new MutationObserver(function(mutations) {
                            if (document.body) {
                                document.body.appendChild(buttonContainer);
                                observer.disconnect();
                            }
                        });
                        observer.observe(document.documentElement, { childList: true, subtree: true });
                    }
                    
                    // Обработчики кликов
                    backButton.addEventListener('click', function() {
                        window.chrome.webview.postMessage('closeWindow');
                    });

                    refreshButton.addEventListener('click', function() {
                        window.chrome.webview.postMessage('refreshPage');
                    });

                    // Гарантируем, что кнопки всегда будут видны
                    var ensureButtonsVisible = function() {
                        if (buttonContainer.parentNode !== document.body && document.body) {
                            document.body.appendChild(buttonContainer);
                        }
                        buttonContainer.style.zIndex = '9999';
                    };
                    
                    setInterval(ensureButtonsVisible, 1000);
                    ensureButtonsVisible();

                    // Удаляем сообщение о блокировке, если оно есть
                    var blockMsg = document.getElementById('blockMessage');
                    if (blockMsg) {
                        blockMsg.remove();
                    }
                    
                    // Делаем кнопки более заметными для сенсорного экрана
                    buttonContainer.style.touchAction = 'manipulation';
                    backButton.style.touchAction = 'manipulation';
                    refreshButton.style.touchAction = 'manipulation';
                    
                    // Возвращаем успех
                    'Buttons injected successfully';
                ";

                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception)
            {
                // Повторяем попытку через 500мс
                await Task.Delay(500);
                await AddNavigationButtonsWithJavaScript();
            }
        }

        private async Task ClearBrowserData()
        {
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    // Очищаем куки
                    var cookieManager = webView.CoreWebView2.CookieManager;
                    var cookies = await cookieManager.GetCookiesAsync("");
                    foreach (var cookie in cookies)
                    {
                        cookieManager.DeleteCookie(cookie);
                    }

                    // Выполняем JavaScript для очистки хранилищ
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                        try {
                            // Очищаем localStorage
                            if (window.localStorage) {
                                localStorage.clear();
                            }
                            
                            // Очищаем sessionStorage
                            if (window.sessionStorage) {
                                sessionStorage.clear();
                            }
                            
                            // Очищаем IndexedDB
                            if (window.indexedDB) {
                                const dbs = await indexedDB.databases();
                                for (const db of dbs) {
                                    if (db.name) {
                                        indexedDB.deleteDatabase(db.name);
                                    }
                                }
                            }
                            
                            // Очищаем кэш
                            if (window.caches) {
                                const cacheNames = await caches.keys();
                                for (const name of cacheNames) {
                                    await caches.delete(name);
                                }
                            }
                            
                            // Удаляем все формы
                            const forms = document.getElementsByTagName('form');
                            for (const form of forms) {
                                form.reset();
                            }
                            
                            // Удаляем сохраненные данные автозаполнения
                            const inputs = document.querySelectorAll('input[type=""text""], input[type=""password""], input[type=""email""]');
                            for (const input of inputs) {
                                input.value = '';
                                input.autocomplete = 'off';
                            }
                        } catch(e) {
                            console.log('Ошибка очистки: ' + e);
                        }
                    ");
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки очистки
            }
        }

        private void CheckAndBlockCurrentUrl()
        {
            if (_isRedirecting || webView?.CoreWebView2?.Source == null)
                return;

            var currentUrl = webView.CoreWebView2.Source.ToString().ToLower();

            // Проверяем, есть ли URL в белом списке
            bool isAllowed = false;
            foreach (var allowedPattern in _allowedUrlPatterns)
            {
                if (currentUrl.Contains(allowedPattern.ToLower()))
                {
                    isAllowed = true;
                    break;
                }
            }

            // Если URL не в белом списке - блокируем
            if (!isAllowed)
            {
                _isRedirecting = true;
                Dispatcher.InvokeAsync(async () =>
                {
                    await BlockAndRedirect();
                });
            }
        }

        private async Task BlockAndRedirect()
        {
            try
            {
                // Показываем сообщение о блокировке через JavaScript
                string blockScript = @"
                    // Удаляем старые сообщения
                    var oldMsg = document.getElementById('blockMessage');
                    if (oldMsg) oldMsg.remove();
                    
                    // Показываем сообщение о блокировке
                    var blockMsg = document.createElement('div');
                    blockMsg.id = 'blockMessage';
                    blockMsg.innerHTML = '<h2 style=""color: red; text-align: center; margin-top: 50px;"">Эта страница недоступна</h2><p style=""text-align: center;"">Возвращаемся на главную страницу...</p>';
                    blockMsg.style.position = 'fixed';
                    blockMsg.style.top = '50%';
                    blockMsg.style.left = '50%';
                    blockMsg.style.transform = 'translate(-50%, -50%)';
                    blockMsg.style.zIndex = '10000';
                    blockMsg.style.backgroundColor = 'white';
                    blockMsg.style.padding = '30px';
                    blockMsg.style.borderRadius = '10px';
                    blockMsg.style.boxShadow = '0 4px 20px rgba(0,0,0,0.3)';
                    blockMsg.style.textAlign = 'center';
                    
                    document.body.appendChild(blockMsg);
                    
                    // Автоматически скрываем через 2 секунды
                    setTimeout(function() {
                        if (blockMsg.parentNode) {
                            blockMsg.remove();
                        }
                    }, 2000);
                ";

                await webView.ExecuteScriptAsync(blockScript);

                // Очищаем данные перед редиректом
                await ClearBrowserData();

                // Ждем 2 секунды
                await Task.Delay(2000);

                // Возвращаем на главную страницу
                await ResetToDefaultPage();
            }
            catch (Exception)
            {
                await ResetToDefaultPage();
            }
            finally
            {
                _isRedirecting = false;
            }
        }

        private async Task ResetToDefaultPage()
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                _buttonsInjected = false;
                _isRedirecting = true;

                try
                {
                    // Очищаем данные
                    await ClearBrowserData();

                    // Возвращаемся на главную
                    webView.CoreWebView2.Navigate(DefaultNewsUrl);
                }
                finally
                {
                    await Task.Delay(500);
                    _isRedirecting = false;
                }
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (_isRedirecting)
            {
                e.Cancel = true;
                return;
            }

            var url = e.Uri.ToLower();

            // Проверяем URL перед началом навигации (белый список)
            bool isAllowed = false;
            foreach (var allowedPattern in _allowedUrlPatterns)
            {
                if (url.Contains(allowedPattern.ToLower()))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                e.Cancel = true; // Отменяем навигацию
                _isRedirecting = true;

                Dispatcher.InvokeAsync(async () =>
                {
                    await BlockAndRedirect();
                });
                return;
            }

            _buttonsInjected = false;

            // Очищаем данные формы при переходе
            Dispatcher.InvokeAsync(async () =>
            {
                await ClearBrowserData();
            });
        }

        private void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (!_isRedirecting)
            {
                CheckAndBlockCurrentUrl();
            }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Блокируем открытие новых окон
            e.Handled = true;

            // Пробуем открыть ссылку в текущем окне, если она разрешена
            var url = e.Uri.ToLower();
            bool isAllowed = false;
            foreach (var allowedPattern in _allowedUrlPatterns)
            {
                if (url.Contains(allowedPattern.ToLower()))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (isAllowed && !_isRedirecting)
            {
                webView.CoreWebView2.Navigate(e.Uri);
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message == "closeWindow")
            {
                // Возвращаемся в главное меню
                this.Close();
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowMainWindow();
            }
            else if (message == "refreshPage")
            {
                // Обновляем страницу
                if (webView?.CoreWebView2 != null && !_isRedirecting)
                {
                    webView.CoreWebView2.Reload();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Возвращаемся в главное меню
                this.Close();
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.ShowMainWindow();
            }
            else if (e.Key == Key.F5) // F5 тоже обновляет страницу
            {
                if (webView?.CoreWebView2 != null && !_isRedirecting)
                {
                    webView.CoreWebView2.Reload();
                }
            }
            else if (e.Key == Key.BrowserBack || e.Key == Key.Back)
            {
                // Блокируем кнопку "Назад" браузера
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _resetTimer?.Stop();
                _sessionCleanerTimer?.Stop();

                // Правильно очищаем WebView2
                if (webView != null && _isInitialized)
                {
                    // Отписываемся от всех событий
                    webView.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
                    webView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                    webView.NavigationCompleted -= WebView_NavigationCompleted;
                    webView.NavigationStarting -= WebView_NavigationStarting;
                    webView.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
                    webView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;

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
                Console.WriteLine($"Ошибка при закрытии NewsBrowserWindow: {ex.Message}");
            }

            base.OnClosing(e);
        }

        // Метод для добавления новых URL в белый список
        public void AddAllowedUrl(string urlPattern)
        {
            if (!string.IsNullOrWhiteSpace(urlPattern))
            {
                _allowedUrlPatterns.Add(urlPattern.ToLower());
            }
        }

        // Метод для удаления URL из белого списка
        public void RemoveAllowedUrl(string urlPattern)
        {
            if (!string.IsNullOrWhiteSpace(urlPattern))
            {
                _allowedUrlPatterns.Remove(urlPattern.ToLower());
            }
        }
    }
}
