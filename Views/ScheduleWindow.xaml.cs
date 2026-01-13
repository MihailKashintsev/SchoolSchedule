using Kiosk.Models;
using Kiosk.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kiosk.Views
{
    public partial class ScheduleWindow : Window
    {
        private readonly JsonScheduleService _scheduleService = new();
        private ScheduleData? _scheduleData;
        private List<DisplayDay>? _currentDisplayDays;
        private DispatcherTimer? _refreshTimer;
        private bool _isFullScreen = true;

        public ScheduleWindow()
        {
            InitializeComponent();
            InitializeRefreshTimer();
            Loaded += ScheduleWindow_Loaded;

            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
        }

        private void InitializeRefreshTimer()
        {
            if (App.Settings.AutoRefresh)
            {
                _refreshTimer = new DispatcherTimer();
                _refreshTimer.Interval = TimeSpan.FromSeconds(App.Settings.RefreshInterval);
                _refreshTimer.Tick += RefreshTimer_Tick;
                _refreshTimer.Start();
            }
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            await LoadScheduleAsync();
        }

        private async void ScheduleWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadScheduleAsync();
        }

        private async System.Threading.Tasks.Task LoadScheduleAsync()
        {
            StatusText.Text = "Загрузка расписания...";

            try
            {
                _scheduleData = await _scheduleService.LoadScheduleAsync(App.Settings.ScheduleFilePath);

                if (_scheduleData?.Schedules != null && _scheduleData.Schedules.Any())
                {
                    UpdateClassComboBox();
                    WeekTypeText.Text = GetWeekTypeDisplay(_scheduleData.WeekType);
                    UpdateTimeText.Text = $" • Обновлено: {FormatUpdateTime(_scheduleData.LastUpdated)}";
                    StatusText.Text = $"Расписание загружено ({_scheduleData.Schedules.Count} классов)";
                }
                else
                {
                    StatusText.Text = "Расписание не найдено";
                    ShowEmptySchedule();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка загрузки: {ex.Message}";
                ShowEmptySchedule();
            }
        }

        private string GetWeekTypeDisplay(string weekType)
        {
            return weekType?.ToLower() switch
            {
                "odd" => "Нечетная неделя",
                "even" => "Четная неделя",
                _ => "Текущая неделя"
            };
        }

        private string FormatUpdateTime(string lastUpdated)
        {
            if (DateTime.TryParse(lastUpdated, out DateTime updated))
            {
                return updated.ToString("dd.MM.yyyy HH:mm");
            }
            return DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }

        private void ShowEmptySchedule()
        {
            ClassComboBox.Items.Clear();
            ClassComboBox.Items.Add("Нет данных");
            ClassComboBox.SelectedIndex = 0;
            ClearScheduleContainer();
            NoLessonsText.Visibility = Visibility.Visible;
        }

        private void UpdateClassComboBox()
        {
            ClassComboBox.Items.Clear();

            if (_scheduleData?.Schedules != null && _scheduleData.Schedules.Any())
            {
                foreach (var schedule in _scheduleData.Schedules.OrderBy(s => s.ClassName))
                {
                    ClassComboBox.Items.Add(schedule.ClassName);
                }
                ClassComboBox.SelectedIndex = 0;
            }
            else
            {
                ClassComboBox.Items.Add("Нет данных");
                ClassComboBox.SelectedIndex = 0;
            }
        }

        private void ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassComboBox.SelectedItem != null && _scheduleData?.Schedules != null)
            {
                var selectedClassName = ClassComboBox.SelectedItem.ToString()!;
                var selectedClass = _scheduleData.Schedules.FirstOrDefault(s => s.ClassName == selectedClassName);
                if (selectedClass != null)
                {
                    _currentDisplayDays = _scheduleService.GetDisplayDays(selectedClass);
                    UpdateDayButtons();
                    ShowSelectedDay();
                }
            }
        }

        private void UpdateDayButtons()
        {
            if (_currentDisplayDays == null) return;

            var buttons = new[] { MondayButton, TuesdayButton, WednesdayButton, ThursdayButton, FridayButton, SaturdayButton };

            for (int i = 0; i < Math.Min(buttons.Length, _currentDisplayDays.Count); i++)
            {
                var day = _currentDisplayDays[i];
                var button = buttons[i];

                if (day.IsToday)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(23, 162, 184));
                    button.Foreground = Brushes.White;
                }
                else if (day.IsSelected)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(44, 95, 158));
                    button.Foreground = Brushes.White;
                }
                else
                {
                    button.Background = Brushes.LightGray;
                    button.Foreground = Brushes.Black;
                }
            }
        }

        private void ShowSelectedDay()
        {
            if (_currentDisplayDays == null || _scheduleData == null) return;

            var selectedDay = _currentDisplayDays.FirstOrDefault(d => d.IsSelected);
            if (selectedDay != null)
            {
                var scheduleItems = _scheduleService.GetScheduleItemsWithBreaks(
                    selectedDay.Lessons,
                    _scheduleData.BreakSettings
                );

                // Очищаем контейнер
                ClearScheduleContainer();

                // Добавляем элементы вручную
                foreach (var item in scheduleItems)
                {
                    if (item.IsBreak)
                    {
                        AddBreakItem(item);
                    }
                    else
                    {
                        AddLessonItem(item);
                    }
                }

                NoLessonsText.Visibility = selectedDay.HasNoLessons ? Visibility.Visible : Visibility.Collapsed;
                ScheduleContainer.Visibility = selectedDay.HasNoLessons ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void ClearScheduleContainer()
        {
            ScheduleContainer.Children.Clear();
        }

        private void AddLessonItem(ScheduleItem item)
        {
            var lesson = item.Lesson;
            if (lesson == null) return;

            // Создаем Border для урока
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid { Margin = new Thickness(15, 10, 15, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            // Номер и время
            var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            leftPanel.Children.Add(new TextBlock
            {
                Text = lesson.Number.ToString(),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 95, 158))
            });
            leftPanel.Children.Add(new TextBlock
            {
                Text = lesson.Time,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125))
            });
            Grid.SetColumn(leftPanel, 0);

            // Предмет и учитель
            var middlePanel = new StackPanel();
            middlePanel.Children.Add(new TextBlock
            {
                Text = lesson.Subject,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });
            middlePanel.Children.Add(new TextBlock
            {
                Text = lesson.Teacher,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87))
            });
            Grid.SetColumn(middlePanel, 1);

            // Кабинет
            var classroomBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(233, 236, 239)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            classroomBorder.Child = new TextBlock
            {
                Text = lesson.Classroom,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87))
            };
            Grid.SetColumn(classroomBorder, 2);

            grid.Children.Add(leftPanel);
            grid.Children.Add(middlePanel);
            grid.Children.Add(classroomBorder);

            border.Child = grid;
            ScheduleContainer.Children.Add(border);
        }

        private void AddBreakItem(ScheduleItem item)
        {
            // Создаем Border для перемены
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(232, 244, 253)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(182, 215, 242)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Height = 50
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = item.BreakText,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 95, 158)),
                Margin = new Thickness(0, 0, 10, 0)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"{item.BreakDuration} мин",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125))
            });

            border.Child = stackPanel;
            ScheduleContainer.Children.Add(border);
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDisplayDays == null) return;

            var button = (Button)sender;
            var dayIndex = button.Content.ToString() switch
            {
                "ПН" => 0,
                "ВТ" => 1,
                "СР" => 2,
                "ЧТ" => 3,
                "ПТ" => 4,
                "СБ" => 5,
                _ => 0
            };

            // Сбрасываем выделение всех дней
            foreach (var day in _currentDisplayDays)
            {
                day.IsSelected = false;
            }

            // Устанавливаем выбранный день
            if (dayIndex < _currentDisplayDays.Count)
            {
                _currentDisplayDays[dayIndex].IsSelected = true;
                UpdateDayButtons();
                ShowSelectedDay();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
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

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnClosed(e);
        }
    }
}