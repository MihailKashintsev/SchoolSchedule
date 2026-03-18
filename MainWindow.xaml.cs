using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kiosk
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _idleTimer;
        private DispatcherTimer _bannerTimer;
        private DispatcherTimer _weatherTimer;
        private bool _isFullScreen = true;
        private ScheduleData _scheduleData;
        private ReplacementData _replacementData;
        private readonly JsonScheduleService _scheduleService = new();
        private readonly DocxReplacementService _replacementService = new();
        private readonly GigaChatService _gigaChat = new();

        private string _aiSelectedClass = "";
        private string _aiWeatherSummary = "";
        private bool _aiIsAsking = false;

        private List<string> _bannerImages = new();
        private int _currentBannerIndex = 0;
        private DateTime _lastUserActivity;
        private bool _isBannerMode = false;

        private LinearGradientBrush _scheduleBrush;
        private LinearGradientBrush _replacementsBrush;
        private LinearGradientBrush _aboutBrush;
        private LinearGradientBrush _mapBrush;
        private LinearGradientBrush _newsBrush;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeTimers();
                UpdateDateTime();
                LoadData();
                LoadBannerSettings();
                UpdateSchoolNames();

                if (App.Settings.WeatherEnabled)
                {
                    WeatherPanel.Visibility = Visibility.Visible;
                    _ = LoadWeatherAsync();
                }

                this.PreviewMouseMove += Window_PreviewMouseMove;
                this.PreviewMouseDown += Window_PreviewMouseDown;
                this.PreviewKeyDown += Window_PreviewKeyDown;
                this.PreviewTouchDown += Window_PreviewTouchDown;
                this.PreviewTouchMove += Window_PreviewTouchMove;
                Loaded += MainWindow_Loaded;

                _lastUserActivity = DateTime.Now;
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            }
            catch (Exception ex)
            {
                var logPath = System.IO.Path.Combine(Program.DataFolder, "crash.log");
                try { System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now}] MainWindow ctor crash:\n{ex}\n{new string('-', 60)}\n"); }
                catch { }
                MessageBox.Show(
                    $"Ошибка запуска:\n{ex.Message}\n\nПодробности:\n{logPath}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }

        private async Task LoadWeatherAsync()
        {
            WeatherLoadingText.Visibility = Visibility.Visible;
            WeatherContent.Visibility = Visibility.Collapsed;

            var info = await WeatherService.GetWeatherAsync(
                App.Settings.WeatherLat,
                App.Settings.WeatherLon,
                string.IsNullOrWhiteSpace(App.Settings.WeatherCity) ? null : App.Settings.WeatherCity);

            if (!info.IsLoaded) { WeatherLoadingText.Text = "Нет данных"; return; }

            WeatherLoadingText.Visibility = Visibility.Collapsed;
            WeatherContent.Visibility = Visibility.Visible;
            WeatherEmoji.Text = info.WeatherEmoji;
            WeatherTemp.Text = $"{info.Temperature:+0;-0;0}°C";
            WeatherDesc.Text = info.WeatherDescription;
            WeatherCity.Text = info.CityName;
            WeatherDetails.Text = $"Ощущается {info.FeelsLike:+0;-0;0}°  ·  Ветер {info.WindSpeed} м/с  ·  Влажность {info.Humidity}%";
            _aiWeatherSummary = $"{info.WeatherEmoji} {info.Temperature:+0;-0;0}°C, {info.WeatherDescription}, ощущается {info.FeelsLike:+0;-0;0}°C, ветер {info.WindSpeed} м/с, влажность {info.Humidity}%";
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) => CreateButtonAnimations();

        private void CreateButtonAnimations()
        {
            _scheduleBrush = CreateAnimatedBrush(Color.FromRgb(44,95,158), Color.FromRgb(52,152,219), Color.FromRgb(41,128,185), Color.FromRgb(26,74,122));
            if (ScheduleButton.Template.FindName("border", ScheduleButton) is Border sb) sb.Background = _scheduleBrush;

            _replacementsBrush = CreateAnimatedBrush(Color.FromRgb(255,230,126), Color.FromRgb(255,165,0), Color.FromRgb(255,69,0), Color.FromRgb(255,215,0));
            if (ReplacementsButton.Template.FindName("border", ReplacementsButton) is Border rb) rb.Background = _replacementsBrush;

            _aboutBrush = CreateAnimatedBrush(Color.FromRgb(155,89,182), Color.FromRgb(231,76,60), Color.FromRgb(243,156,18), Color.FromRgb(142,68,173));
            if (AboutButton.Template.FindName("border", AboutButton) is Border ab) ab.Background = _aboutBrush;

            _mapBrush = CreateAnimatedBrush(Color.FromRgb(39,174,96), Color.FromRgb(46,204,113), Color.FromRgb(52,152,219), Color.FromRgb(34,153,84));
            if (MapBrowserButton.Template.FindName("border", MapBrowserButton) is Border mb) mb.Background = _mapBrush;

            _newsBrush = CreateAnimatedBrush(Color.FromRgb(67,97,238), Color.FromRgb(58,12,163), Color.FromRgb(114,9,183), Color.FromRgb(247,37,133));
            if (News.Template.FindName("border", News) is Border nb) nb.Background = _newsBrush;
        }

        private LinearGradientBrush CreateAnimatedBrush(Color c1, Color c2, Color c3, Color c4)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(1,1) };
            var stop1 = new GradientStop(c1, 0);
            var stop2 = new GradientStop(c1, 0.5);
            var stop3 = new GradientStop(c3, 1);
            brush.GradientStops.Add(stop1); brush.GradientStops.Add(stop2); brush.GradientStops.Add(stop3);
            stop1.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation { From=c1, To=c2, Duration=TimeSpan.FromSeconds(2), AutoReverse=true, RepeatBehavior=RepeatBehavior.Forever });
            stop2.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation { From=c3, To=c4, Duration=TimeSpan.FromSeconds(3), BeginTime=TimeSpan.FromSeconds(1), AutoReverse=true, RepeatBehavior=RepeatBehavior.Forever });
            return brush;
        }

        private async void LoadBannerSettings()
        {
            if (!App.Settings.EnableBanners || string.IsNullOrEmpty(App.Settings.BannerImagePaths)) return;
            var paths = App.Settings.BannerImagePaths.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
            _bannerImages.Clear();
            foreach (var path in paths)
            {
                try
                {
                    var lp = await Services.FileSourceService.GetLocalPathAsync(path);
                    if (!string.IsNullOrEmpty(lp) && File.Exists(lp)) { _bannerImages.Add(lp); continue; }
                }
                catch { }
                var ap = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(path));
                if (File.Exists(ap)) _bannerImages.Add(ap);
            }
            if (_bannerImages.Count > 0)
            {
                _idleTimer.Interval = TimeSpan.FromSeconds(App.Settings.BannerTimeout);
                _bannerTimer.Interval = TimeSpan.FromSeconds(App.Settings.BannerSwitchInterval);
                _idleTimer.Start();
            }
        }

        private void InitializeTimers()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateDateTime();
            _timer.Start();

            _idleTimer = new DispatcherTimer();
            _idleTimer.Tick += IdleTimer_Tick;

            _bannerTimer = new DispatcherTimer();
            _bannerTimer.Tick += BannerTimer_Tick;

            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _weatherTimer.Tick += async (s, e) => { if (App.Settings.WeatherEnabled) await LoadWeatherAsync(); };
            _weatherTimer.Start();
        }

        private void ResetIdleTimer()
        {
            if (_isBannerMode) return;
            _lastUserActivity = DateTime.Now;
            _idleTimer.Stop();
            if (_bannerImages.Count > 0 && App.Settings.EnableBanners) _idleTimer.Start();
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastUserActivity).TotalSeconds >= App.Settings.BannerTimeout) StartBannerMode();
        }

        private void BannerTimer_Tick(object sender, EventArgs e) => ShowNextBannerWithAnimation();

        private async void StartBannerMode()
        {
            if (_bannerImages.Count == 0 || _isBannerMode) return;
            _isBannerMode = true; _idleTimer.Stop();
            _currentBannerIndex = 0; ShowBanner(_bannerImages[0]);
            BannerGrid.Visibility = Visibility.Visible;
            BannerGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
            _bannerTimer.Start();
            await Task.CompletedTask;
        }

        private async void ExitBannerMode()
        {
            _bannerTimer.Stop();
            BannerGrid.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)));
            await Task.Delay(300);
            BannerGrid.Visibility = Visibility.Collapsed;
            _isBannerMode = false; _lastUserActivity = DateTime.Now;
            if (_bannerImages.Count > 0 && App.Settings.EnableBanners) _idleTimer.Start();
        }

        private void ShowNextBannerWithAnimation()
        {
            _currentBannerIndex = (_currentBannerIndex + 1) % _bannerImages.Count;
            ShowBanner(_bannerImages[_currentBannerIndex]);
        }

        private void ShowBanner(string path)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                BannerImage.Source = img;
            }
            catch { }
        }

        private void UpdateDateTime()
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm:ss");
            DateText.Text = GetRussianDateString(now);
        }

        private string GetRussianDateString(DateTime d)
        {
            string[] days = { "воскресенье","понедельник","вторник","среда","четверг","пятница","суббота" };
            string[] months = { "января","февраля","марта","апреля","мая","июня","июля","августа","сентября","октября","ноября","декабря" };
            return $"{days[(int)d.DayOfWeek]}, {d.Day} {months[d.Month-1]} {d.Year}";
        }

        // ─── ИИ-Помощник ─────────────────────────────────────────────────────

        private void UpdateAssistantInfo()
        {
            if (AIClassComboBox.SelectedItem != null)
                _aiSelectedClass = AIClassComboBox.SelectedItem.ToString();
        }

        private void ShowQuestionButtons()
        {
            AIContentPanel.Children.Clear();
            AIContentPanel.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = "Привет! Чем помочь? 👋",
                Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 12)
            });

            var questions = new[]
            {
                ("📚", "Какой сейчас урок?",       "Какой сейчас урок для класса {CLASS}?"),
                ("⏰", "Когда следующий урок?",     "Когда следующий урок для класса {CLASS}?"),
                ("🔔", "Когда звонок?",             "Когда ближайший звонок для класса {CLASS}?"),
                ("🔄", "Есть ли замены?",           "Есть ли замены для класса {CLASS} сегодня?"),
                ("📅", "Расписание на сегодня",     "Покажи расписание для класса {CLASS} на сегодня."),
                ("☀️", "Какая погода?",             "Какая сейчас погода на улице?"),
                ("🎒", "Что нести завтра?",         "Какие уроки у класса {CLASS} завтра?"),
            };

            foreach (var (emoji, label, template) in questions)
                AIContentPanel.Children.Add(MakeQuestionButton(emoji, label, template));
        }

        private Button MakeQuestionButton(string emoji, string label, string questionTemplate)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = emoji, FontSize = 26,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = label, FontSize = 18,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var innerBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(16, 46, 82)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 77, 183)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16, 14, 16, 14),
                Child = sp
            };

            var btn = new Button
            {
                Content = innerBorder,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            btn.Click += async (s, e) =>
            {
                if (_aiIsAsking) return;
                if (questionTemplate.Contains("{CLASS}") && string.IsNullOrWhiteSpace(_aiSelectedClass))
                { ShowMessage("⚠️ Сначала выберите класс выше."); return; }
                await AskGigaChat(questionTemplate.Replace("{CLASS}", _aiSelectedClass));
            };
            return btn;
        }

        private async Task AskGigaChat(string question)
        {
            if (_aiIsAsking) return;
            _aiIsAsking = true;
            AIContentPanel.Children.Clear();
            AIContentPanel.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = "🤔 ИИ думает...",
                Foreground = new SolidColorBrush(Color.FromRgb(138,173,212)),
                FontSize = 20, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0,20,0,10)
            });
            AIContentPanel.Children.Add(MakeBackButton());

            try
            {
                var context = AssistantContextBuilder.Build(_scheduleData, _replacementData, _aiWeatherSummary, _aiSelectedClass, DateTime.Now);
                var answer = await _gigaChat.AskAsync(App.Settings.GigaChatApiKey ?? "", context, question);

                AIContentPanel.Children.Clear();
                AIContentPanel.Children.Add(new Emoji.Wpf.TextBlock
                {
                    Text = $"❓ {question}",
                    Foreground = new SolidColorBrush(Color.FromRgb(138,173,212)),
                    FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10)
                });
                var ab = new Border { Background = new SolidColorBrush(Color.FromRgb(12,34,58)), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0,0,0,10) };
                ab.Child = new Emoji.Wpf.TextBlock { Text = answer, Foreground = Brushes.White, FontSize = 17, TextWrapping = TextWrapping.Wrap, LineHeight = 26 };
                AIContentPanel.Children.Add(ab);
                AIContentPanel.Children.Add(MakeBackButton());
            }
            catch (Exception ex)
            {
                AIContentPanel.Children.Clear();
                ShowMessage($"❌ Ошибка: {ex.Message}");
                AIContentPanel.Children.Add(MakeBackButton());
            }
            finally { _aiIsAsking = false; }
        }

        private Button MakeBackButton()
        {
            var btn = new Button
            {
                Content = new Emoji.Wpf.TextBlock { Text = "← Назад к вопросам", Foreground = Brushes.White, FontSize = 18 },
                Background = new SolidColorBrush(Color.FromRgb(30,77,183)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12,8,12,8),
                Margin = new Thickness(0,4,0,0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) => ShowQuestionButtons();
            return btn;
        }

        private void ShowMessage(string text)
        {
            AIContentPanel.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = text, Foreground = Brushes.White, FontSize = 13,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0,10,0,10)
            });
        }

        // ─── Загрузка данных ─────────────────────────────────────────────────

        private async void LoadData()
        {
            try
            {
                _scheduleData = await _scheduleService.LoadScheduleAsync(App.Settings.ScheduleFilePath);
                UpdateClassComboBox();
                if (!string.IsNullOrWhiteSpace(_aiSelectedClass)) ShowQuestionButtons();
                _replacementData = await _replacementService.LoadReplacementsAsync(App.Settings.ReplacementsFilePath);
                StatusText.Text = _replacementData?.HasReplacements == true
                    ? $"Данные загружены ({_scheduleData.Schedules.Count} классов, есть замены)"
                    : $"Данные загружены ({_scheduleData?.Schedules?.Count ?? 0} классов, замен нет)";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка загрузки данных: {ex.Message}"; }
        }

        private void UpdateClassComboBox()
        {
            AIClassComboBox.Items.Clear();
            if (_scheduleData?.Schedules != null && _scheduleData.Schedules.Any())
            {
                foreach (var s in _scheduleData.Schedules.OrderBy(x => x.ClassName))
                    AIClassComboBox.Items.Add(s.ClassName);
                if (AIClassComboBox.Items.Count > 0) AIClassComboBox.SelectedIndex = 0;
            }
        }

        public void UpdateSchoolNames()
        {
            SchoolFullNameText.Text = App.Settings.SchoolFullName;
            SchoolShortNameText.Text = App.Settings.SchoolShortName;
        }

        // ─── Обработчики событий ─────────────────────────────────────────────

        private void AIClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AIClassComboBox.SelectedItem != null)
            {
                _aiSelectedClass = AIClassComboBox.SelectedItem.ToString();
                ShowQuestionButtons();
            }
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e) { if (!_isBannerMode) ResetIdleTimer(); }
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { if (_isBannerMode) ExitBannerMode(); else ResetIdleTimer(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (_isBannerMode) { ExitBannerMode(); return; } ResetIdleTimer(); if (e.Key == Key.F11) ToggleFullScreen(); else if (e.Key == Key.Escape && _isFullScreen) ToggleFullScreen(); }
        private void Window_PreviewTouchDown(object sender, TouchEventArgs e) { if (_isBannerMode) ExitBannerMode(); else ResetIdleTimer(); }
        private void Window_PreviewTouchMove(object sender, TouchEventArgs e) { if (!_isBannerMode) ResetIdleTimer(); }
        private void ExitBannerButton_Click(object sender, RoutedEventArgs e) => ExitBannerMode();
        private void NewsButton_Click(object sender, RoutedEventArgs e) => new NewsBrowserWindow().Show();
        private void MapBrowserButton_Click(object sender, RoutedEventArgs e) => new BrowserMap().Show();

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            try { var w = new Views.ScheduleWindow(); w.Owner = this; w.Show(); this.Hide(); }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ReplacementsButton_Click(object sender, RoutedEventArgs e)
        {
            try { var w = new Views.ReplacementsWindow(); w.Owner = this; w.Show(); this.Hide(); }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool authenticated = false;
                if (App.Settings.ShowKeyboardForPassword)
                { var pw = new PasswordWindow(); authenticated = pw.ShowDialog() == true && pw.IsPasswordCorrect; }
                else
                { var pw = new SimplePasswordDialog(); authenticated = pw.ShowDialog() == true && pw.IsPasswordCorrect; }

                if (!authenticated) { MessageBox.Show("Неверный пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }

                var sw = new SettingsWindow(); sw.Owner = this; sw.ShowDialog();
                LoadBannerSettings();
                WeatherPanel.Visibility = App.Settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
                if (App.Settings.WeatherEnabled) _ = LoadWeatherAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try { var w = new Views.AboutWindow(); w.Owner = this; w.Show(); this.Hide(); }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isBannerMode) { ExitBannerMode(); return; }
            if (e.Key == Key.F11) ToggleFullScreen();
            else if (e.Key == Key.Escape && _isFullScreen) ToggleFullScreen();
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            { WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Maximized; _isFullScreen = false; StatusText.Text = "Оконный режим • F11 - полноэкранный режим"; }
            else
            { WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; _isFullScreen = true; StatusText.Text = "Полноэкранный режим • F11 - оконный режим"; }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _idleTimer?.Stop();
            _bannerTimer?.Stop();
            _weatherTimer?.Stop();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}
