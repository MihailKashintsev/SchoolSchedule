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
    public partial class ReplacementsWindow : Window
    {
        private readonly DocxReplacementService _replacementService = new();
        private ReplacementData _replacementData = new();
        private List<ClassReplacement> _classReplacements = new();
        private DispatcherTimer _refreshTimer;
        private bool _isFullScreen = true;

        public ReplacementsWindow()
        {
            InitializeComponent();
            InitializeRefreshTimer();
            Loaded += ReplacementsWindow_Loaded;

            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
        }

        private void InitializeRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(5); // Обновление каждые 5 минут
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            await LoadReplacementsAsync();
        }

        private async void ReplacementsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadReplacementsAsync();
        }

        private async System.Threading.Tasks.Task LoadReplacementsAsync()
        {
            StatusText.Text = "Загрузка замен...";

            try
            {
                _replacementData = _replacementService.LoadReplacements(App.Settings.ReplacementsFilePath);

                if (_replacementData != null && _replacementData.HasReplacements)
                {
                    _classReplacements = _replacementService.GetReplacementsByClass(_replacementData);
                    UpdateClassComboBox();
                    DateText.Text = $"Замены на {_replacementData.Date}";

                    // Отладочная информация
                    int totalLessons = _replacementData.Sections.Sum(s => s.Lessons.Count);
                    StatusText.Text = $"Загружено: {_replacementData.Sections.Count} разделов, {totalLessons} замен";

                    ShowReplacementsForSelectedClass();
                }
                else
                {
                    StatusText.Text = "Замен на сегодня нет";
                    ShowNoReplacements();

                    // Информация об отсутствии замен
                    ReplacementsContainer.Children.Clear();
                    var infoText = new TextBlock
                    {
                        Text = "✅ На сегодня замены уроков отсутствуют\n\nВсе уроки проводятся по расписанию",
                        FontSize = 20,
                        Foreground = Brushes.Green,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    ReplacementsContainer.Children.Add(infoText);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка загрузки: {ex.Message}";
                ShowNoReplacements();

                // Показываем ошибку пользователю
                ReplacementsContainer.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Ошибка загрузки файла замен:\n{ex.Message}\n\nПроверьте путь к файлу и его формат",
                    FontSize = 16,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                ReplacementsContainer.Children.Add(errorText);
            }
        }

        private void UpdateClassComboBox()
        {
            ClassComboBox.Items.Clear();

            if (_classReplacements.Any())
            {
                ClassComboBox.Items.Add("Все классы");
                foreach (var classReplacement in _classReplacements)
                {
                    ClassComboBox.Items.Add(classReplacement.ClassName);
                }
                ClassComboBox.SelectedIndex = 0;
            }
            else
            {
                ClassComboBox.Items.Add("Нет данных");
                ClassComboBox.SelectedIndex = 0;
            }
        }

        private void ShowNoReplacements()
        {
            ReplacementsContainer.Children.Clear();
            NoReplacementsText.Visibility = Visibility.Visible;
            ReplacementsContainer.Visibility = Visibility.Collapsed;
        }

        private void ClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowReplacementsForSelectedClass();
        }

        private void ShowReplacementsForSelectedClass()
        {
            ReplacementsContainer.Children.Clear();

            if (ClassComboBox.SelectedItem == null || !_classReplacements.Any())
            {
                ShowNoReplacements();
                return;
            }

            var selectedClass = ClassComboBox.SelectedItem.ToString();
            List<ClassReplacement> classesToShow;

            if (selectedClass == "Все классы")
            {
                classesToShow = _classReplacements;
            }
            else
            {
                classesToShow = _classReplacements
                    .Where(c => c.ClassName == selectedClass)
                    .ToList();
            }

            if (!classesToShow.Any())
            {
                ShowNoReplacements();
                return;
            }

            NoReplacementsText.Visibility = Visibility.Collapsed;
            ReplacementsContainer.Visibility = Visibility.Visible;

            foreach (var classReplacement in classesToShow)
            {
                AddClassReplacements(classReplacement);
            }
        }

        private void AddClassReplacements(ClassReplacement classReplacement)
        {
            // Заголовок класса
            var classHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Margin = new Thickness(0, 20, 0, 5),
                Padding = new Thickness(15, 10, 15, 10)
            };

            classHeader.Child = new TextBlock
            {
                Text = $"Класс: {classReplacement.ClassName}",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };

            ReplacementsContainer.Children.Add(classHeader);

            // Контейнер для замен
            var replacementsGrid = new Grid();
            replacementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // №
            replacementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Учитель
            replacementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Кабинет
            replacementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Примечания

            // Заголовки столбцов
            AddGridHeader(replacementsGrid, "№", 0);
            AddGridHeader(replacementsGrid, "Заменяющий учитель", 1);
            AddGridHeader(replacementsGrid, "Кабинет", 2);
            AddGridHeader(replacementsGrid, "Примечания", 3);

            // Данные замен
            int rowIndex = 1;
            foreach (var replacement in classReplacement.Replacements.OrderBy(r => r.LessonNumber))
            {
                AddReplacementRow(replacementsGrid, replacement, rowIndex);
                rowIndex++;
            }

            ReplacementsContainer.Children.Add(replacementsGrid);
        }

        private void AddGridHeader(Grid grid, string text, int column)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8)
            };

            header.Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Grid.SetColumn(header, column);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);
        }

        private void AddReplacementRow(Grid grid, ReplacementLesson replacement, int rowIndex)
        {
            // Добавляем строку в сетку
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Номер урока
            var lessonBorder = CreateCellBorder(rowIndex % 2 == 0);
            lessonBorder.Child = new TextBlock
            {
                Text = replacement.LessonNumber.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lessonBorder, 0);
            Grid.SetRow(lessonBorder, rowIndex);
            grid.Children.Add(lessonBorder);

            // Заменяющий учитель
            var teacherBorder = CreateCellBorder(rowIndex % 2 == 0);
            teacherBorder.Child = new TextBlock
            {
                Text = replacement.ReplacementTeacher,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(teacherBorder, 1);
            Grid.SetRow(teacherBorder, rowIndex);
            grid.Children.Add(teacherBorder);

            // Кабинет
            var classroomBorder = CreateCellBorder(rowIndex % 2 == 0);
            classroomBorder.Child = new TextBlock
            {
                Text = replacement.Classroom,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(classroomBorder, 2);
            Grid.SetRow(classroomBorder, rowIndex);
            grid.Children.Add(classroomBorder);

            // Примечания
            var notesBorder = CreateCellBorder(rowIndex % 2 == 0);
            var notesText = new TextBlock
            {
                Text = replacement.Notes,
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(notesBorder, 3);
            Grid.SetRow(notesBorder, rowIndex);
            grid.Children.Add(notesBorder);
        }

        private Border CreateCellBorder(bool isEven)
        {
            return new Border
            {
                Background = isEven ? Brushes.White : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 8, 5, 8)
            };
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