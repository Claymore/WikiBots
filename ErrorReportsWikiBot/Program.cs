using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.ErrorReportsWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.ErrorReportsWikiBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            wiki.Login(Settings.Default.Login, Settings.Default.Password);
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");
            DateTime time = new DateTime(2009, 1, 1);
            Dictionary<string, DateTime> pages = new Dictionary<string, DateTime>();
            pages.Add("Википедия:Сообщения об ошибках", new DateTime());
            while (time <= DateTime.Now)
            {
                string name = time.ToString("MMMM yyyy");
                pages.Add("Википедия:Сообщения об ошибках/Архив/" + name, new DateTime());
                time = time.AddMonths(1);
            }
            string timeString = DateTime.Now.ToUniversalTime().ToString("HH:mm, d MMMM yyyy (UTC)");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Query(QueryBy.Titles, parameters, pages.Keys);
            XmlNodeList nodes = doc.SelectNodes("/api/query/pages/page");
            foreach (XmlNode node in nodes)
            {
                string title = node.Attributes["title"].Value;
                string timestamp = node.Attributes["touched"].Value;
                pages[title] = DateTime.Parse(timestamp, null, DateTimeStyles.AssumeUniversal);
            }
            bool changed = false;
            List<Message> messages = new List<Message>();
            Console.Out.WriteLine("Downloading statistic data...");
            foreach (string s in pages.Keys)
            {
                string timestamp = pages[s].ToString("yyyyMMddHHmmss");
                string filename = s.Replace(':', '-').Replace('/', '-') + ".bin";
                string text = null;
                if (File.Exists(filename))
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Open))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                    using (TextReader sr = new StreamReader(gs))
                    {
                        string ts = sr.ReadLine();
                        if (ts == timestamp)
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (text == null)
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        text = wiki.LoadPage(s);
                        sw.WriteLine(timestamp);
                        sw.Write(text);
                        changed = true;
                    }
                }
                messages.AddRange(GetMessagesFromPage(text, s.Contains("Архив")));
            }
            Console.Out.WriteLine("Data downloaded.");
            Console.Out.WriteLine("Processing data...");
            int newCount = messages.Count(m => m.Opened &&
                ((m.Archived && m.Closed) || !m.Archived));
            int closedCount = messages.Count(m => m.Opened && m.Closed);
            using (StreamWriter sw =
                        new StreamWriter("output.txt"))
            {
                sw.Write("На этой странице приводится статистика обработки [[ВП:СО|сообщений об ошибках]]");
                sw.Write(" по состоянию на " + timeString + ".");
                sw.Write(" Всего было подано " + Messages(messages.Count));
                sw.Write(", из них не учтено " + Messages(messages.Count - newCount));
                sw.Write(", учтено " + Messages(newCount));
                sw.Write(" (" + closedCount.ToString() + " распознаны как закрытые).");
                sw.Write("{{.ref|<ref>На [[ВП:СО]] учитываются сообщения, которые включают в себя строчку «Автор сообщения» с датой добавления. На страницах архивов учитываются сообщения, которые содержат строчку «Автор сообщения» или «Сообщил» и больше одной даты, при этом новейшая из них считается датой закрытия.</ref>}}\n\n");

                PrintWeeks(sw, messages);
                PrintMonths(sw, messages);
                PrintYears(sw, messages);
                PrintPeriods(sw, messages);
                sw.WriteLine("== Примечания ==");
                sw.WriteLine("{{примечания}}");
            }
            Console.Out.WriteLine("Results are ready.");
            if (changed)
            {
                Console.Out.WriteLine("Updating the wiki page...");
                using (TextReader sr =
                            new StreamReader("output.txt"))
                {
                    string text = sr.ReadToEnd();
                    wiki.SavePage("User:LEMeZza/Статистика ВП:СО", text, "Обновление статистики обработки [[ВП:СО|сообщений об ошибках]]");
                }
            }
            else
            {
                Console.Out.WriteLine("No changes, skipping the update.");
            }
            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        static string RussianNounPlural(int number, string one, string two, string others)
        {
            bool exception = (number % 100) / 10 == 1;
            int digit = number % 10;
            if (digit == 1)
            {
                return string.Format("{0} {1}", number, one);
            }
            else if (!exception && digit > 1 && digit < 5)
            {
                return string.Format("{0} {1}", number, two);
            }
            else
            {
                return string.Format("{0} {1}", number, others);
            }
        }

        static string Messages(int number)
        {
            return RussianNounPlural(number, "сообщение", "сообщения", "сообщений");
        }

        static void PrintPeriods(StreamWriter streamWriter, IList<Message> messages)
        {
            streamWriter.WriteLine("== Время обработки сообщения ==");
            streamWriter.WriteLine("{| class=\"standard\" width=\"100%\"");
            streamWriter.WriteLine("! style=\"background:#efefef\" rowspan=2 | Период");
            streamWriter.WriteLine("! style=\"background:#efefef\" rowspan=2 | Новых сообщений<ref name='new' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" rowspan=2 | Обработано новых<ref>Количество новых сообщений, которые поступили в этот период и сейчас закрыты.</ref>");
            streamWriter.WriteLine("! style=\"background:#efefef\" rowspan=2 | Процент<ref name='new_percent' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" colspan=4 | Время обработки сообщения");
            streamWriter.WriteLine("|-");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Минимальное");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Максимальное");
            streamWriter.WriteLine("! style=\"background:#efefef\" | [[Среднее арифметическое|Среднее]]");
            streamWriter.WriteLine("! style=\"background:#efefef\" | [[Медиана (статистика)|]]");

            DateTime now = DateTime.Now.AddDays(1);
            DateTime end = new DateTime(now.Year, now.Month, now.Day);
            DateTime start = end.AddDays(-7);

            PrintPeriod(streamWriter, "Последняя неделя", start, end, messages, "bgcolor=#FFFFFF");
            start = end.AddMonths(-1).AddDays(-1);
            PrintPeriod(streamWriter, "Последний месяц", start, end, messages, "bgcolor=#F7F7F7");
            start = end.AddMonths(-3).AddDays(-1);
            PrintPeriod(streamWriter, "Последние три месяца", start, end, messages, "bgcolor=#FFFFFF");
            start = end.AddYears(-1).AddDays(-1);
            PrintPeriod(streamWriter, "Последний год", start, end, messages, "bgcolor=#F7F7F7");
            start = new DateTime();
            PrintPeriod(streamWriter, "За всё время", start, end, messages, "bgcolor=#FFFFFF");
            streamWriter.WriteLine("|}\n");
        }

        static void PrintWeeks(StreamWriter streamWriter, IList<Message> messages)
        {
            DateTime end = DateTime.Parse("2009-01-01");
            DateTime start = end.AddDays(-6);
            streamWriter.WriteLine("== Статистика по неделям ==");
            streamWriter.WriteLine("{| class=\"standard\" width=\"100%\"");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Период");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано всего<ref name='total'>Количество всех сообщений, которые были закрыты в данный период.</ref>");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Новых сообщений<ref name='new'>Количество новых сообщений, поступивших в данный период.</ref>");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано новых<ref name='processed_new'>Количество новых сообщений, которые поступили и были закрыты в этот же период.</ref>");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Процент<ref name='new_percent'>Процент обработанных новых сообщений.</ref>");

            bool even = false;
            while (start <= DateTime.Now)
            {
                end = start.AddDays(6);
                string bg;
                if (!even)
                {
                    bg = "bgcolor=#FFFFFF";
                }
                else
                {
                    bg = "bgcolor=#F7F7F7";
                }
                PrintWeek(streamWriter, start, end, messages, bg);

                start = end.AddDays(1);
                even = !even;
            }
            streamWriter.WriteLine("|}\n");
        }

        static void PrintMonths(StreamWriter streamWriter, IList<Message> messages)
        {
            DateTime start = DateTime.Parse("2008-12-01");
            streamWriter.WriteLine("== Статистика по месяцам ==");
            streamWriter.WriteLine("{| class=\"standard\" width=\"100%\"");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Период");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано всего<ref name='total' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Новых сообщений<ref name='new' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано новых<ref name='processed_new' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Процент<ref name='new_percent' />");

            bool even = false;
            while (start <= DateTime.Now)
            {
                DateTime end = start.AddMonths(1);
                string bg;
                if (!even)
                {
                    bg = "bgcolor=#FFFFFF";
                }
                else
                {
                    bg = "bgcolor=#F7F7F7";
                }
                PrintMonth(streamWriter, start, end, messages, bg);

                start = end;
                even = !even;
            }
            streamWriter.WriteLine("|}\n");
        }

        static void PrintYears(StreamWriter streamWriter, IList<Message> messages)
        {
            DateTime start = DateTime.Parse("2008-01-01");
            streamWriter.WriteLine("== Статистика по годам ==");
            streamWriter.WriteLine("{| class=\"standard\" width=\"100%\"");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Период");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано всего<ref name='total' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Новых сообщений<ref name='new' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Обработано новых<ref name='processed_new' />");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Процент<ref name='new_percent' />");

            bool even = false;
            while (start <= DateTime.Now)
            {
                DateTime end = start.AddYears(1);
                string bg;
                if (!even)
                {
                    bg = "bgcolor=#FFFFFF";
                }
                else
                {
                    bg = "bgcolor=#F7F7F7";
                }
                PrintYear(streamWriter, start, end, messages, bg);

                start = end;
                even = !even;
            }
            streamWriter.WriteLine("|}\n");
        }

        static string TimeSpanToString(TimeSpan ts)
        {
            string days = ts.Days > 0 ? ts.Days.ToString() + "d " : "";
            return string.Format("{0}{1:D2}:{2:D2}", days, ts.Hours, ts.Minutes);
        }

        static void PrintWeek(StreamWriter streamWriter, DateTime start, DateTime end, IList<Message> messages, string background)
        {
            streamWriter.WriteLine("|- align=\"center\" " + background);
            string period;
            if (start.Month == end.Month && start.Year == end.Year)
            {
                period = string.Format("{0}—{1}",
                    start.Day, end.ToString("d MMMM yyyy"));
            }
            else if (start.Year == end.Year)
            {
                period = string.Format("{0} — {1}",
                    start.ToString("d MMMM"), end.ToString("d MMMM yyyy"));
            }
            else
            {
                period = string.Format("{0} — {1}",
                    start.ToString("d MMMM yyy"), end.ToString("d MMMM yyyy"));
            }
            DateTime realEnd = end.AddDays(1);
            int processedTotal = messages.Count(m => m.Opened &&
                m.Closed &&
                m.ClosedAt >= start &&
                m.ClosedAt < realEnd);
            int newMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < realEnd &&
                    ((m.Archived && m.Closed) || !m.Archived));
            int processedMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < realEnd &&
                    m.Closed &&
                    m.ClosedAt >= start &&
                    m.ClosedAt < realEnd);

            streamWriter.WriteLine("|" + period);
            streamWriter.WriteLine("|" + processedTotal);
            streamWriter.WriteLine("|" + newMessagesCount);
            streamWriter.WriteLine("|" + processedMessagesCount);
            streamWriter.WriteLine(string.Format("|{0} %", processedMessagesCount * 100 / newMessagesCount));
        }

        static void PrintMonth(StreamWriter streamWriter, DateTime start, DateTime end, IList<Message> messages, string background)
        {
            streamWriter.WriteLine("|- align=\"center\" " + background);
            string period = start.ToString("MMMM yyyy").ToLower();
            int processedTotal = messages.Count(m => m.Opened &&
                m.Closed &&
                m.ClosedAt >= start &&
                m.ClosedAt < end);

            int newMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    ((m.Archived && m.Closed) || !m.Archived));
            int processedMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    m.Closed &&
                    m.ClosedAt >= start &&
                    m.ClosedAt <= end);

            streamWriter.WriteLine("|" + period);
            streamWriter.WriteLine("|" + processedTotal);
            streamWriter.WriteLine("|" + newMessagesCount);
            streamWriter.WriteLine("|" + processedMessagesCount);
            streamWriter.WriteLine(string.Format("|{0} %", processedMessagesCount * 100 / newMessagesCount));
        }

        static void PrintYear(StreamWriter streamWriter, DateTime start, DateTime end, IList<Message> messages, string background)
        {
            streamWriter.WriteLine("|- align=\"center\" " + background);
            string period = start.ToString("yyyy");
            int processedTotal = messages.Count(m => m.Opened &&
                m.Closed &&
                m.ClosedAt >= start &&
                m.ClosedAt < end);
            int newMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    ((m.Archived && m.Closed) || !m.Archived));
            int processedMessagesCount = messages.Count(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    m.Closed &&
                    m.ClosedAt >= start &&
                    m.ClosedAt <= end);

            streamWriter.WriteLine("|" + period);
            streamWriter.WriteLine("|" + processedTotal);
            streamWriter.WriteLine("|" + newMessagesCount);
            streamWriter.WriteLine("|" + processedMessagesCount);
            streamWriter.WriteLine(string.Format("|{0} %", processedMessagesCount * 100 / newMessagesCount));
        }

        static void PrintPeriod(StreamWriter streamWriter, string period, DateTime start, DateTime end, IList<Message> messages, string background)
        {
            streamWriter.WriteLine("|- align=\"center\" " + background);
            List<Message> weekMessages = new List<Message>(messages.Where(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    ((m.Archived && m.Closed) || !m.Archived)));
            List<Message> processedMessages = new List<Message>(messages.Where(m => m.Opened &&
                    m.OpenedAt >= start &&
                    m.OpenedAt < end &&
                    m.Closed));

            processedMessages.Sort(CompareMessagesByProcessingTime);
            int medianIndex = (int)((processedMessages.Count + 1) * 0.5);
            Message medianMessage = processedMessages[medianIndex];
            TimeSpan median = medianMessage.ClosedAt - medianMessage.OpenedAt;
            long averageTicks = (long)processedMessages.Average(m => (m.ClosedAt - m.OpenedAt).Ticks);
            TimeSpan average = TimeSpan.FromTicks(averageTicks);
            long maxTicks = processedMessages.Max(m => (m.ClosedAt - m.OpenedAt).Ticks);
            TimeSpan max = TimeSpan.FromTicks(maxTicks);
            long minTicks = processedMessages.Min(m => (m.ClosedAt - m.OpenedAt).Ticks);
            TimeSpan min = TimeSpan.FromTicks(minTicks);

            streamWriter.WriteLine("|" + period);
            streamWriter.WriteLine("|" + weekMessages.Count);
            streamWriter.WriteLine("|" + processedMessages.Count);
            streamWriter.WriteLine(string.Format("|{0} %", processedMessages.Count * 100 / weekMessages.Count));
            streamWriter.WriteLine("|" + TimeSpanToString(min));
            streamWriter.WriteLine("|" + TimeSpanToString(max));
            streamWriter.WriteLine("|" + TimeSpanToString(average));
            streamWriter.WriteLine("|" + TimeSpanToString(median));
        }

        static IEnumerable<Message> GetMessagesFromPage(string text, bool archive)
        {
            string[] lines = text.Split(new char[] { '\n' });
            List<Message> messages = new List<Message>();
            StringBuilder message = new StringBuilder();
            Regex sectionRE = new Regex(@"^==[^=]+==\s*$");
            bool skip = true;
            foreach (string line in lines)
            {
                Match m = sectionRE.Match(line);
                if (m.Success)
                {
                    skip = false;
                    if (message.Length > 0)
                    {
                        messages.Add(Message.Parse(message.ToString(), archive));
                        message = new StringBuilder();
                    }
                    message.Append(line + "\n");
                }
                else if (!skip)
                {
                    message.Append(line + "\n");
                }
            }
            if (!skip && message.Length > 0)
            {
                messages.Add(Message.Parse(message.ToString(), archive));
            }
            return messages;
        }

        static int CompareMessagesByProcessingTime(Message x, Message y)
        {
            long xTicks = (x.ClosedAt - x.OpenedAt).Ticks;
            long yTicks = (y.ClosedAt - y.OpenedAt).Ticks;
            return xTicks.CompareTo(yTicks);
        }
    }
}
