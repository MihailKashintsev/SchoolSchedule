using System.Collections.Generic;

namespace Kiosk.Models
{
    public record ScheduleData
    {
        public string LastUpdated { get; set; } = string.Empty;
        public string WeekType { get; set; } = "current";
        public List<ClassSchedule> Schedules { get; set; } = new();
        public BreakSettings BreakSettings { get; set; } = new();
    }

    public record ClassSchedule
    {
        public string ClassName { get; set; } = string.Empty;
        public ClassDays Days { get; set; } = new();
    }

    public record ClassDays
    {
        public List<Lesson> Monday { get; set; } = new();
        public List<Lesson> Tuesday { get; set; } = new();
        public List<Lesson> Wednesday { get; set; } = new();
        public List<Lesson> Thursday { get; set; } = new();
        public List<Lesson> Friday { get; set; } = new();
        public List<Lesson> Saturday { get; set; } = new();
    }

    public record Lesson
    {
        public int Number { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
    }

    public class DisplayDay
    {
        public string DayName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public List<Lesson> Lessons { get; set; } = new();
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }

        public bool HasNoLessons => Lessons == null || Lessons.Count == 0;
    }

    public record BreakSettings
    {
        public int Break1Duration { get; set; } = 10;
        public int Break2Duration { get; set; } = 10;
        public int Break3Duration { get; set; } = 15;
        public int Break4Duration { get; set; } = 10;
        public int Break5Duration { get; set; } = 10;
        public int Break6Duration { get; set; } = 10;
        public int Break7Duration { get; set; } = 10;
    }

    public class ScheduleItem
    {
        public bool IsBreak { get; set; }
        public string BreakText { get; set; } = string.Empty;
        public int BreakDuration { get; set; }
        public Lesson? Lesson { get; set; }
    }
}