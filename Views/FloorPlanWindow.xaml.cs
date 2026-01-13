using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Threading;
using Kiosk.Models;
using System.Windows.Media.Animation;

namespace Kiosk
{
    public partial class FloorPlanWindow : Window
    {
        private MainWindow _mainWindow;
        private bool _isInitialized = false;

        // Переменные для управления масштабированием и перемещением
        private Point _lastDragPoint;
        private bool _isDragging = false;
        private double _startScale = 1.0;
        private Point _startTranslation;

        // Переменные для долгого нажатия
        private DispatcherTimer _longPressTimer;
        private const double LONG_PRESS_DELAY = 0.5; // 0.5 секунды
        private double _longPressTime = 0;
        private bool _isLongPressInProgress = false;
        private Point _longPressStartPoint;

        // Словарь с информацией о кабинетах
        private Dictionary<string, RoomInfo> _roomData;

        public FloorPlanWindow()
        {
            InitializeComponent();
            InitializeTimers();
            Loaded += OnLoaded;
            InitializeRoomData();
        }

        public FloorPlanWindow(MainWindow mainWindow) : this()
        {
            _mainWindow = mainWindow;
        }

        private void InitializeTimers()
        {
            // Таймер для долгого нажатия
            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(50);
            _longPressTimer.Tick += LongPressTimer_Tick;
        }

        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            _longPressTime += 0.05;

            // Обновляем прогресс
            double progress = _longPressTime / LONG_PRESS_DELAY;
            UpdateLongPressProgress(progress);

            if (_longPressTime >= LONG_PRESS_DELAY)
            {
                // Активируем перемещение
                _longPressTimer.Stop();
                _isLongPressInProgress = false;
                _isDragging = true;
                LongPressIndicator.Visibility = Visibility.Collapsed;
                TouchOperationIndicator.Visibility = Visibility.Visible;

                this.Cursor = Cursors.SizeAll;
            }
        }

        private void UpdateLongPressProgress(double progress)
        {
            // Обновляем текстовое отображение
            double remaining = LONG_PRESS_DELAY - _longPressTime;
            LongPressProgressText.Text = remaining.ToString("0.0");

            // Обновляем круговой прогресс
            double circumference = 2 * Math.PI * 30;
            double dashLength = progress * circumference;
            LongPressProgressEllipse.StrokeDashArray = new DoubleCollection { dashLength, circumference };
        }

        private void StartLongPressActivation(Point startPoint)
        {
            _isLongPressInProgress = true;
            _longPressTime = 0;
            _longPressStartPoint = startPoint;

            // Показываем индикатор
            LongPressIndicator.Visibility = Visibility.Visible;
            UpdateLongPressProgress(0);

            // Запускаем таймер
            _longPressTimer.Start();
        }

        private void CancelLongPressActivation()
        {
            _longPressTimer.Stop();
            _isLongPressInProgress = false;
            LongPressIndicator.Visibility = Visibility.Collapsed;
            TouchOperationIndicator.Visibility = Visibility.Collapsed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isInitialized = true;
            LoadFloor("Floor1.xaml");

            // Устанавливаем фокус на окно для обработки клавиш
            Focus();
        }

