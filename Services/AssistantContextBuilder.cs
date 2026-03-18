using Kiosk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kiosk.Services
{
    /// <summary>
    /// Собирает контекст из данных приложения для системного промпта GigaChat.
    /// </summary>
    public static class AssistantContextBuilder
    {
        public static string Build(
            ScheduleData scheduleData,
            ReplacementData replacementData,
            string weatherSummary,
            string selectedClass,
            DateTime now)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ты — школьный ИИ-помощник на информационном киоске. Отвечай коротко, по делу, дружелюбно. Используй эмодзи.");
            sb.AppendLine($"Сейчас: {now:dddd, d MMMM yyyy, HH:mm}");
            sb.AppendLine();

            // Погода
            if (!string.IsNullOrWhiteSpace(weatherSummary))
            {
                sb.AppendLine($"🌤 Погода: {weatherSummary}");
                sb.AppendLine();
            }

            // Расписание для выбранного класса
            if (scheduleData?.Schedules != null && !string.IsNullOrWhiteSpace(selectedClass))
            {
                var cls = scheduleData.Schedules.FirstOrDefault(s => s.ClassName == selectedClass);
                if (cls != null)
                {
                    var todayLessons = GetTodayLessons(cls, now.DayOfWeek);
                    sb.AppendLine($"📚 Расписание класса {selectedClass} на сегодня ({DayName(now.DayOfWeek)}):");
                    if (todayLessons.Any())
                    {
                        foreach (var l in todayLessons.OrderBy(x => x.Number))
                            sb.AppendLine($"  {l.Number}. {l.Time} — {l.Subject}, {l.Teacher}, каб. {l.Classroom}");
                    }
                    else
                        sb.AppendLine("  Уроков нет.");
                    sb.AppendLine();
                }
            }

            // Замены
            if (replacementData?.HasReplacements == true && !string.IsNullOrWhiteSpace(selectedClass))
            {
                var replacements = replacementData.Sections
                    .Where(s => s.HasLessons)
                    .SelectMany(s => s.Lessons)
                    .Where(l => l.Class == selectedClass)
                    .OrderBy(l => l.LessonNumber)
                    .ToList();

                if (replacements.Any())
                {
                    sb.AppendLine($"🔄 Замены для {selectedClass} на сегодня:");
                    foreach (var r in replacements)
                    {
                        var line = $"  Урок {r.LessonNumber}: {r.ReplacementTeacher}";
                        if (!string.IsNullOrWhiteSpace(r.Classroom) && r.Classroom != "-")
                            line += $", каб. {r.Classroom}";
                        if (!string.IsNullOrWhiteSpace(r.Notes))
                            line += $" ({r.Notes})";
                        sb.AppendLine(line);
                    }
                    sb.AppendLine();
                }
            }

            // Текущий статус (идёт урок / перемена)
            sb.AppendLine(GetCurrentStatus(scheduleData, selectedClass, now));

            return sb.ToString();
        }

        private static string GetCurrentStatus(ScheduleData scheduleData, string selectedClass, DateTime now)
        {
            if (scheduleData?.Schedules == null || string.IsNullOrWhiteSpace(selectedClass))
                return "";

            var cls = scheduleData.Schedules.FirstOrDefault(s => s.ClassName == selectedClass);
            if (cls == null) return "";

            var lessons = GetTodayLessons(cls, now.DayOfWeek);
            var t = now.TimeOfDay;

            // Звонки
            var bells = new Dictionary<int, (TimeSpan Start, TimeSpan End)>
            {
                { 1, (new TimeSpan(8,30,0),  new TimeSpan(9,15,0)) },
                { 2, (new TimeSpan(9,30,0),  new TimeSpan(10,15,0)) },
                { 3, (new TimeSpan(10,30,0), new TimeSpan(11,15,0)) },
                { 4, (new TimeSpan(11,30,0), new TimeSpan(12,15,0)) },
                { 5, (new TimeSpan(12,25,0), new TimeSpan(13,10,0)) },
                { 6, (new TimeSpan(13,35,0), new TimeSpan(14,20,0)) },
                { 7, (new TimeSpan(14,30,0), new TimeSpan(15,15,0)) },
                { 8, (new TimeSpan(15,30,0), new TimeSpan(16,15,0)) },
            };

            foreach (var l in lessons.OrderBy(x => x.Number))
            {
                if (!bells.TryGetValue(l.Number, out var bell)) continue;
                if (t >= bell.Start && t <= bell.End)
                {
                    var rem = bell.End - t;
                    return $"⏰ Сейчас идёт {l.Number} урок ({l.Subject}), до конца {(int)rem.TotalMinutes} мин.";
                }
            }

            foreach (var l in lessons.OrderBy(x => x.Number))
            {
                if (!bells.TryGetValue(l.Number, out var bell)) continue;
                if (t < bell.Start)
                {
                    var rem = bell.Start - t;
                    return $"☕ Сейчас перемена. Следующий урок: {l.Number} ({l.Subject}) через {(int)rem.TotalMinutes} мин.";
                }
            }

            return "🎉 Уроки на сегодня завершены.";
        }

        private static List<Lesson> GetTodayLessons(ClassSchedule cls, DayOfWeek day) => day switch
        {
            DayOfWeek.Monday    => cls.Days.Monday,
            DayOfWeek.Tuesday   => cls.Days.Tuesday,
            DayOfWeek.Wednesday => cls.Days.Wednesday,
            DayOfWeek.Thursday  => cls.Days.Thursday,
            DayOfWeek.Friday    => cls.Days.Friday,
            DayOfWeek.Saturday  => cls.Days.Saturday,
            _ => new List<Lesson>()
        };

        private static string DayName(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday    => "понедельник",
            DayOfWeek.Tuesday   => "вторник",
            DayOfWeek.Wednesday => "среда",
            DayOfWeek.Thursday  => "четверг",
            DayOfWeek.Friday    => "пятница",
            DayOfWeek.Saturday  => "суббота",
            _ => "воскресенье"
        };
    }
}
