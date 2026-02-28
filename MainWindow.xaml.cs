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
        private DispatcherTimer _aiTimer;
        private DispatcherTimer _idleTimer;
        private DispatcherTimer _bannerTimer;
        private DispatcherTimer _weatherTimer;
        private bool _isFullScreen = true;
        private ScheduleData _scheduleData;
        private ReplacementData _replacementData;
        private readonly JsonScheduleService _scheduleService = new();
        private readonly DocxReplacementService _replacementService = new();
        private readonly SchoolAssistantService _assistantService = new();

        private List<string> _bannerImages = new();
        private int _currentBannerIndex = 0;
        private DateTime _lastUserActivity;
        private bool _isBannerMode = false;

        // Анимированные кисти для кнопок
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

                // Сразу подставляем названия из настроек
                UpdateSchoolNames();

                // Загружаем погоду
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

        // ─── Погода ───────────────────────────────────────────────────────────

        private async Task LoadWeatherAsync()
        {
            WeatherLoadingText.Visibility = Visibility.Visible;
            WeatherContent.Visibility = Visibility.Collapsed;

            var info = await WeatherService.GetWeatherAsync(
                App.Settings.WeatherLat,
                App.Settings.WeatherLon,
                string.IsNullOrWhiteSpace(App.Settings.WeatherCity) ? null : App.Settings.WeatherCity
            );

            if (!info.IsLoaded)
            {
                WeatherLoadingText.Text = "Нет данных";
                return;
            }

            WeatherLoadingText.Visibility = Visibility.Collapsed;
            WeatherContent.Visibility = Visibility.Visible;

            WeatherEmoji.Text = info.WeatherEmoji;
            WeatherTemp.Text = $"{info.Temperature:+0;-0;0}°C";
            WeatherDesc.Text = info.WeatherDescription;
            WeatherCity.Text = info.CityName;
            WeatherDetails.Text =
                $"Ощущается {info.FeelsLike:+0;-0;0}°  ·  Ветер {info.WindSpeed} м/с  ·  Влажность {info.Humidity}%";
        }

        // ─── Анимации кнопок ─────────────────────────────────────────────────

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CreateButtonAnimations();
        }

        private void CreateButtonAnimations()
        {
            _scheduleBrush = CreateAnimatedBrush(
                Color.FromRgb(44, 95, 158), Color.FromRgb(52, 152, 219),
                Color.FromRgb(41, 128, 185), Color.FromRgb(26, 74, 122));
            if (ScheduleButton.Template.FindName("border", ScheduleButton) is Border scheduleBorder)
                scheduleBorder.Background = _scheduleBrush;

            _replacementsBrush = CreateAnimatedBrush(
                Color.FromRgb(255, 230, 126), Color.FromRgb(255, 165, 0),
                Color.FromRgb(255, 69, 0), Color.FromRgb(255, 215, 0));
            if (ReplacementsButton.Template.FindName("border", ReplacementsButton) is Border replacementsBorder)
                replacementsBorder.Background = _replacementsBrush;

            _aboutBrush = CreateAnimatedBrush(
                Color.FromRgb(155, 89, 182), Color.FromRgb(231, 76, 60),
                Color.FromRgb(243, 156, 18), Color.FromRgb(142, 68, 173));
            if (AboutButton.Template.FindName("border", AboutButton) is Border aboutBorder)
                aboutBorder.Background = _aboutBrush;

            _mapBrush = CreateAnimatedBrush(
                Color.FromRgb(39, 174, 96), Color.FromRgb(46, 204, 113),
                Color.FromRgb(52, 152, 219), Color.FromRgb(34, 153, 84));
            if (MapBrowserButton.Template.FindName("border", MapBrowserButton) is Border mapBorder)
                mapBorder.Background = _mapBrush;

            _newsBrush = CreateAnimatedBrush(
                Color.FromRgb(67, 97, 238), Color.FromRgb(58, 12, 163),
                Color.FromRgb(114, 9, 183), Color.FromRgb(247, 37, 133));
            if (News.Template.FindName("border", News) is Border newsBorder)
                newsBorder.Background = _newsBrush;
        }

        private LinearGradientBrush CreateAnimatedBrush(Color c1, Color c2, Color c3, Color c4)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            var stop1 = new GradientStop(c1, 0);
            var stop2 = new GradientStop(c1, 0.5);
            var stop3 = new GradientStop(c3, 1);
            brush.GradientStops.Add(stop1);
            brush.GradientStops.Add(stop2);
            brush.GradientStops.Add(stop3);

            stop1.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation
            {
                From = c1, To = c2, Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
            });
            stop2.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation
            {
                From = c3, To = c4, Duration = TimeSpan.FromSeconds(3),
                BeginTime = TimeSpan.FromSeconds(1),
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever
            });
            return brush;
        }

        // ─── Баннеры ─────────────────────────────────────────────────────────

        private void LoadBannerSettings()
        {
            if (App.Settings.EnableBanners && !string.IsNullOrEmpty(App.Settings.BannerImagePaths))
            {
                var paths = App.Settings.BannerImagePaths.Split(';')
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .ToList();

                _bannerImages.Clear();
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        _bannerImages.Add(path);
                    else
                    {
                        var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(path));
                        if (File.Exists(appPath)) _bannerImages.Add(appPath);
                    }
                }

                if (_bannerImages.Count > 0)
                {
                    _idleTimer.Interval = TimeSpan.FromSeconds(App.Settings.BannerTimeout);
                    _bannerTimer.Interval = TimeSpan.FromSeconds(App.Settings.BannerSwitchInterval);
                    _idleTimer.Start();
                }
            }
        }

        // ─── Таймеры ─────────────────────────────────────────────────────────

        private void InitializeTimers()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateDateTime();
            _timer.Start();

            _aiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _aiTimer.Tick += (s, e) => UpdateAssistantInfo();
            _aiTimer.Start();

            _idleTimer = new DispatcherTimer();
            _idleTimer.Tick += IdleTimer_Tick;

            _bannerTimer = new DispatcherTimer();
            _bannerTimer.Tick += BannerTimer_Tick;

            // Погода обновляется каждые 15 минут
            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _weatherTimer.Tick += async (s, e) =>
            {
                if (App.Settings.WeatherEnabled) await LoadWeatherAsync();
            };
            _weatherTimer.Start();
        }

        private void ResetIdleTimer()
        {
            if (_isBannerMode) return;
            _lastUserActivity = DateTime.Now;
            _idleTimer.Stop();
            if (_bannerImages.Count > 0 && App.Settings.EnableBanners)
                _idleTimer.Start();
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastUserActivity).TotalSeconds >= App.Settings.BannerTimeout)
                StartBannerMode();
        }

        private void BannerTimer_Tick(object sender, EventArgs e)
        {
            ShowNextBannerWithAnimation();
        }

        private async void StartBannerMode()
        {
            if (_bannerImages.Count == 0 || _isBannerMode) return;
            _isBannerMode = true;
            _idleTimer.Stop();

            // Показываем первый баннер
            _currentBannerIndex = 0;
            ShowBanner(_bannerImages[_currentBannerIndex]);
            BannerGrid.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500));
            BannerGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _bannerTimer.Start();
            await Task.CompletedTask;
        }

        private async void ExitBannerMode()
        {
            _bannerTimer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            BannerGrid.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            await Task.Delay(300);
            BannerGrid.Visibility = Visibility.Collapsed;
            _isBannerMode = false;
            _lastUserActivity = DateTime.Now;
            if (_bannerImages.Count > 0 && App.Settings.EnableBanners)
                _idleTimer.Start();
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

        // ─── Дата/время ──────────────────────────────────────────────────────

        private void UpdateDateTime()
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm:ss");
            DateText.Text = GetRussianDateString(now);
        }

        private string GetRussianDateString(DateTime d)
        {
            string[] days = { "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" };
            string[] months = { "января", "февраля", "марта", "апреля", "мая", "июня",
                                 "июля", "августа", "сентября", "октября", "ноября", "декабря" };
            return $"{days[(int)d.DayOfWeek]}, {d.Day} {months[d.Month - 1]} {d.Year}";
        }

        // ─── Помощник ────────────────────────────────────────────────────────

        private void UpdateAssistantInfo()
        {
            if (AIClassComboBox.SelectedItem == null || _scheduleData == null) return;
            var cls = AIClassComboBox.SelectedItem.ToString();
            var info = _assistantService.GetCurrentInfo(_scheduleData, _replacementData, cls, DateTime.Now);
            UpdateAIContent(info, cls, DateTime.Now);
        }

        private void UpdateAIContent(AssistantInfo info, string className, DateTime currentTime)
        {
            AIContentPanel.Children.Clear();
            AddAITitle($"Информация для {className}");
            AddCurrentState(info.CurrentState, currentTime);
            if (info.ClassReplacements.Any()) AddReplacementsInfo(info.ClassReplacements);
            if (info.NextLesson != null) AddNextLessonInfo(info.NextLesson);
            if (info.TodayLessons.Any()) AddTodaySchedule(info.TodayLessons);
        }

        private void AddAITitle(string title)
        {
            AIContentPanel.Children.Add(new TextBlock
            {
                Text = title, Foreground = Brushes.White, FontSize = 16,
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 15),
                TextAlignment = TextAlignment.Center
            });
        }

        private void AddCurrentState(CurrentState state, DateTime currentTime)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            if (state.IsLesson && state.CurrentLesson != null)
            {
                AddStateItem(panel, "📚 Сейчас идет:", $"{state.CurrentLesson.Number} урок - {state.CurrentLesson.Subject}");
                AddStateItem(panel, "⏰ До конца:", _assistantService.FormatTimeRemaining(state.TimeRemaining));
                AddStateItem(panel, "👨‍🏫 Учитель:", state.CurrentLesson.Teacher);
                AddStateItem(panel, "🚪 Кабинет:", state.CurrentLesson.Classroom);
            }
            else if (state.IsBreak && state.NextLesson != null)
            {
                AddStateItem(panel, "☕ Сейчас перемена", "");
                AddStateItem(panel, "⏰ До урока:", _assistantService.FormatTimeRemaining(state.TimeRemaining));
                AddStateItem(panel, "📚 Следующий:", $"{state.NextLesson.Number} урок - {state.NextLesson.Subject}");
                AddStateItem(panel, "👨‍🏫 Учитель:", state.NextLesson.Teacher);
                AddStateItem(panel, "🚪 Кабинет:", state.NextLesson.Classroom);
            }
            else if (state.IsSchoolOver)
                AddStateItem(panel, "🎉 Уроки завершены", "Хорошего отдыха!");
            else
                AddStateItem(panel, "ℹ️ Нет информации", "Выберите другой класс или проверьте расписание");
            AIContentPanel.Children.Add(panel);
        }

        private void AddStateItem(Panel parent, string label, string value)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lb = new TextBlock { Text = label, Foreground = Brushes.LightBlue, FontSize = 12, FontWeight = FontWeights.SemiBold };
            var vb = new TextBlock { Text = value, Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(lb, 0); Grid.SetColumn(vb, 1);
            grid.Children.Add(lb); grid.Children.Add(vb);
            parent.Children.Add(grid);
        }

        private void AddReplacementsInfo(List<ReplacementLesson> replacements)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(10)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "🔄 Замены на сегодня:", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(0, 0, 0, 5) });
            foreach (var r in replacements)
            {
                var txt = $"{r.LessonNumber} урок: {r.ReplacementTeacher}";
                if (!string.IsNullOrEmpty(r.Classroom) && r.Classroom != "-") txt += $" ({r.Classroom})";
                if (!string.IsNullOrEmpty(r.Notes)) txt += $" - {r.Notes}";
                sp.Children.Add(new TextBlock { Text = txt, Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(10, 2, 0, 2), TextWrapping = TextWrapping.Wrap });
            }
            border.Child = sp;
            AIContentPanel.Children.Add(border);
        }

        private void AddNextLessonInfo(Lesson nextLesson)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(10)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "➡️ Следующий урок:", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(0, 0, 0, 5) });
            sp.Children.Add(new TextBlock
            {
                Text = $"{nextLesson.Number} урок: {nextLesson.Subject}\nУчитель: {nextLesson.Teacher}\nКабинет: {nextLesson.Classroom}",
                Foreground = Brushes.White, FontSize = 11, TextWrapping = TextWrapping.Wrap
            });
            border.Child = sp;
            AIContentPanel.Children.Add(border);
        }

        private void AddTodaySchedule(List<Lesson> lessons)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(10)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = $"📅 Расписание на сегодня ({lessons.Count} уроков):", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(0, 0, 0, 5) });
            foreach (var l in lessons.OrderBy(x => x.Number))
            {
                var t = $"{l.Number}. {l.Time} - {l.Subject}";
                if (!string.IsNullOrEmpty(l.Teacher)) t += $" ({l.Teacher})";
                if (!string.IsNullOrEmpty(l.Classroom)) t += $" - {l.Classroom}";
                sp.Children.Add(new TextBlock { Text = t, Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(10, 2, 0, 2), TextWrapping = TextWrapping.Wrap });
            }
            border.Child = sp;
            AIContentPanel.Children.Add(border);
        }

        // ─── Загрузка данных ─────────────────────────────────────────────────

        private async void LoadData()
        {
            try
            {
                _scheduleData = await _scheduleService.LoadScheduleAsync(App.Settings.ScheduleFilePath);
                UpdateClassComboBox();
                _replacementData = _replacementService.LoadReplacements(App.Settings.ReplacementsFilePath);
                StatusText.Text = _replacementData != null && _replacementData.HasReplacements
                    ? $"Данные загружены ({_scheduleData.Schedules.Count} классов, есть замены)"
                    : $"Данные загружены ({_scheduleData.Schedules.Count} классов, замен нет)";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка загрузки данных: {ex.Message}";
            }
        }

        private void UpdateClassComboBox()
        {
            AIClassComboBox.Items.Clear();
            if (_scheduleData?.Schedules != null && _scheduleData.Schedules.Any())
            {
                foreach (var s in _scheduleData.Schedules.OrderBy(x => x.ClassName))
                    AIClassComboBox.Items.Add(s.ClassName);
                if (AIClassComboBox.Items.Count > 0)
                    AIClassComboBox.SelectedIndex = 0;
            }
        }

        public void UpdateSchoolNames()
        {
            SchoolFullNameText.Text = App.Settings.SchoolFullName;
            SchoolShortNameText.Text = App.Settings.SchoolShortName;
        }

        // ─── Обработчики событий ─────────────────────────────────────────────

        private void AIClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateAssistantInfo();

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isBannerMode) return;
            ResetIdleTimer();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isBannerMode) ExitBannerMode();
            else ResetIdleTimer();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isBannerMode) { ExitBannerMode(); return; }
            ResetIdleTimer();
            if (e.Key == Key.F11) ToggleFullScreen();
            else if (e.Key == Key.Escape && _isFullScreen) ToggleFullScreen();
        }

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (_isBannerMode) ExitBannerMode();
            else ResetIdleTimer();
        }

        private void Window_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_isBannerMode) return;
            ResetIdleTimer();
        }

        private void ExitBannerButton_Click(object sender, RoutedEventArgs e) => ExitBannerMode();

        private void NewsButton_Click(object sender, RoutedEventArgs e)
            => new NewsBrowserWindow().Show();

        private void MapBrowserButton_Click(object sender, RoutedEventArgs e)
            => new BrowserMap().Show();

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new Views.ScheduleWindow();
                w.Owner = this; w.Show(); this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии расписания: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReplacementsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new Views.ReplacementsWindow();
                w.Owner = this; w.Show(); this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии замен: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool authenticated = false;
                if (App.Settings.ShowKeyboardForPassword)
                {
                    var pw = new PasswordWindow();
                    authenticated = pw.ShowDialog() == true && pw.IsPasswordCorrect;
                }
                else
                {
                    var pw = new SimplePasswordDialog();
                    authenticated = pw.ShowDialog() == true && pw.IsPasswordCorrect;
                }

                if (!authenticated)
                {
                    MessageBox.Show("Неверный пароль", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var sw = new SettingsWindow();
                sw.Owner = this;
                sw.ShowDialog();
                LoadBannerSettings();

                // Обновляем видимость виджета погоды
                WeatherPanel.Visibility = App.Settings.WeatherEnabled
                    ? Visibility.Visible : Visibility.Collapsed;
                if (App.Settings.WeatherEnabled)
                    _ = LoadWeatherAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии настроек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new Views.AboutWindow();
                w.Owner = this; w.Show(); this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Maximized;
                _isFullScreen = false;
                StatusText.Text = "Оконный режим • F11 - полноэкранный режим";
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                _isFullScreen = true;
                StatusText.Text = "Полноэкранный режим • F11 - оконный режим";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _aiTimer?.Stop();
            _idleTimer?.Stop();
            _bannerTimer?.Stop();
            _weatherTimer?.Stop();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}
