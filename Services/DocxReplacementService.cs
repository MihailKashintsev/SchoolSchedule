using Kiosk.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Kiosk.Services
{
    public class DocxReplacementService
    {
        public ReplacementData LoadReplacements(string filePath)
        {
            var replacementData = new ReplacementData();

            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"Файл замен не найден: {filePath}", "Информация",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return replacementData;
                }

                using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = doc.MainDocumentPart.Document.Body;
                    replacementData = ParseDocument(body);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения файла замен: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return replacementData;
        }

        private ReplacementData ParseDocument(Body body)
        {
            var replacementData = new ReplacementData();
            ReplacementSection currentSection = null;

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    string text = GetParagraphText(paragraph).Trim();

                    if (string.IsNullOrEmpty(text)) continue;

                    // Парсим заголовок документа
                    if (replacementData.Date == string.Empty && text.Contains("Замены на"))
                    {
                        replacementData.Date = ExtractDate(text);
                        replacementData.Title = text;
                        continue;
                    }

                    // Ищем учителя (жирный текст)
                    if (IsTeacherParagraph(paragraph, text))
                    {
                        // Сохраняем предыдущую секцию если она есть
                        if (currentSection != null && currentSection.Lessons.Any())
                        {
                            replacementData.Sections.Add(currentSection);
                        }

                        currentSection = new ReplacementSection
                        {
                            Teacher = CleanTeacherText(text),
                            Description = ExtractDescription(text)
                        };
                        continue;
                    }

                    // Парсим описание мероприятия
                    if (currentSection != null && text.Contains("МЭ ВсОШ") ||
                        text.Contains("Городская библиотека") ||
                        text.Contains("Начало в"))
                    {
                        currentSection.Description = text;
                    }
                }
                else if (element is Table table && currentSection != null)
                {
                    ParseTable(table, currentSection);
                }
            }

            // Добавляем последнюю секцию
            if (currentSection != null && currentSection.Lessons.Any())
            {
                replacementData.Sections.Add(currentSection);
            }

            return replacementData;
        }

        private string GetParagraphText(Paragraph paragraph)
        {
            return string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
        }

        private bool IsTeacherParagraph(Paragraph paragraph, string text)
        {
            // Проверяем жирный шрифт
            var runs = paragraph.Descendants<Run>();
            if (runs.Any(r => r.RunProperties?.Bold != null))
            {
                return true;
            }

            // Проверяем формат **Текст**
            if (Regex.IsMatch(text, @"\*\*.+\*\*"))
            {
                return true;
            }

            return false;
        }

        private string CleanTeacherText(string text)
        {
            // Убираем ** и лишние пробелы
            text = text.Replace("**", "").Trim();

            // Убираем описание после учителя
            var index = text.IndexOf('+');
            if (index > 0)
            {
                text = text.Substring(0, index).Trim();
            }

            return text;
        }

        private string ExtractDescription(string text)
        {
            if (text.Contains("Городская библиотека") || text.Contains("МЭ ВсОШ"))
            {
                return text;
            }

            var plusIndex = text.IndexOf('+');
            if (plusIndex > 0)
            {
                return text.Substring(plusIndex + 1).Trim();
            }

            return string.Empty;
        }

        private string ExtractDate(string text)
        {
            var match = Regex.Match(text, @"\d{1,2}\.\d{1,2}\.\d{4}");
            return match.Success ? match.Value : DateTime.Now.ToString("dd.MM.yyyy");
        }

        private void ParseTable(Table table, ReplacementSection section)
        {
            var rows = table.Descendants<TableRow>().Skip(1); // Пропускаем заголовок

            foreach (var row in rows)
            {
                var cells = row.Descendants<TableCell>().ToList();
                if (cells.Count >= 4)
                {
                    var lesson = new ReplacementLesson();

                    // Номер урока
                    string lessonText = GetCellText(cells[0]);
                    if (int.TryParse(lessonText, out int lessonNum))
                    {
                        lesson.LessonNumber = lessonNum;
                    }

                    // Класс
                    lesson.Class = GetCellText(cells[1]);

                    // Заменяющий учитель
                    lesson.ReplacementTeacher = GetCellText(cells[2]);

                    // Кабинет
                    lesson.Classroom = GetCellText(cells[3]);

                    // Проверяем на специальные пометки
                    if (lesson.ReplacementTeacher.Contains("Подгруппа") ||
                        lesson.ReplacementTeacher.Contains("объединение") ||
                        lesson.ReplacementTeacher.Contains("приходит"))
                    {
                        lesson.Notes = lesson.ReplacementTeacher;
                        lesson.ReplacementTeacher = "—";
                    }

                    if (!string.IsNullOrEmpty(lesson.Class) && lesson.LessonNumber > 0)
                    {
                        section.Lessons.Add(lesson);
                    }
                }
            }
        }

        private string GetCellText(TableCell cell)
        {
            return string.Join("", cell.Descendants<Text>().Select(t => t.Text)).Trim();
        }

        public List<ClassReplacement> GetReplacementsByClass(ReplacementData replacementData)
        {
            var classReplacements = new Dictionary<string, ClassReplacement>();

            foreach (var section in replacementData.Sections)
            {
                foreach (var lesson in section.Lessons)
                {
                    if (!classReplacements.ContainsKey(lesson.Class))
                    {
                        classReplacements[lesson.Class] = new ClassReplacement
                        {
                            ClassName = lesson.Class
                        };
                    }

                    classReplacements[lesson.Class].Replacements.Add(lesson);
                }
            }

            return classReplacements.Values
                .OrderBy(c => c.ClassName)
                .ToList();
        }
    }
}