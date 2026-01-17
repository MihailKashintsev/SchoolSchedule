using Microsoft.Web.WebView2.Wpf;
using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Kiosk.Views
{
    public partial class NewsBrowserWindow : Window
    {
        public string DefaultNewsUrl = App.Settings.NewsUrl;

        // БЕЛЫЙ СПИСОК: только эти страницы разрешены
        private readonly HashSet<string> _allowedUrlPatterns = new HashSet<string>
        {
            "vk.com/school_liga_khimki",   // Главная страница
            "vk.com/feed",                  // Лента новостей
            "vk.com/school_liga_khimki?w=", // Посты на стене
            "vk.com/wall",                  // Стена
            "vk.com/video",                 // Видео
            "vk.com/photo",                 // Фото
            "m.vk.com/school_liga_khimki"   // Мобильная версия
        };

        private DispatcherTimer _resetTimer;
        private DispatcherTimer _sessionCleanerTimer;
        private bool _isButtonAdded = false;
        private bool _isRedirecting = false;
        private string _tempUserDataFolder;

        public NewsBrowserWindow()
        {
            InitializeComponent();

            // Создаем временную папку для данных браузера
            _tempUserDataFolder = Path.Combine(Path.GetTempPath(), "KioskBrowser_" + Guid.NewGuid().ToString());

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Создаем окружение с временной папкой для данных
                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: _tempUserDataFolder);

                // Инициализируем WebView2 с нашим окружением
                await webView.EnsureCoreWebView2Async(environment);

                // Настраиваем параметры для предотвращения сохранения данных
                ConfigurePrivacySettings();

                // Включаем поддержку сообщений из JavaScript
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                // Подписываемся на события
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.NavigationStarting += WebView_NavigationStarting;
                webView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
                webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // Загружаем начальную страницу
                webView.Source = new Uri(DefaultNewsUrl);

                // Настраиваем таймеры
                InitializeResetTimer();
                InitializeSessionCleanerTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigurePrivacySettings()
        {
            // Отключаем сохранение данных
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Блокируем всплывающие окна
            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

            // Отключаем DevTools
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Отключаем контекстное меню
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // Блокируем скрипты, которые могут сохранять данные
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
        }

        private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            // Блокируем запросы к сервисам авторизации
            var requestUri = e.Request.Uri.ToLower();
            if (requestUri.Contains("login") ||
                requestUri.Contains("auth") ||
                requestUri.Contains("oauth") ||
                requestUri.Contains("password") ||
                requestUri.Contains("token"))
            {
                e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 403, "Forbidden", "Blocked by kiosk policy");
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

        private void InitializeSessionCleanerTimer()
        {
            _sessionCleanerTimer = new DispatcherTimer();
            _sessionCleanerTimer.Interval = TimeSpan.FromMinutes(1); // Очищаем каждую минуту
            _sessionCleanerTimer.Tick += async (sender, e) =>
            {
                await ClearBrowserData();
            };
            _sessionCleanerTimer.Start();
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
                _isButtonAdded = false;
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

            _isButtonAdded = false;

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

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_isRedirecting) return;

            await Task.Delay(500);

            CheckAndBlockCurrentUrl();

            if (!_isButtonAdded && !_isRedirecting)
            {
                await AddNavigationButtonsWithJavaScript();
                _isButtonAdded = true;
            }
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
                    document.body.appendChild(buttonContainer);
                    
                    // Обработчики кликов
                    backButton.addEventListener('click', function() {
                        window.chrome.webview.postMessage('closeWindow');
                    });

                    refreshButton.addEventListener('click', function() {
                        window.chrome.webview.postMessage('refreshPage');
                    });

                    // Гарантируем, что кнопки всегда будут видны
                    var ensureButtonsVisible = function() {
                        if (buttonContainer.parentNode !== document.body) {
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
                ";

                await webView.ExecuteScriptAsync(script);
            }
            catch (Exception)
            {
                await Task.Delay(1000);
                if (!_isRedirecting)
                {
                    await AddNavigationButtonsWithJavaScript();
                }
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message == "closeWindow")
            {
                this.Close();
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
                this.Close();
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
            _resetTimer?.Stop();
            _sessionCleanerTimer?.Stop();

            // Очищаем данные перед закрытием
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Stop();
                }
            }
            catch { }

            // Удаляем временную папку с данными браузера
            try
            {
                if (Directory.Exists(_tempUserDataFolder))
                {
                    for (int i = 0; i < 3; i++) // Пробуем несколько раз
                    {
                        try
                        {
                            Directory.Delete(_tempUserDataFolder, true);
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки удаления
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