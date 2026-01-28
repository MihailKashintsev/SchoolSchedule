using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Статический словарь для управления окнами
        private static Dictionary<Type, Window> _openWindows = new Dictionary<Type, Window>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimers();
            UpdateDateTime();
            LoadData();
            LoadBannerSettings();

            // Устанавливаем обработчики событий пользовательской активности
            this.PreviewMouseMove += Window_PreviewMouseMove;
            this.PreviewMouseDown += Window_PreviewMouseDown;
            this.PreviewKeyDown += Window_PreviewKeyDown;
            this.PreviewTouchDown += Window_PreviewTouchDown;
            this.PreviewTouchMove += Window_PreviewTouchMove;
            Loaded += MainWindow_Loaded;

            // Начальное время активности
            _lastUserActivity = DateTime.Now;

            // Set fullscreen mode
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Создаем и запускаем анимации для кнопок
            CreateButtonAnimations();
        }

        private void CreateButtonAnimations()
        {
            // Анимация для кнопки "Расписание" (синяя)
            _scheduleBrush = CreateAnimatedBrush(
                Color.FromRgb(44, 95, 158),   // Темно-синий (#2c5f9e)
                Color.FromRgb(52, 152, 219),  // Светло-синий (#3498db)
                Color.FromRgb(41, 128, 185),  // Синий (#2980b9)
                Color.FromRgb(26, 74, 122)    // Очень темно-синий (#1a4a7a)
            );

            // Применяем анимированную кисть к кнопке "Расписание"
            if (ScheduleButton.Template.FindName("border", ScheduleButton) is Border scheduleBorder)
            {
                scheduleBorder.Background = _scheduleBrush;
            }

            // Анимация для кнопки "Замены" (оранжевая лава)
            _replacementsBrush = CreateAnimatedBrush(
                Color.FromRgb(255, 230, 126),  // Светло-оранжевый (#FFE67E)
                Color.FromRgb(255, 165, 0),    // Оранжевый (#FFA500)
                Color.FromRgb(255, 69, 0),     // Красный (#FF4500)
                Color.FromRgb(255, 215, 0)     // Золотой (#FFD700)
            );

            // Применяем анимированную кисть к кнопке "Замены"
            if (ReplacementsButton.Template.FindName("border", ReplacementsButton) is Border replacementsBorder)
            {
                replacementsBorder.Background = _replacementsBrush;
            }

            // Анимация для кнопки "О проекте" (фиолетово-красная)
            _aboutBrush = CreateAnimatedBrush(
                Color.FromRgb(155, 89, 182),   // Фиолетовый (#9b59b6)
                Color.FromRgb(231, 76, 60),    // Красный (#e74c3c)
                Color.FromRgb(243, 156, 18),   // Оранжевый (#f39c12)
                Color.FromRgb(142, 68, 173)    // Темно-фиолетовый (#8e44ad)
            );

            if (AboutButton.Template.FindName("border", AboutButton) is Border aboutBorder)
            {
                aboutBorder.Background = _aboutBrush;
            }

            // Анимация для кнопки "Карта здания" (зелено-синяя)
            _mapBrush = CreateAnimatedBrush(
                Color.FromRgb(39, 174, 96),    // Зеленый (#27ae60)
                Color.FromRgb(46, 204, 113),   // Светло-зеленый (#2ecc71)
                Color.FromRgb(52, 152, 219),   // Синий (#3498db)
                Color.FromRgb(34, 153, 84)     // Темно-зеленый (#229954)
            );

            if (MapBrowserButton.Template.FindName("border", MapBrowserButton) is Border mapBorder)
            {
                mapBorder.Background = _mapBrush;
            }

            // Анимация для кнопки "Новости" (сине-фиолетовая)
            _newsBrush = CreateAnimatedBrush(
                Color.FromRgb(67, 97, 238),    // Синий (#4361ee)
                Color.FromRgb(58, 12, 163),    // Темно-синий (#3a0ca3)
                Color.FromRgb(114, 9, 183),    // Фиолетовый (#7209b7)
                Color.FromRgb(247, 37, 133)    // Розовый (#f72585)
            );

            if (News.Template.FindName("border", News) is Border newsBorder)
            {
                newsBorder.Background = _newsBrush;
            }
        }

        private LinearGradientBrush CreateAnimatedBrush(Color color1, Color color2, Color color3, Color color4)
        {
            // Создаем градиентную кисть
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };

            // Создаем градиентные остановки
            GradientStop stop1 = new GradientStop(color1, 0);
            GradientStop stop2 = new GradientStop(color1, 0.5);
            GradientStop stop3 = new GradientStop(color3, 1);

            brush.GradientStops.Add(stop1);
            brush.GradientStops.Add(stop2);
            brush.GradientStops.Add(stop3);

            // Создаем анимации
            ColorAnimation animation1 = new ColorAnimation
            {
                From = color1,
                To = color2,
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            ColorAnimation animation2 = new ColorAnimation
            {
                From = color3,
                To = color4,
                Duration = TimeSpan.FromSeconds(3),
                BeginTime = TimeSpan.FromSeconds(1),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Запускаем анимации
            stop1.BeginAnimation(GradientStop.ColorProperty, animation1);
            stop2.BeginAnimation(GradientStop.ColorProperty, animation2);

            return brush;
        }

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
                    {
                        _bannerImages.Add(path);
                    }
                    else
                    {
                        // Попробуем найти файл в папке приложения
                        var fileName = Path.GetFileName(path);
                        var appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                        if (File.Exists(appPath))
                        {
                            _bannerImages.Add(appPath);
                        }
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

            // Таймер бездействия для показа баннеров
            _idleTimer = new DispatcherTimer();
            _idleTimer.Tick += IdleTimer_Tick;

            // Таймер переключения баннеров
            _bannerTimer = new DispatcherTimer();
            _bannerTimer.Tick += BannerTimer_Tick;
        }

        private void ResetIdleTimer()
        {
            if (_isBannerMode) return;

            _lastUserActivity = DateTime.Now;
            _idleTimer.Stop();

            if (_bannerImages.Count > 0 && App.Settings.EnableBanners)
            {
                _idleTimer.Start();
            }
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastUserActivity).TotalSeconds >= App.Settings.BannerTimeout)
            {
                StartBannerMode();
            }
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

            // Показываем баннер с плавной анимацией
            BannerGrid.Visibility = Visibility.Visible;
            _currentBannerIndex = 0;

            // Плавное появление баннера с затемнением
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            BannerGrid.BeginAnimation(OpacityProperty, fadeInAnimation);

            // Показываем первый баннер
            await ShowCurrentBannerWithAnimation();

            // Запускаем таймер переключения
            _bannerTimer.Start();
        }

        private async void ExitBannerMode()
        {
            if (!_isBannerMode) return;

            _isBannerMode = false;
            _bannerTimer.Stop();

            // Плавное исчезновение баннера
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                BannerGrid.Visibility = Visibility.Collapsed;
                BannerImage.Source = null; // Очищаем изображение
                BannerGrid.Opacity = 0;
            };

            BannerGrid.BeginAnimation(OpacityProperty, fadeOutAnimation);

            // Сбрасываем таймер бездействия
            ResetIdleTimer();
        }

        private async System.Threading.Tasks.Task ShowCurrentBannerWithAnimation()
        {
            if (_currentBannerIndex < 0 || _currentBannerIndex >= _bannerImages.Count) return;

            try
            {
                // Анимация исчезновения текущего баннера (если он есть)
                if (BannerImage.Source != null)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = BannerImage.Opacity,
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.3),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };

                    BannerImage.BeginAnimation(OpacityProperty, fadeOut);

                    // Ждем завершения анимации
                    await System.Threading.Tasks.Task.Delay(300);
                }

                // Загружаем новое изображение
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_bannerImages[_currentBannerIndex], UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                // Применяем изображение
                BannerImage.Source = bitmap;

                // Анимация появления нового баннера
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };

                BannerImage.BeginAnimation(OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки баннера: {ex.Message}");
                // Переходим к следующему баннеру при ошибке
                _currentBannerIndex = (_currentBannerIndex + 1) % _bannerImages.Count;
                await ShowCurrentBannerWithAnimation();
            }
        }

        private async void ShowNextBannerWithAnimation()
        {
            if (_bannerImages.Count == 0) return;

            _currentBannerIndex = (_currentBannerIndex + 1) % _bannerImages.Count;
            await ShowCurrentBannerWithAnimation();
        }

        // Обработчики пользовательской активности
        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isBannerMode)
            {
                // Не выходим из режима баннеров при движении мыши, только при клике
                return;
            }
            ResetIdleTimer();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isBannerMode)
            {
                // Клик в любом месте экрана выходит из режима баннеров
                ExitBannerMode();
            }
            else
            {
                ResetIdleTimer();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isBannerMode)
            {
                // Любая клавиша выходит из режима баннеров
                ExitBannerMode();
            }
            else
            {
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
        }

        private void Window_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (_isBannerMode)
            {
                ExitBannerMode();
            }
            else
            {
                ResetIdleTimer();
            }
        }

        private void Window_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (_isBannerMode) return;
            ResetIdleTimer();
        }

        private void ExitBannerButton_Click(object sender, RoutedEventArgs e)
        {
            ExitBannerMode();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateDateTime();
        }

        private void AITimer_Tick(object sender, EventArgs e)
        {
            UpdateAssistantInfo();
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
                AddStateItem(statePanel, "🎉Уроки завершены", "Хорошего отдыха!");
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

        private void AddReplacementsInfo(List<ReplacementLesson> replacements)
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

            foreach (var replacement in replacements) // Показываем ВСЕ замены
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

        private void AddTodaySchedule(List<Lesson> lessons)
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

            // Показываем ВСЕ уроки, а не только первые 5
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

        private void AIClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAssistantInfo();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrFocusWindow<NewsBrowserWindow>();
        }

        private void MapBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrFocusWindow<BrowserMap>();
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrFocusWindow<Views.ScheduleWindow>();
        }

        private void ReplacementsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrFocusWindow<Views.ReplacementsWindow>();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
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
                        // Обновляем настройки баннеров после закрытия окна настроек
                        LoadBannerSettings();
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
                        // Обновляем настройки баннеров после закрытия окна настроек
                        LoadBannerSettings();
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isBannerMode)
            {
                ExitBannerMode();
                return;
            }

            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
            else if (e.Key == Key.Escape && _isFullScreen)
            {
                ToggleFullScreen();
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOrFocusWindow<Views.AboutWindow>();
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

        // Метод для открытия или фокусировки окна
        private void OpenOrFocusWindow<T>() where T : Window, new()
        {
            try
            {
                // Проверяем, есть ли уже открытое окно этого типа
                if (_openWindows.ContainsKey(typeof(T)))
                {
                    var window = _openWindows[typeof(T)];
                    if (window != null && window.IsVisible)
                    {
                        // Активируем существующее окно
                        window.Activate();
                        window.WindowState = WindowState.Normal;
                        window.Focus();
                        return;
                    }
                    else
                    {
                        // Удаляем недействительное окно
                        _openWindows.Remove(typeof(T));
                    }
                }

                // Создаем новое окно
                var newWindow = new T();
                newWindow.Owner = this;

                // Обработчик закрытия окна
                newWindow.Closed += (s, args) =>
                {
                    if (_openWindows.ContainsKey(typeof(T)))
                    {
                        _openWindows.Remove(typeof(T));
                    }
                };

                // Добавляем в словарь и показываем
                _openWindows[typeof(T)] = newWindow;
                newWindow.Show();

                // Скрываем главное окно
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчик для показа главного окна при закрытии дочерних
        public void ShowMainWindow()
        {
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Закрываем все дочерние окна
            foreach (var window in _openWindows.Values.ToList())
            {
                try
                {
                    window?.Close();
                }
                catch { }
            }

            _timer?.Stop();
            _aiTimer?.Stop();
            _idleTimer?.Stop();
            _bannerTimer?.Stop();

            // Очищаем кэш WebView2
            ClearWebView2Cache();

            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private async void ClearWebView2Cache()
        {
            try
            {
                var cacheFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KioskApp",
                    "WebView2Cache");

                if (Directory.Exists(cacheFolder))
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            // Пробуем несколько раз удалить файлы
                            for (int i = 0; i < 3; i++)
                            {
                                try
                                {
                                    Directory.Delete(cacheFolder, true);
                                    break;
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(100);
                                }
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }
    }
}