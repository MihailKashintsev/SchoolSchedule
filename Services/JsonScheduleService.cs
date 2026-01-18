using Kiosk.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Kiosk.Services
{
    public class JsonScheduleService
    {
        public async Task<ScheduleData> LoadScheduleAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show($"Файл расписания не найден: {filePath}", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                        return CreateEmptySchedule();
                    }

                    string json = File.ReadAllText(filePath);
                    var scheduleData = JsonConvert.DeserializeObject<ScheduleData>(json);

                    if (scheduleData?.Schedules == null)
                    {
                        MessageBox.Show("Неверный формат файла расписания", "Ошибка",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return CreateEmptySchedule();
                    }

                    return scheduleData;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки расписания: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return CreateEmptySchedule();
                }
            });
        }

        private ScheduleData CreateEmptySchedule()
        {
            return new ScheduleData
            {
                LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                WeekType = "current",
                Schedules = new List<ClassSchedule>(),
                BreakSettings = new BreakSettings()
            };
        }

        public List<DisplayDay> GetDisplayDays(ClassSchedule classSchedule)
        {
            var displayDays = new List<DisplayDay>();
            var today = DateTime.Today.DayOfWeek;

            var daysMap = new[]
            {
                new { FullName = "Понедельник", ShortName = "Пн", Property = classSchedule.Days.Monday, DayOfWeek = DayOfWeek.Monday },
                new { FullName = "Вторник", ShortName = "Вт", Property = classSchedule.Days.Tuesday, DayOfWeek = DayOfWeek.Tuesday },
                new { FullName = "Среда", ShortName = "Ср", Property = classSchedule.Days.Wednesday, DayOfWeek = DayOfWeek.Wednesday },
                new { FullName = "Четверг", ShortName = "Чт", Property = classSchedule.Days.Thursday, DayOfWeek = DayOfWeek.Thursday },
                new { FullName = "Пятница", ShortName = "Пт", Property = classSchedule.Days.Friday, DayOfWeek = DayOfWeek.Friday },
                new { FullName = "Суббота", ShortName = "Сб", Property = classSchedule.Days.Saturday, DayOfWeek = DayOfWeek.Saturday }
            };

            foreach (var day in daysMap)
            {
                // Фильтруем пустые уроки и уроки без предмета
                var filteredLessons = (day.Property ?? new List<Lesson>())
                    .Where(lesson => !IsEmptyLesson(lesson))
                    .ToList();

                displayDays.Add(new DisplayDay
                {
                    DayName = day.FullName,
                    ShortName = day.ShortName,
                    Lessons = filteredLessons,
                    IsToday = day.DayOfWeek == today
                });
            }

            // Set first day with lessons as selected by default
            var firstDayWithLessons = displayDays.FirstOrDefault(d => d.Lessons.Any());
            if (firstDayWithLessons != null)
                firstDayWithLessons.IsSelected = true;
            else if (displayDays.Count > 0)
                displayDays[0].IsSelected = true;

            return displayDays;
        }

        private bool IsEmptyLesson(Lesson lesson)
        {
            return string.IsNullOrWhiteSpace(lesson.Subject) &&
                   string.IsNullOrWhiteSpace(lesson.Teacher) &&
                   string.IsNullOrWhiteSpace(lesson.Classroom) &&
                   string.IsNullOrWhiteSpace(lesson.Time);
        }

        public List<ScheduleItem> GetScheduleItemsWithBreaks(List<Lesson> lessons, BreakSettings breakSettings)
        {
            var items = new List<ScheduleItem>();

            if (lessons == null || !lessons.Any())
                return items;

            // Сортируем уроки по номеру и фильтруем пустые
            var orderedLessons = lessons
                .Where(lesson => !IsEmptyLesson(lesson))
                .OrderBy(l => l.Number)
                .ToList();

            for (int i = 0; i < orderedLessons.Count; i++)
            {
                var lesson = orderedLessons[i];

                // Добавляем урок
                items.Add(new ScheduleItem
                {
                    IsBreak = false,
                    Lesson = lesson
                });

                // Добавляем перемену после урока (кроме последнего)
                if (i < orderedLessons.Count - 1)
                {
                    int breakDuration = GetBreakDuration(lesson.Number, breakSettings);

                    // Добавляем перемену только если ее продолжительность больше 0
                    if (breakDuration > 0)
                    {
                        items.Add(new ScheduleItem
                        {
                            IsBreak = true,
                            BreakText = GetBreakText(breakDuration),
                            BreakDuration = breakDuration
                        });
                    }
                }
            }

            return items;
        }

        private int GetBreakDuration(int lessonNumber, BreakSettings breakSettings)
        {
            return lessonNumber switch
            {
                1 => breakSettings.Break1Duration,
                2 => breakSettings.Break2Duration,
                3 => breakSettings.Break3Duration,
                4 => breakSettings.Break4Duration,
                5 => breakSettings.Break5Duration,
                6 => breakSettings.Break6Duration,
                7 => breakSettings.Break7Duration,
                _ => 10 // значение по умолчанию
            };
        }

        private string GetBreakText(int duration)
        {
            if (duration >= 20)
                return $"Большая перемена 🏃 ({duration} мин)";
            else if (duration >= 15)
                return $"Перемена 🕒 ({duration} мин)";
            else
                return $"Перемена ({duration} мин)";
        }
    }
}