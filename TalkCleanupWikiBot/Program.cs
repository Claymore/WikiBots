using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using TalkCleanupWikiBot.Properties;

namespace Claymore.TalkCleanupWikiBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            wiki.Login(Settings.Default.Login, Settings.Default.Password);
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения удаления страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList pages = doc.SelectNodes("//page");
            List<Day> days = new List<Day>();
            DateTime today = DateTime.Now;
            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К удалению/".Length);
                Day day = new Day();
                try
                {
                    day.Date = DateTime.Parse(date, CultureInfo.CreateSpecificCulture("ru-RU"), System.Globalization.DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
                {
                    continue;
                }
                Console.Out.WriteLine("Processing " + pageName + "...");
                string text = "";
                if (File.Exists("cache\\" + date + ".txt"))
                {
                    using (TextReader sr = new StreamReader("cache\\" + date + ".txt"))
                    {
                        string revid = sr.ReadLine();
                        if (revid != page.Attributes["lastrevid"].Value)
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("cache\\" + date + ".txt"))
                    {
                        sw.Write(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }                
                day.Candidates = new List<Candidate>(GetCandidatesFromPage(text, false));
                days.Add(day);
            }
            
            /*using (StreamWriter sw =
                        new StreamWriter("output.txt"))
            {
                sw.WriteLine("{{ВПКУ-Навигация}}\n");
                
                foreach (Candidate candidate in candidates)
                {
                    string title;
                    if (candidate.hasVerdict && !candidate.StrikenOut)
                    {
                        title = "== <s>" + candidate.Title + "</s> ==";
                    }
                    else
                    {
                        title = candidate.RawTitle;
                    }
                    sw.WriteLine(title);
                    sw.Write(candidate.Text);
                }
            }*/

            days.Sort(CompareDays);
            using (StreamWriter sw =
                        new StreamWriter("main.txt"))
            {
                sw.WriteLine("{{/Заголовок}}\n");
               
                foreach (Day day in days)
                {
                    sw.Write("{{Удаление статей|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {
                        titles.Add(candidate.ToString() + candidate.SubsectionsToString());
                    }
                    sw.Write(string.Join(" • ", titles.ConvertAll(c => c).ToArray()));
                    sw.Write("}}\n\n");
                }
                                
                sw.WriteLine("{{/Подвал}}");            
            }

            days.Clear();
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            DateTime start = currentMonth.AddMonths(-1);
            while (start < currentMonth)
            {
                string pageName = "Википедия:К удалению/" + start.ToString("d MMMM yyyy");
                if (doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") != null)
                {
                    start = start.AddDays(1);
                    continue;
                }
                                
                Day day = new Day();
                day.Date = start;
                Console.Out.WriteLine("Processing " + pageName + "...");
                string text = "";
                if (File.Exists("cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                {
                    using (TextReader sr = new StreamReader("cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                    {
                        text = sr.ReadToEnd();
                    }
                }
                else
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                    {
                        sw.Write(text);
                    }
                }
                day.Candidates = new List<Candidate>(GetCandidatesFromPage(text, false));
                days.Add(day);

                start = start.AddDays(1);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter("archive.txt"))
            {
                sw.WriteLine("{| class=standard");
                sw.WriteLine("|-");
                sw.WriteLine("!| Дата");
                sw.WriteLine("!| Статьи, вынесенные на удаление");
                sw.WriteLine("|-\n");

                StringBuilder sb = new StringBuilder();
                foreach (Day day in days)
                {
                    sb.Append("{{Удаление статей|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {
                        titles.Add(candidate.ToString() + candidate.SubsectionsToString());
                    }
                    sb.Append(string.Join(" • ", titles.ConvertAll(c => c).ToArray()));
                    sb.Append("}}\n\n");
                }
                sb.Replace("<s>", "");
                sb.Replace("</s>", "");
                sb.Replace("<strike>", "");
                sb.Replace("</strike>", "");
                
                sw.Write(sb.ToString());

                sw.WriteLine("|}");
            }

            Console.Out.WriteLine("Updating the wiki pages...");
            using (TextReader sr =
                        new StreamReader("main.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К удалению", text, "Обновление списка обсуждаемых страниц");
            }
            using (TextReader sr =
                        new StreamReader("archive.txt"))
            {
                string page = currentMonth.AddMonths(-1).ToString("yyyy-MM");
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:Архив запросов на удаление/" + page, text, "Обновление списка обсуждаемых страниц");
            }
            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        struct Day
        {
            public List<Candidate> Candidates;
            public DateTime Date;
        }

        static IEnumerable<Candidate> GetCandidatesFromPage(string text, bool archive)
        {
            string[] lines = text.Split(new char[] { '\n' });
            List<Candidate> messages = new List<Candidate>();
            StringBuilder message = new StringBuilder();
            Regex sectionRE = new Regex(@"^={2}[^=]+={2}\s*$");
            bool skip = true;
            foreach (string line in lines)
            {
                Match m = sectionRE.Match(line);
                if (m.Success)
                {
                    skip = false;
                    if (message.Length > 0)
                    {
                        messages.Add(Candidate.ParseCandidate(message.ToString(), 2));
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
                messages.Add(Candidate.ParseCandidate(message.ToString(), 2));
            }
            return messages;
        }

        static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }
    }
}
