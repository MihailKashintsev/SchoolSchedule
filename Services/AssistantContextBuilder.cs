using Kiosk.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kiosk.Services
{
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

            sb.AppendLine("Ты — школьный ИИ-помощник на информационном киоске.");
            sb.AppendLine("Отвечай коротко, по делу, дружелюбно. Используй эмодзи.");
            sb.AppendLine("ВАЖНО: отвечай ТОЛЬКО на основе данных ниже. Не придумывай уроки.");
            sb.AppendLine($"Сейчас: {now.ToString("dddd, d MMMM yyyy, HH:mm", new CultureInfo("ru-RU"))}");
            sb.AppendLine();

            // Погода
            if (!string.IsNullOrWhiteSpace(weatherSummary))
            {
                sb.AppendLine($"🌤 Погода: {weatherSummary}");
                sb.AppendLine();
            }

            if (scheduleData?.Schedules != null && !string.IsNullOrWhiteSpace(selectedClass))
            {
                var cls = scheduleData.Schedules.FirstOrDefault(s => s.ClassName == selectedClass);
                if (cls != null)
                {
                    // Расписание на всю неделю
                    sb.AppendLine($"📅 ПОЛНОЕ РАСПИСАНИЕ класса {selectedClass} на неделю:");
                    sb.AppendLine();

                    var days = new[]
                    {
                        (DayOfWeek.Monday,    "Понедельник", cls.Days.Monday),
                        (DayOfWeek.Tuesday,   "Вторник",     cls.Days.Tuesday),
                        (DayOfWeek.Wednesday, "Среда",       cls.Days.Wednesday),
                        (DayOfWeek.Thursday,  "Четверг",     cls.Days.Thursday),
                        (DayOfWeek.Friday,    "Пятница",     cls.Days.Friday),
                        (DayOfWeek.Saturday,  "Суббота",     cls.Days.Saturday),
                    };

                    var tomorrow = now.AddDays(1).DayOfWeek;

                    foreach (var (dow, name, lessons) in days)
                    {
                        var marker = dow == now.DayOfWeek ? " ← СЕГОДНЯ"
                                   : dow == tomorrow      ? " ← ЗАВТРА"
                                   : "";
                        sb.AppendLine($"  {name}{marker}:");

                        if (lessons == null || !lessons.Any())
                        {
                            sb.AppendLine("    Уроков нет.");
                        }
                        else
                        {
                            foreach (var l in lessons.OrderBy(x => x.Number))
                            {
                                var cab = string.IsNullOrWhiteSpace(l.Classroom) || l.Classroom == "-"
                                    ? "" : $", каб. {l.Classroom}";
                                var teacher = string.IsNullOrWhiteSpace(l.Teacher)
                                    ? "" : $", {l.Teacher}";
                                sb.AppendLine($"    {l.Number}. {l.Time} — {l.Subject}{teacher}{cab}");
                            }
                        }
                        sb.AppendLine();
                    }

                    // Текущий статус
                    sb.AppendLine(GetCurrentStatus(cls, now));
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

            return sb.ToString();
        }

        private static string GetCurrentStatus(ClassSchedule cls, DateTime now)
        {
            var lessons = GetLessonsForDay(cls, now.DayOfWeek);
            var t = now.TimeOfDay;

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
                    return $"☕ Сейчас перемена. Следующий: {l.Number} урок ({l.Subject}) через {(int)rem.TotalMinutes} мин.";
                }
            }

            return "🎉 Уроки на сегодня завершены.";
        }

        private static List<Lesson> GetLessonsForDay(ClassSchedule cls, DayOfWeek day) => day switch
        {
            DayOfWeek.Monday    => cls.Days.Monday    ?? new List<Lesson>(),
            DayOfWeek.Tuesday   => cls.Days.Tuesday   ?? new List<Lesson>(),
            DayOfWeek.Wednesday => cls.Days.Wednesday ?? new List<Lesson>(),
            DayOfWeek.Thursday  => cls.Days.Thursday  ?? new List<Lesson>(),
            DayOfWeek.Friday    => cls.Days.Friday    ?? new List<Lesson>(),
            DayOfWeek.Saturday  => cls.Days.Saturday  ?? new List<Lesson>(),
            _ => new List<Lesson>()
        };
    }
}
