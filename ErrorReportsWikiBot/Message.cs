using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Claymore.ErrorReportsWikiBot
{
    internal struct Message
    {
        public string Title;
        public bool Opened;
        public DateTime OpenedAt;
        public bool Closed;
        public DateTime ClosedAt;
        public bool Archived;

        public override string ToString()
        {
            return Title.ToString();
        }

        public static Message Parse(string text, bool isArchived)
        {
            CultureInfo culture = CultureInfo.CreateSpecificCulture("ru-RU");
            Message message = new Message();
            message.Archived = isArchived;
            string[] lines = text.Split(new char[] { '\n' });
            Regex authorRE = new Regex(@"(Автор сообщения|Сообщил): .+ (\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
            Regex titleRE = new Regex(@"^==([^=]+)==\s*$");
            Match m = titleRE.Match(lines[0]);
            if (m.Success)
            {
                message.Title = m.Groups[1].Value.Trim();
            }
            foreach (string line in lines)
            {
                m = authorRE.Match(line);
                if (m.Success)
                {
                    message.Opened = true;
                    message.OpenedAt = DateTime.Parse(m.Groups[2].Value,
                        culture,
                        DateTimeStyles.AssumeUniversal);
                    break;
                }
            }
            string[] closedTemplates = new string[] { "{{Closed}}", "{{Закрыто}}", "{{Обсуждение закрыто}}", "{{Начало завершившегося обсуждения}}" };
            string[] ecsTemplates = new string[] { "{{Ecs}}", "{{Закрыто-конец}}", "{{/closed}}", "{{\\closed}}", "{{Конец}}" };
            bool closed = closedTemplates.Any(s => text.IndexOf(s, StringComparison.CurrentCultureIgnoreCase) != -1) ||
                ecsTemplates.Any(s => text.IndexOf(s, StringComparison.CurrentCultureIgnoreCase) != -1);
            if (isArchived || closed)
            {
                Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
                MatchCollection ms = timeRE.Matches(text);
                if (ms.Count > 1)
                {
                    message.ClosedAt = DateTime.Parse(ms[0].Groups[1].Value, culture,
                            DateTimeStyles.AssumeUniversal);
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, culture,
                            DateTimeStyles.AssumeUniversal);
                        if (time > message.ClosedAt)
                        {
                            message.Closed = true;
                            message.ClosedAt = time;
                        }
                    }
                }
            }
            return message;
        }
    }
}
