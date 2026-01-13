using System.Collections.Generic;
using System.Linq;

namespace Kiosk.Models
{
    public class ReplacementData
    {
        public string Date { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<ReplacementSection> Sections { get; set; } = new();

        // Добавляем свойство для проверки наличия замен
        public bool HasReplacements => Sections?.Any(s => s.Lessons?.Any() == true) == true;
    }

    public class ReplacementSection
    {
        public string Teacher { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ReplacementLesson> Lessons { get; set; } = new();

        // Добавляем свойство для проверки наличия уроков в секции
        public bool HasLessons => Lessons?.Any() == true;
    }

    public class ReplacementLesson
    {
        public int LessonNumber { get; set; }
        public string Class { get; set; } = string.Empty;
        public string ReplacementTeacher { get; set; } = string.Empty;
        public string Classroom { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ClassReplacement
    {
        public string ClassName { get; set; } = string.Empty;
        public List<ReplacementLesson> Replacements { get; set; } = new();

        // Добавляем свойство для проверки наличия замен в классе
        public bool HasReplacements => Replacements?.Any() == true;
    }
}