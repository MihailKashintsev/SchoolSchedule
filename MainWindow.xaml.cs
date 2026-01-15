using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Views;
using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace Kiosk
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _aiTimer;
        private DispatcherTimer _idleTimer;
        private DateTime _lastActivityTime;
        private bool _isFullScreen = true;
        private bool _isBannerMode = false;
        private ScheduleData _scheduleData;
        private ReplacementData _replacementData;
        private readonly JsonScheduleService _scheduleService = new();
        private readonly DocxReplacementService _replacementService = new();
        private readonly SchoolAssistantService _assistantService = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimers();
            InitializeIdleTimer();
            UpdateDateTime();
            LoadData();

            // Set fullscreen mode
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;

            // Инициализация WebView2 асинхронно
            InitializeWebViewAsync();

            // Устанавливаем начальное время активности
            _lastActivityTime = DateTime.Now;
        }

        private void InitializeTimers()
        {
            // Таймер для обновления времени
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Таймер для обновления информации от ИИ
            _aiTimer = new DispatcherTimer();
            _aiTimer.Interval = TimeSpan.FromSeconds(10);
            _aiTimer.Tick += AITimer_Tick;
            _aiTimer.Start();
        }

        private void InitializeIdleTimer()
        {
            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromSeconds(1);
            _idleTimer.Tick += CheckIdleTime;
            _idleTimer.Start();
            _lastActivityTime = DateTime.Now;
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                // Инициализация WebView2
                await BannerWebView.EnsureCoreWebView2Async(null);

                // Настройка WebView2
                BannerWebView.CoreWebView2.Settings.IsScriptEnabled = true;
                BannerWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                // Устанавливаем запасной контент
                SetFallbackBannerContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка инициализации WebView2: {ex.Message}");
                SetFallbackBannerContent();
            }
        }

        private void SetFallbackBannerContent()
        {
            string fallbackHtml = @"
                <html>
                    <head>
                        <style>
                            body { 
                                margin: 0; 
                                padding: 0; 
                                background: linear-gradient(135deg, #2c5f9e 0%, #3498db 100%);
                                color: white;
                                font-family: 'Segoe UI', Arial, sans-serif;
                                height: 100vh;
                                display: flex;
                                flex-direction: column;
                                justify-content: center;
                                align-items: center;
                                text-align: center;
                            }
                            h1 { 
                                font-size: 48px; 
                                margin-bottom: 20px; 
                                text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
                            }
                            p { 
                                font-size: 24px; 
                                max-width: 800px; 
                                margin: 0 20px; 
                                line-height: 1.6;
                            }
                            .touch-hint {
                                margin-top: 40px;
                                font-size: 18px;
                                opacity: 0.8;
                                background: rgba(255,255,255,0.1);
                                padding: 10px 20px;
                                border-radius: 10px;
                            }
                        </style>
                    </head>
                    <body>
                        <h1>Школьный информационный киоск</h1>
                        <p>Для возврата в главное меню коснитесь любого места на экрана</p>
                        <div class='touch-hint'>Коснитесь экрана, чтобы продолжить</div>
                    </body>
                </html>";

            BannerWebView.NavigateToString(fallbackHtml);
        }

        private async void LoadData()
        {
            try
            {
                // Загружаем расписание
                _scheduleData = await _scheduleService.LoadScheduleAsync(App.Settings.ScheduleFilePath);
                UpdateClassComboBox();

                // Загружаем замены
                _replacementData = _replacementService.LoadReplacements(App.Settings.ReplacementsFilePath);

                // Проверяем наличие замен
                if (_replacementData != null && _replacementData.HasReplacements)
                {
                    StatusText.Text = $"Данные загружены ({_scheduleData.Schedules.Count} классов, есть замены)";
                }
                else
                {
                    StatusText.Text = $"Данные загружены ({_scheduleData.Schedules.Count} классов, замен нет)";
                }
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
                foreach (var schedule in _scheduleData.Schedules.OrderBy(s => s.ClassName))
                {
                    AIClassComboBox.Items.Add(schedule.ClassName);
                }

                if (AIClassComboBox.Items.Count > 0)
                    AIClassComboBox.SelectedIndex = 0;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void AITimer_Tick(object sender, EventArgs e)
        {
            UpdateAssistantInfo();
        }

        private void CheckIdleTime(object sender, EventArgs e)
        {
            var idleTime = DateTime.Now - _lastActivityTime;

            if (idleTime.TotalSeconds >= App.Settings.IdleTimeBeforeBanner && !_isBannerMode)
            {
                EnterBannerMode();
            }
        }

        // УПРОЩЕННАЯ ЛОГИКА: Сбрасываем таймер только при явных действиях
        private void ResetIdleTimer()
        {
            // Обновляем время последней активности
            _lastActivityTime = DateTime.Now;

            // НЕ выходим из режима баннера здесь!
            // Баннер будет скрываться только при явных кликах
        }

        // Метод для выхода из режима баннера при явном действии
        private void ExitBannerModeFromUserAction()
        {
            if (_isBannerMode)
            {
                _isBannerMode = false;

                // Скрываем баннер
                BannerOverlay.Visibility = Visibility.Collapsed;

                // Восстанавливаем основной интерфейс
                ContentGrid.Opacity = 1;

                // Сбрасываем таймер бездействия
                ResetIdleTimer();

                // Обновляем статус
                UpdateStatusText();
            }
        }

        private async void EnterBannerMode()
        {
            _isBannerMode = true;

            // Показываем баннер
            BannerOverlay.Visibility = Visibility.Visible;

            // Обновляем контент баннера
            await LoadBannerContent();

            // Добавляем эффект затемнения основного контента
            ContentGrid.Opacity = 0.3;

            // Обновляем статус
            StatusText.Text = "Режим баннера • Коснитесь экрана для возврата";
        }

        private void UpdateStatusText()
        {
            if (_isFullScreen)
                StatusText.Text = "Полноэкранный режим • F11 - оконный режим";
            else
                StatusText.Text = "Оконный режим • F11 - полноэкранный режим";
        }

        private async Task LoadBannerContent()
        {
            try
            {
                if (!string.IsNullOrEmpty(App.Settings.Bannerurl))
                {
                    // Загружаем из настроек приложения
                    BannerWebView.Source = new Uri(App.Settings.Bannerurl);
                }
                else
                {
                    // Используем запасной контент
                    SetFallbackBannerContent();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки баннера: {ex.Message}");
                SetFallbackBannerContent();
            }
        }

        private void UpdateDateTime()
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm:ss");
            DateText.Text = GetRussianDateString(now);
        }

        private string GetRussianDateString(DateTime date)
        {
            string[] daysOfWeek = { "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" };
            string[] months = { "января", "февраля", "марта", "апреля", "мая", "июня",
                              "июля", "августа", "сентября", "октября", "ноября", "декабря" };

            string dayOfWeek = daysOfWeek[(int)date.DayOfWeek];
            string month = months[date.Month - 1];

            return $"{dayOfWeek}, {date.Day} {month} {date.Year}";
        }

        private void UpdateAssistantInfo()
        {
            if (AIClassComboBox.SelectedItem == null || _scheduleData == null)
                return;

            var selectedClass = AIClassComboBox.SelectedItem.ToString();
            var currentTime = DateTime.Now;

            var assistantInfo = _assistantService.GetCurrentInfo(_scheduleData, _replacementData, selectedClass, currentTime);

            UpdateAIContent(assistantInfo, selectedClass, currentTime);
        }

        private void UpdateAIContent(AssistantInfo info, string className, DateTime currentTime)
        {
            AIContentPanel.Children.Clear();

            // Заголовок с классом
            AddAITitle($"Информация для {className}");

            // Текущее состояние
            AddCurrentState(info.CurrentState, currentTime);

            // Замены
            if (info.ClassReplacements.Any())
            {
                AddReplacementsInfo(info.ClassReplacements);
            }

            // Следующий урок
            if (info.NextLesson != null)
            {
                AddNextLessonInfo(info.NextLesson);
            }

            // Расписание на сегодня - ВСЕ уроки
            if (info.TodayLessons.Any())
            {
                AddTodaySchedule(info.TodayLessons);
            }
        }

        private void AddAITitle(string title)
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                TextAlignment = TextAlignment.Center
            };
            AIContentPanel.Children.Add(titleBlock);
        }

        private void AddCurrentState(CurrentState state, DateTime currentTime)
        {
            var statePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };

            if (state.IsLesson && state.CurrentLesson != null)
            {
                AddStateItem(statePanel, "📚 Сейчас идет:", $"{state.CurrentLesson.Number} урок - {state.CurrentLesson.Subject}");
                AddStateItem(statePanel, "⏰ До конца:", _assistantService.FormatTimeRemaining(state.TimeRemaining));
                AddStateItem(statePanel, "👨‍🏫 Учитель:", state.CurrentLesson.Teacher);
                AddStateItem(statePanel, "🚪 Кабинет:", state.CurrentLesson.Classroom);
            }
            else if (state.IsBreak && state.NextLesson != null)
            {
                AddStateItem(statePanel, "☕ Сейчас перемена", "");
                AddStateItem(statePanel, "⏰ До урока:", _assistantService.FormatTimeRemaining(state.TimeRemaining));
                AddStateItem(statePanel, "📚 Следующий:", $"{state.NextLesson.Number} урок - {state.NextLesson.Subject}");
                AddStateItem(statePanel, "👨‍🏫 Учитель:", state.NextLesson.Teacher);
                AddStateItem(statePanel, "🚪 Кабинет:", state.NextLesson.Classroom);
            }
            else if (state.IsSchoolOver)
            {
                AddStateItem(statePanel, "🎉 Уроки завершены", "Хорошего отдыха!");
            }
            else
            {
                AddStateItem(statePanel, "ℹ️ Нет информации", "Выберите другой класс или проверьте расписание");
            }

            AIContentPanel.Children.Add(statePanel);
        }

        private void AddStateItem(Panel parent, string label, string value)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = Brushes.LightBlue,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(labelBlock, 0);

            var valueBlock = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontSize = 12
            };
            Grid.SetColumn(valueBlock, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
            parent.Children.Add(grid);
        }

        private void AddReplacementsInfo(System.Collections.Generic.List<ReplacementLesson> replacements)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();

            var title = new TextBlock
            {
                Text = "🔄 Замены на сегодня:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(title);

            foreach (var replacement in replacements)
            {
                var replacementText = $"{replacement.LessonNumber} урок: {replacement.ReplacementTeacher}";
                if (!string.IsNullOrEmpty(replacement.Classroom) && replacement.Classroom != "-")
                    replacementText += $" ({replacement.Classroom})";

                if (!string.IsNullOrEmpty(replacement.Notes))
                    replacementText += $" - {replacement.Notes}";

                var textBlock = new TextBlock
                {
                    Text = replacementText,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    Margin = new Thickness(10, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(textBlock);
            }

            border.Child = stackPanel;
            AIContentPanel.Children.Add(border);
        }

        private void AddNextLessonInfo(Lesson nextLesson)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();

            var title = new TextBlock
            {
                Text = "➡️ Следующий урок:",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(title);

            var details = new TextBlock
            {
                Text = $"{nextLesson.Number} урок: {nextLesson.Subject}\n" +
                       $"Учитель: {nextLesson.Teacher}\n" +
                       $"Кабинет: {nextLesson.Classroom}",
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(details);

            border.Child = stackPanel;
            AIContentPanel.Children.Add(border);
        }

        private void AddTodaySchedule(System.Collections.Generic.List<Lesson> lessons)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();

            var title = new TextBlock
            {
                Text = $"📅 Расписание на сегодня ({lessons.Count} уроков):",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(title);

            foreach (var lesson in lessons.OrderBy(l => l.Number))
            {
                var lessonText = $"{lesson.Number}. {lesson.Time} - {lesson.Subject}";
                if (!string.IsNullOrEmpty(lesson.Teacher))
                    lessonText += $" ({lesson.Teacher})";
                if (!string.IsNullOrEmpty(lesson.Classroom))
                    lessonText += $" - {lesson.Classroom}";

                var textBlock = new TextBlock
                {
                    Text = lessonText,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    Margin = new Thickness(10, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(textBlock);
            }

            border.Child = stackPanel;
            AIContentPanel.Children.Add(border);
        }

        #region Обработчики явных действий пользователя

        // Эти обработчики выходят из режима баннера
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void Window_TouchDown(object sender, TouchEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void MainGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void MainGrid_TouchDown(object sender, TouchEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();

            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
            else if (e.Key == Key.Escape && _isFullScreen)
            {
                ToggleFullScreen();
            }
        }

        #endregion

        #region Обработчики баннера

        private void BannerOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void BannerWebView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        private void CloseBannerButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
        }

        #endregion

        #region Существующие обработчики кнопок (обновленные)

        private void MapButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            FloorPlanWindow floorPlanWindow = new FloorPlanWindow(this);
            floorPlanWindow.Show();
            this.Hide();
        }

        private void AIClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            UpdateAssistantInfo();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            var newsWindow = new NewsBrowserWindow();
            newsWindow.Show();
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            try
            {
                var scheduleWindow = new Views.ScheduleWindow();
                scheduleWindow.Owner = this;
                scheduleWindow.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии расписания: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReplacementsButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            try
            {
                var replacementsWindow = new Views.ReplacementsWindow();
                replacementsWindow.Owner = this;
                replacementsWindow.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии замен: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            try
            {
                if (App.Settings.ShowKeyboardForPassword)
                {
                    var passwordWindow = new PasswordWindow();
                    if (passwordWindow.ShowDialog() == true && passwordWindow.IsPasswordCorrect)
                    {
                        var settingsWindow = new SettingsWindow();
                        settingsWindow.Owner = this;
                        settingsWindow.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show("Неверный пароль", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    var simplePasswordDialog = new SimplePasswordDialog();
                    if (simplePasswordDialog.ShowDialog() == true && simplePasswordDialog.IsPasswordCorrect)
                    {
                        var settingsWindow = new SettingsWindow();
                        settingsWindow.Owner = this;
                        settingsWindow.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show("Неверный пароль", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии настроек: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerModeFromUserAction();
            ResetIdleTimer();
            try
            {
                var aboutWindow = new Views.AboutWindow();
                aboutWindow.Owner = this;
                aboutWindow.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии информации о проекте: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void ToggleFullScreen()
        {
            ResetIdleTimer();

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
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }
}