        // ЗАЩИТА КНОПОК ОТ ПЕРЕМЕЩЕНИЯ
        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Блокируем обработку мыши в ScrollViewer для кнопок
            if (e.OriginalSource is Button || e.OriginalSource is Slider || e.OriginalSource is ComboBox)
            {
                e.Handled = true;
            }
        }

        private void ScrollViewer_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            // Блокируем обработку касания в ScrollViewer для кнопок
            if (e.OriginalSource is Button || e.OriginalSource is Slider || e.OriginalSource is ComboBox)
            {
                e.Handled = true;
            }
        }

        private void FloorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (FloorSelector?.SelectedItem is ComboBoxItem item && item.Tag is string fileName)
            {
                LoadFloor(fileName);
            }
        }

        private void LoadFloor(string fileName)
        {
            try
            {
                var pageUri = new Uri($"/Kiosk;component/Pages/{fileName}", UriKind.Relative);
                FloorFrame.Source = pageUri;
                ResetZoomAndPosition();
            }
            catch (Exception)
            {
                ShowPlanInDevelopment();
            }
        }

        private void ShowPlanInDevelopment()
        {
            var page = new Page();
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };

            var iconText = new TextBlock
            {
                Text = "🚧",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var mainText = new TextBlock
            {
                Text = "План в разработке",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var subText = new TextBlock
            {
                Text = "Данный этаж находится в процессе создания",
                FontSize = 14,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            stackPanel.Children.Add(iconText);
            stackPanel.Children.Add(mainText);
            stackPanel.Children.Add(subText);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                Padding = new Thickness(40),
                Child = stackPanel
            };

            var grid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 42, 54)),
                Width = 600,
                Height = 400
            };
            grid.Children.Add(border);

            page.Content = grid;
            FloorFrame.Content = page;
            ResetZoomAndPosition();
        }

        // МАСШТАБИРОВАНИЕ
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || ViewboxScaleTransform == null) return;

            try
            {
                double scale = e.NewValue;
                ViewboxScaleTransform.ScaleX = scale;
                ViewboxScaleTransform.ScaleY = scale;
                UpdateZoomText();
            }
            catch { }
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || ZoomSlider == null) return;

            try
            {
                double newValue = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.2);
                ZoomSlider.Value = newValue;
            }
            catch { }
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || ZoomSlider == null) return;

            try
            {
                double newValue = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.2);
                ZoomSlider.Value = newValue;
            }
            catch { }
        }

        private void ResetZoomBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetZoomAndPosition();
        }

        private void ResetPanBtn_Click(object sender, RoutedEventArgs e)
        {
            ResetPan();
        }

        private void ResetPan()
        {
            if (!_isInitialized) return;

            try
            {
                if (ViewboxTranslateTransform != null)
                {
                    // Плавный сброс позиции
                    var animation = new DoubleAnimation(0,
                        TimeSpan.FromMilliseconds(300));
                    ViewboxTranslateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
                    ViewboxTranslateTransform.BeginAnimation(TranslateTransform.YProperty, animation);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (MainScrollViewer != null)
                        {
                            MainScrollViewer.ScrollToHorizontalOffset(0);
                            MainScrollViewer.ScrollToVerticalOffset(0);
                        }
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch { }
        }

        private void ResetZoomAndPosition()
        {
            if (!_isInitialized) return;

            try
            {
                if (ZoomSlider != null)
                {
                    ZoomSlider.Value = 1.0;
                }

                ResetPan();
            }
            catch { }
        }

        private void UpdateZoomText()
        {
            if (ZoomText != null && ZoomSlider != null)
            {
                ZoomText.Text = $"{(int)(ZoomSlider.Value * 100)}%";
            }
        }

        // МАСШТАБИРОВАНИЕ КОЛЕСИКОМ МЫШИ
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isInitialized || ZoomSlider == null) return;

            try
            {
                // Масштабирование относительно позиции мыши
                Point mousePos = e.GetPosition(ContentGrid);
                double zoomFactor = e.Delta > 0 ? 1.2 : 0.8;
                double newZoom = ZoomSlider.Value * zoomFactor;
                newZoom = Math.Max(ZoomSlider.Minimum, Math.Min(ZoomSlider.Maximum, newZoom));

                // Сохраняем старый масштаб для расчета смещения
                double oldZoom = ZoomSlider.Value;

                // Применяем новый масштаб
                ZoomSlider.Value = newZoom;

                // Корректируем позицию для масштабирования относительно курсора
                if (ViewboxTranslateTransform != null)
                {
                    double scaleChange = newZoom / oldZoom;
                    ViewboxTranslateTransform.X = mousePos.X - (mousePos.X - ViewboxTranslateTransform.X) * scaleChange;
                    ViewboxTranslateTransform.Y = mousePos.Y - (mousePos.Y - ViewboxTranslateTransform.Y) * scaleChange;
                }

                e.Handled = true;
            }
            catch { }
        }

        // ПЕРЕМЕЩЕНИЕ КАРТЫ - ТОЛЬКО ПКМ ИЛИ ДОЛГОЕ НАЖАТИЕ
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // ЛКМ - только для кликов по кабинетам, не для перемещения
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = false;
                return;
            }

            // ПКМ - немедленное перемещение
            if (e.ChangedButton == MouseButton.Right)
            {
                if (!_isInitialized) return;

                _lastDragPoint = e.GetPosition(ContentGrid);
                _isDragging = true;
                this.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isInitialized || !_isDragging) return;

            Point currentPosition = e.GetPosition(ContentGrid);
            Vector delta = currentPosition - _lastDragPoint;

            if (ViewboxTranslateTransform != null)
            {
                ViewboxTranslateTransform.X += delta.X;
                ViewboxTranslateTransform.Y += delta.Y;
            }

            _lastDragPoint = currentPosition;
            e.Handled = true;
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Left))
            {
                _isDragging = false;
                this.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        // ПРАВАЯ КНОПКА МЫШИ
        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;

            _lastDragPoint = e.GetPosition(ContentGrid);
            _isDragging = true;
            this.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        // СЕНСОРНОЕ УПРАВЛЕНИЕ - ДОЛГОЕ НАЖАТИЕ ДЛЯ ПЕРЕМЕЩЕНИЯ
        private void Window_TouchDown(object sender, TouchEventArgs e)
        {
            if (!_isInitialized) return;

            var touchPoint = e.GetTouchPoint(ContentGrid);
            _longPressStartPoint = touchPoint.Position;

            // Запускаем таймер для активации перемещения при долгом нажатии
            StartLongPressActivation(_longPressStartPoint);

            e.Handled = true;
        }

        private void Window_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_isInitialized) return;

            var touchPoint = e.GetTouchPoint(ContentGrid);
            Point currentPosition = touchPoint.Position;

            if (_isDragging)
            {
                // Перемещение карты
                Vector delta = currentPosition - _lastDragPoint;

                if (ViewboxTranslateTransform != null)
                {
                    ViewboxTranslateTransform.X += delta.X;
                    ViewboxTranslateTransform.Y += delta.Y;
                }

                _lastDragPoint = currentPosition;
                e.Handled = true;
            }
            else if (_isLongPressInProgress)
            {
                // Если палец сдвинулся слишком сильно до активации перемещения - отменяем
                if ((currentPosition - _longPressStartPoint).Length > 20)
                {
                    CancelLongPressActivation();
                }
            }
        }

        private void Window_TouchUp(object sender, TouchEventArgs e)
        {
            if (_isLongPressInProgress)
            {
                // Короткое касание - отменяем активацию перемещения
                CancelLongPressActivation();

                // Обрабатываем как клик по кабинету
                ProcessTouchAsClick(e.GetTouchPoint(ContentGrid).Position);
            }
            else if (_isDragging)
            {
                _isDragging = false;
                TouchOperationIndicator.Visibility = Visibility.Collapsed;
            }

            e.Handled = true;
        }

        private void ProcessTouchAsClick(Point position)
        {
            // Здесь можно добавить логику обработки клика по кабинетам
            // Например, поиск элемента под точкой касания
            // ShowRoomInfo(roomNumber);
        }

        // ЖЕСТЫ МАСШТАБИРОВАНИЯ (pinch-to-zoom)
        private void MainScrollViewer_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            if (!_isInitialized) return;

            e.ManipulationContainer = ContentGrid;
            e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
            _startScale = ViewboxScaleTransform.ScaleX;
            _startTranslation = new Point(ViewboxTranslateTransform.X, ViewboxTranslateTransform.Y);
            e.Handled = true;
        }

        private void MainScrollViewer_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                // Масштабирование жестом pinch
                if (e.DeltaManipulation.Scale.X != 1.0 || e.DeltaManipulation.Scale.Y != 1.0)
                {
                    double newScale = _startScale * e.DeltaManipulation.Scale.X;
                    newScale = Math.Max(ZoomSlider.Minimum, Math.Min(ZoomSlider.Maximum, newScale));

                    ViewboxScaleTransform.ScaleX = newScale;
                    ViewboxScaleTransform.ScaleY = newScale;
                    ZoomSlider.Value = newScale;
                }

                // Перемещение жестом
                if (e.DeltaManipulation.Translation.X != 0 || e.DeltaManipulation.Translation.Y != 0)
                {
                    ViewboxTranslateTransform.X = _startTranslation.X + e.DeltaManipulation.Translation.X;
                    ViewboxTranslateTransform.Y = _startTranslation.Y + e.DeltaManipulation.Translation.Y;
                }

                e.Handled = true;
            }
            catch { }
        }

        public void ShowRoomInfo(string roomNumber)
        {
            if (_roomData.ContainsKey(roomNumber))
            {
                var roomInfoWindow = new RoomInfoWindow(_roomData[roomNumber]);
                roomInfoWindow.Owner = this;
                roomInfoWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show($"Информация о кабинете {roomNumber} пока недоступна.",
                              "Информация",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.Show();
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitButton_Click(null, null);
            }
            else if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                ZoomInBtn_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                ZoomOutBtn_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                ResetZoomAndPosition();
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                ResetPan();
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                ResetZoomAndPosition();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }


        private void InitializeRoomData()
        {
            _roomData = new Dictionary<string, RoomInfo>
            {
                {
                    "107", new RoomInfo
                    {
                        RoomNumber = "107",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс, оснащенный последними технологиями для обучения программированию и компьютерным наукам.",
                        Responsible = "Иванов Иван Иванович",
                        Teacher = "Иванов Иван Иванович",
                        Phone = "+7 (495) 123-45-67",
                        Hours = "9:00 - 18:00",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 9:00-18:00, Сб: 10:00-15:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },
                {
                    "108", new RoomInfo
                    {
                        RoomNumber = "108",
                        Name = "Лаборатория физики",
                        Description = "Лаборатория оборудована для проведения экспериментов и практических работ по курсу физики.",
                        Responsible = "Петров Петр Петрович",
                        Teacher = "Петров Петр Петрович",
                        Phone = "+7 (495) 123-45-68",
                        Hours = "8:30 - 17:30",
                        Floor = "1",
                        Purpose = "Лаборатория",
                        Schedule = "Пн-Пт: 8:30-17:30",
                        CurrentLesson = "Механика",
                        AdditionalInfo = "Имеется оборудование для опытов по механике, оптике и электричеству"
                    }
                },
                {
                    "110", new RoomInfo
                    {
                        RoomNumber = "110",
                        Name = "Кабинет математики",
                        Description = "Учебный класс для занятий высшей математикой и аналитической геометрией.",
                        Responsible = "Сидорова Мария Ивановна",
                        Teacher = "Сидорова Мария Ивановна",
                        Phone = "+7 (495) 123-45-69",
                        Hours = "9:00 - 18:00",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 9:00-18:00",
                        CurrentLesson = "Линейная алгебра",
                        AdditionalInfo = "Оснащен проектором и маркерной доской"
                    }
                },
                {
                    "111", new RoomInfo
                    {
                        RoomNumber = "111",
                        Name = "Кабинет химии",
                        Description = "Специализированный класс для проведения химических экспериментов и лабораторных работ.",
                        Responsible = "Козлова Елена Викторовна",
                        Teacher = "Козлова Елена Викторовна",
                        Phone = "+7 (495) 123-45-70",
                        Hours = "9:00 - 17:00",
                        Floor = "1",
                        Purpose = "Лаборатория",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Органическая химия",
                        AdditionalInfo = "Оснащен вытяжными шкафами и лабораторным оборудованием"
                    }
                },
                {
                    "112", new RoomInfo
                    {
                        RoomNumber = "112",
                        Name = "Кабинет биологии",
                        Description = "Класс для изучения биологии, оснащенный микроскопами и наглядными пособиями.",
                        Responsible = "Николаев Алексей Сергеевич",
                        Teacher = "Николаев Алексей Сергеевич",
                        Phone = "+7 (495) 123-45-71",
                        Hours = "8:00 - 16:30",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 8:00-16:30",
                        CurrentLesson = "Ботаника",
                        AdditionalInfo = "Имеется коллекция гербариев и микроскопы"
                    }
                },
                {
                    "113", new RoomInfo
                    {
                        RoomNumber = "113",
                        Name = "Кабинет истории",
                        Description = "Учебный класс для занятий историей и обществознанием.",
                        Responsible = "Федоров Дмитрий Анатольевич",
                        Teacher = "Федоров Дмитрий Анатольевич",
                        Phone = "+7 (495) 123-45-72",
                        Hours = "9:00 - 18:00",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 9:00-18:00",
                        CurrentLesson = "История России",
                        AdditionalInfo = "Оснащен картами и историческими реконструкциями"
                    }
                },
                {
                    "114", new RoomInfo
                    {
                        RoomNumber = "114",
                        Name = "Кабинет географии",
                        Description = "Класс, оборудованный картами и глобусами для изучения географии.",
                        Responsible = "Смирнова Ольга Петровна",
                        Teacher = "Смирнова Ольга Петровна",
                        Phone = "+7 (495) 123-45-73",
                        Hours = "9:00 - 17:00",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Физическая география",
                        AdditionalInfo = "Имеется коллекция минералов и горных пород"
                    }
                },
                {
                    "115", new RoomInfo
                    {
                        RoomNumber = "115",
                        Name = "Кабинет литературы",
                        Description = "Уютный класс для изучения русской и зарубежной литературы.",
                        Responsible = "Васильева Татьяна Ивановна",
                        Teacher = "Васильева Татьяна Ивановна",
                        Phone = "+7 (495) 123-45-74",
                        Hours = "8:30 - 17:30",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 8:30-17:30",
                        CurrentLesson = "Русская классика",
                        AdditionalInfo = "Библиотека с произведениями русских и зарубежных авторов"
                    }
                },

                                {
                    "116", new RoomInfo
                    {
                        RoomNumber = "116",
                        Name = "Кабинет иностранных языков",
                        Description = "Специализированный класс для изучения иностранных языков с лингафонным оборудованием.",
                        Responsible = "Кузнецова Анна Сергеевна",
                        Teacher = "Кузнецова Анна Сергеевна",
                        Phone = "+7 (495) 123-45-75",
                        Hours = "8:00 - 17:00",
                        Floor = "1",
                        Purpose = "Учебный класс",
                        Schedule = "Пн-Пт: 8:00-17:00",
                        CurrentLesson = "Английский язык",
                        AdditionalInfo = "Оснащен лингафонным оборудованием и аудиоматериалами"
                    }
                },

                {
                    "202", new RoomInfo
                   {
                        RoomNumber = "202",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },

                 {
                    "203", new RoomInfo
                   {
                        RoomNumber = "203",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                  {
                    "204", new RoomInfo
                   {
                        RoomNumber = "204",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                   {
                    "205", new RoomInfo
                   {
                        RoomNumber = "205",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                    {
                    "206", new RoomInfo
                   {
                        RoomNumber = "206",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                     {
                    "207", new RoomInfo
                   {
                        RoomNumber = "207",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                      {
                    "208", new RoomInfo
                   {
                        RoomNumber = "208",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                       {
                    "209", new RoomInfo
                   {
                        RoomNumber = "209",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                        {
                    "210", new RoomInfo
                   {
                        RoomNumber = "210",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                         {
                    "212", new RoomInfo
                   {
                        RoomNumber = "212",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },
                          {
                    "213", new RoomInfo
                   {
                        RoomNumber = "213",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },

                 {
                    "214", new RoomInfo
                   {
                        RoomNumber = "214",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },

                  {
                    "211", new RoomInfo
                   {
                        RoomNumber = "211",
                        Name = "Кабинет технологии",
                        Description = "Специализированный класс для занятий технологией и трудовым обучением.",
                        Responsible = "Семенов Сергей Владимирович",
                        Teacher = "Семенов Сергей Владимирович",
                        Phone = "+7 (495) 123-45-76",
                        Hours = "9:00 - 17:00",
                        Floor = "2",
                        Purpose = "Мастерская",
                        Schedule = "Пн-Пт: 9:00-17:00",
                        CurrentLesson = "Технология обработки дерева",
                        AdditionalInfo = "Оснащен станками и инструментами для работы с деревом и металлом"
                    }
                },

                {
                    "301", new RoomInfo
                    {
                        RoomNumber = "301",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "302", new RoomInfo
                    {
                        RoomNumber = "302",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "303", new RoomInfo
                    {
                        RoomNumber = "303",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "304", new RoomInfo
                    {
                        RoomNumber = "304",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "305", new RoomInfo
                    {
                        RoomNumber = "305",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "306", new RoomInfo
                    {
                        RoomNumber = "306",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "307", new RoomInfo
                    {
                        RoomNumber = "307",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "308", new RoomInfo
                    {
                        RoomNumber = "308",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "309", new RoomInfo
                    {
                        RoomNumber = "309",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "310", new RoomInfo
                    {
                        RoomNumber = "310",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "311", new RoomInfo
                    {
                        RoomNumber = "311",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "312", new RoomInfo
                    {
                        RoomNumber = "312",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "313", new RoomInfo
                    {
                        RoomNumber = "313",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "314", new RoomInfo
                    {
                        RoomNumber = "314",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },


                {
                    "315", new RoomInfo
                    {
                        RoomNumber = "315",
                        Name = "Кабинет информатики",
                        Description = "Современный компьютерный класс для занятий программированием и ИТ-технологиями.",
                        Responsible = "Петров Алексей Иванович",
                        Teacher = "Петров Алексей Иванович",
                        Phone = "+7 (495) 123-45-78",
                        Hours = "8:00 - 18:00",
                        Floor = "3",
                        Purpose = "Компьютерный класс",
                        Schedule = "Пн-Пт: 8:00-18:00, Сб: 9:00-14:00",
                        CurrentLesson = "Программирование на C#",
                        AdditionalInfo = "Оснащен 15 компьютерами, проектором, интерактивной доской"
                    }
                },

                {
                    "401", new RoomInfo
                    {
                        RoomNumber = "401",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "402", new RoomInfo
                    {
                        RoomNumber = "402",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "403", new RoomInfo
                    {
                        RoomNumber = "403",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "404", new RoomInfo
                    {
                        RoomNumber = "404",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "405", new RoomInfo
                    {
                        RoomNumber = "405",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "406", new RoomInfo
                    {
                        RoomNumber = "406",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "407", new RoomInfo
                    {
                        RoomNumber = "407",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "408", new RoomInfo
                    {
                        RoomNumber = "408",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "409", new RoomInfo
                    {
                        RoomNumber = "409",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "410", new RoomInfo
                    {
                        RoomNumber = "410",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "411", new RoomInfo
                    {
                        RoomNumber = "411",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "Актовый зал", new RoomInfo
                    {
                        RoomNumber = "Актовый зал",
                        Name = "Актовый зал",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "Спорт зал 1", new RoomInfo
                    {
                        RoomNumber = "Спорт зал 1",
                        Name = "Спорт зал 1",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                },
                {
                    "Спорт зал 2", new RoomInfo
                    {
                        RoomNumber = "Спорт зал 2",
                        Name = "Спорт зал 2",
                        Description = "Просторное помещение для проведения мероприятий, собраний и концертов.",
                        Responsible = "Никитина Елена Владимировна",
                        Teacher = "Никитина Елена Владимировна",
                        Phone = "+7 (495) 123-45-81",
                        Hours = "8:00 - 20:00",
                        Floor = "4",
                        Purpose = "Мероприятия",
                        Schedule = "Пн-Вс: по предварительной записи",
                        CurrentLesson = "Репетиция школьного концерта",
                        AdditionalInfo = "Вместимость: 150 человек, есть сцена и мультимедийное оборудование"
                    }
                }
            };
        }
    }
}