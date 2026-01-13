using Kiosk.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kiosk.Services
{
    public class SchoolAssistantService
    {
        private readonly JsonScheduleService _scheduleService;
        private readonly DocxReplacementService _replacementService;

        // Расписание звонков (можно вынести в настройки)
        private readonly Dictionary<int, (TimeSpan Start, TimeSpan End)> _bellSchedule = new()
        {
            { 1, (new TimeSpan(8, 30, 0), new TimeSpan(9, 15, 0)) },
            { 2, (new TimeSpan(9, 30, 0), new TimeSpan(10, 15, 0)) },
            { 3, (new TimeSpan(10, 30, 0), new TimeSpan(11, 15, 0)) },
            { 4, (new TimeSpan(11, 30, 0), new TimeSpan(12, 15, 0)) },
            { 5, (new TimeSpan(12, 25, 0), new TimeSpan(13, 10, 0)) },
            { 6, (new TimeSpan(13, 35, 0), new TimeSpan(14, 20, 0)) },
            { 7, (new TimeSpan(14, 30, 0), new TimeSpan(15, 15, 0)) },
            { 8, (new TimeSpan(15, 30, 0), new TimeSpan(16, 15, 0)) }
        };

        public SchoolAssistantService()
        {
            _scheduleService = new JsonScheduleService();
            _replacementService = new DocxReplacementService();
        }

        public AssistantInfo GetCurrentInfo(ScheduleData scheduleData, ReplacementData replacementData, string className, DateTime currentTime)
        {
            var info = new AssistantInfo();
            var classSchedule = scheduleData.Schedules.FirstOrDefault(s => s.ClassName == className);

            if (classSchedule == null)
                return info;

            // Получаем уроки на сегодня
            var todayLessons = GetTodayLessons(classSchedule, currentTime.DayOfWeek);
            info.TodayLessons = todayLessons;

            // Определяем текущее состояние
            var currentState = GetCurrentState(todayLessons, currentTime);
            info.CurrentState = currentState;

            // Получаем информацию о заменах
            var classReplacements = GetClassReplacements(replacementData, className);
            info.ClassReplacements = classReplacements;

            // Определяем следующий урок
            info.NextLesson = GetNextLesson(todayLessons, currentTime);

            return info;
        }

        private List<Lesson> GetTodayLessons(ClassSchedule classSchedule, DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => classSchedule.Days.Monday,
                DayOfWeek.Tuesday => classSchedule.Days.Tuesday,
                DayOfWeek.Wednesday => classSchedule.Days.Wednesday,
                DayOfWeek.Thursday => classSchedule.Days.Thursday,
                DayOfWeek.Friday => classSchedule.Days.Friday,
                DayOfWeek.Saturday => classSchedule.Days.Saturday,
                _ => new List<Lesson>()
            };
        }

        private CurrentState GetCurrentState(List<Lesson> lessons, DateTime currentTime)
        {
            var currentTimeOfDay = currentTime.TimeOfDay;
            var state = new CurrentState();

            // Проверяем, идет ли сейчас урок
            foreach (var lesson in lessons.OrderBy(l => l.Number))
            {
                if (_bellSchedule.TryGetValue(lesson.Number, out var bell))
                {
                    if (currentTimeOfDay >= bell.Start && currentTimeOfDay <= bell.End)
                    {
                        state.IsLesson = true;
                        state.CurrentLesson = lesson;
                        state.TimeRemaining = bell.End - currentTimeOfDay;
                        return state;
                    }
                }
            }

            // Если не урок, ищем следующее занятие
            foreach (var lesson in lessons.OrderBy(l => l.Number))
            {
                if (_bellSchedule.TryGetValue(lesson.Number, out var bell))
                {
                    if (currentTimeOfDay < bell.Start)
                    {
                        state.IsBreak = true;
                        state.NextLesson = lesson;
                        state.TimeRemaining = bell.Start - currentTimeOfDay;
                        return state;
                    }
                }
            }

            // Если все уроки прошли
            state.IsSchoolOver = true;
            return state;
        }

        private List<ReplacementLesson> GetClassReplacements(ReplacementData replacementData, string className)
        {
            if (replacementData?.Sections == null || !replacementData.HasReplacements)
                return new List<ReplacementLesson>();

            var replacements = new List<ReplacementLesson>();

            foreach (var section in replacementData.Sections)
            {
                if (!section.HasLessons) continue;

                var classReplacements = section.Lessons.Where(l => l.Class == className).ToList();
                replacements.AddRange(classReplacements);
            }

            return replacements.OrderBy(r => r.LessonNumber).ToList();
        }

        private Lesson GetNextLesson(List<Lesson> lessons, DateTime currentTime)
        {
            var currentTimeOfDay = currentTime.TimeOfDay;

            return lessons.OrderBy(l => l.Number)
                .FirstOrDefault(lesson =>
                    _bellSchedule.TryGetValue(lesson.Number, out var bell) &&
                    bell.Start > currentTimeOfDay);
        }

        // Новый метод для форматирования времени
        public string FormatTimeRemaining(TimeSpan timeRemaining)
        {
            if (timeRemaining.TotalHours >= 1)
            {
                return $"{(int)timeRemaining.TotalHours:00}:{timeRemaining.Minutes:00}:{timeRemaining.Seconds:00}";
            }
            else
            {
                return $"{timeRemaining.Minutes:00}:{timeRemaining.Seconds:00}";
            }
        }
    }

    public class AssistantInfo
    {
        public CurrentState CurrentState { get; set; } = new();
        public List<Lesson> TodayLessons { get; set; } = new();
        public List<ReplacementLesson> ClassReplacements { get; set; } = new();
        public Lesson NextLesson { get; set; }
    }

    public class CurrentState
    {
        public bool IsLesson { get; set; }
        public bool IsBreak { get; set; }
        public bool IsSchoolOver { get; set; }
        public Lesson CurrentLesson { get; set; }
        public Lesson NextLesson { get; set; }
        public TimeSpan TimeRemaining { get; set; }
    }
}