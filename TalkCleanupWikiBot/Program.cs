using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using Claymore.SharpMediaWiki;
using TalkCleanupWikiBot.Properties;
using System.Net;

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

            ProcessArticlesForDeletion(wiki);
            UpdateArticlesForDeletion(wiki);

            ProcessProposedMerges(wiki);
            UpdateProposedMerges(wiki);

            ProcessRequestedMoves(wiki);
            UpdateRequestedMoves(wiki);
                        
            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        private static void UpdateRequestedMoves(Wiki wiki)
        {
            Console.Out.WriteLine("Updating requested moves...");
            using (TextReader sr =
                        new StreamReader("move.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К переименованию", text, "Обновление списка обсуждаемых страниц");
            }
        }

        private static void ProcessRequestedMoves(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения переименования страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList pages = doc.SelectNodes("//page");

            List<Day> days = new List<Day>();
            DateTime start = DateTime.Today;
            Regex closedRE = new Regex(@"\{{2}ВПКПМ-Навигация\}{2}\s*\{{2}(Закрыто|Closed|закрыто|closed)\}{2}");
            Regex closed2RE = new Regex(@"{{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПКПМ-Навигация}}.+");
            
            wiki.SleepBetweenEdits = 10;
            wiki.SleepBetweenQueries = 2;

            DateTime cutOffDate = new DateTime(2009, 3, 21);
            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К переименованию/".Length);
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
                if (File.Exists("moves-cache\\" + date + ".txt"))
                {
                    using (TextReader sr = new StreamReader("moves-cache\\" + date + ".txt"))
                    {
                        string revid = sr.ReadLine();
                        if (revid == page.Attributes["lastrevid"].Value)
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("moves-cache\\" + date + ".txt"))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }
                Match m = closedRE.Match(text);
                Match m2 = closed2RE.Match(text);
                if (m.Success || m2.Success)
                {
                    text = text.Replace("{{ВПКПМ-Навигация}}", "{{ВПКПМ-Навигация|nocat=1}}");
                    try
                    {
                        wiki.SavePage(pageName, text, "Обсуждение закрыто, убираем страницу из категории.");
                    }
                    catch (WikiException)
                    {
                    }
                    continue;
                }
                day.Candidates = new List<Candidate>(GetCandidatesFromPage(text, false));
                days.Add(day);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter("move.txt"))
            {
                sw.WriteLine("{{/Шапка}}\n");
                sw.WriteLine("{{Переименование статей/Статьи, вынесенные на переименование}}\n");

                Regex wikiLinkRE = new Regex(@"^==\s*(<s>)?\s*\[{2}([^\]]+)\]{2}\s*");
                Regex wikiLink2RE = new Regex(@"^===\s*(<s>)?\s*\[{2}([^\]]+)\]{2}\s*");

                foreach (Day day in days)
                {
                    sw.Write("{{Переименование статей/День|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {                        
                        Match m = wikiLinkRE.Match(candidate.RawTitle);
                        if (m.Success)
                        {
                            string link = m.Groups[2].Value;
                            if (candidate.hasVerdict)
                            {
                                string movedTo;
                                bool moved = MovedTo(wiki, link, day.Date, out movedTo);
                                string result;
                                if (moved)
                                {
                                    result = string.Format(" ''(переименовано в «[[{0}]]»)''", movedTo);
                                }
                                else
                                {
                                    result = " ''(не переименовано)''";
                                }
                                titles.Add(candidate.ToString() + result);
                                continue;
                            }
                        }
                        titles.Add(candidate.ToString());
                        foreach (Candidate subsection in candidate.SubSections)
                        {
                            m = wikiLink2RE.Match(subsection.RawTitle);
                            if (m.Success)
                            {
                                string link = m.Groups[2].Value;
                                if (candidate.hasVerdict ||
                                    candidate.StrikenOut ||
                                    subsection.hasVerdict ||
                                    subsection.StrikenOut)
                                {
                                    string movedTo;
                                    bool moved = MovedTo(wiki, link, day.Date, out movedTo);
                                    string result;
                                    if (moved)
                                    {
                                        result = string.Format(" ''(переименовано в «[[{0}]]»)''", movedTo);
                                    }
                                    else
                                    {
                                        result = " ''(не переименовано)''";
                                    }
                                    titles.Add("*" + subsection.ToString() + result);
                                    continue;
                                }
                                titles.Add("*" + subsection.ToString());
                            }
                        }
                    }
                    StringBuilder line = new StringBuilder(string.Join("\n*", titles.ConvertAll(c => c).ToArray()));
                    if (titles.Count > 0)
                    {
                        line.Insert(0, "\n*");
                    }
                    sw.Write(line.ToString());
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("|}\n");
                sw.WriteLine("{{/Окончание}}");
            }
        }

        private static bool MovedTo(Wiki wiki, string title, DateTime start, out string movedTo)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "revisions");
            parameters.Add("titles", title);
            parameters.Add("rvlimit", "max");
            parameters.Add("rvstart", start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
            parameters.Add("rvdir", "newer");
            parameters.Add("rvprop", "comment|timestamp");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList revisions = doc.SelectNodes("//rev[@comment]");
            Regex movedToRE = new Regex(@"переименовал «\[{2}([^\]]+)\]{2}» в «\[{2}([^\]]+)\]{2}»");
            Regex movedFromRE = new Regex(@"«\[{2}([^\]]+)\]{2}» переименована в «\[{2}([^\]]+)\]{2}»");
            foreach (XmlNode revision in revisions)
            {
                string comment = revision.Attributes["comment"].Value;
                Match toM = movedToRE.Match(comment);
                Match fromM = movedFromRE.Match(comment);
                if (toM.Success)
                {
                    if (toM.Groups[1].Value == title)
                    {
                        movedTo = toM.Groups[2].Value;
                        return true;
                    }
                }
                else if (fromM.Success)
                {
                    if (fromM.Groups[1].Value == title)
                    {
                        movedTo = fromM.Groups[2].Value;
                        return true;
                    }
                    else
                    {
                        movedTo = "";
                        return false;
                    }
                }
            }
            movedTo = "";
            return false;
        }

        private static void UpdateProposedMerges(Wiki wiki)
        {
            Console.Out.WriteLine("Updating proposed merges...");
            using (TextReader sr =
                        new StreamReader("union.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К объединению",
                    "1",
                    text,
                    "Обновление списка обсуждаемых страниц",
                    MinorFlags.Minor,
                    CreateFlags.NoCreate,
                    WatchFlags.Watch,
                    SaveFlags.Replace);
            }

            using (TextReader sr =
                        new StreamReader("union-archive.txt"))
            {
                DateTime today = DateTime.Today;
                DateTime currentMonth = new DateTime(today.Year, today.Month, 1);

                string page = currentMonth.AddMonths(-1).ToString("yyyy-MM");
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:Архив запросов на объединение/" + page, text, "Обновление списка обсуждаемых страниц");
            }
        }

        private static void ProcessProposedMerges(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения объединения страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList pages = doc.SelectNodes("//page");

            List<Day> days = new List<Day>();
            DateTime start = DateTime.Today;
            Regex closedRE = new Regex(@"{{ВПКОБ-Навигация}}\s*{{(Закрыто|Closed|закрыто|closed)}}");
            Regex closed2RE = new Regex(@"{{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПКОБ-Навигация}}");

            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К объединению/".Length);
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
                if (File.Exists("union-cache\\" + date + ".txt"))
                {
                    using (TextReader sr = new StreamReader("union-cache\\" + date + ".txt"))
                    {
                        string revid = sr.ReadLine();
                        if (revid == page.Attributes["lastrevid"].Value)
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("union-cache\\" + date + ".txt"))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }
                Match m = closedRE.Match(text);
                Match m2 = closed2RE.Match(text);
                if (m.Success || m2.Success)
                {
                    text = text.Replace("{{ВПКОБ-Навигация}}", "{{ВПКОБ-Навигация|nocat=1}}");
                    wiki.SavePage(pageName, text, "Обсуждение закрыто, убираем страницу из категории.");
                    continue;
                }
                day.Candidates = new List<Candidate>(GetCandidatesFromPage(text, false));
                days.Add(day);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter("union.txt"))
            {
                sw.WriteLine("== Текущие обсуждения ==\n");
                sw.WriteLine("{{Объединение статей/Статьи, вынесенные для объединения}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{Объединение статей/День|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {
                        titles.Add(candidate.ToString());
                    }
                    StringBuilder line = new StringBuilder(string.Join("\n* ", titles.ConvertAll(c => c).ToArray()));
                    if (titles.Count > 0)
                    {
                        line.Insert(0, "\n* ");
                    }
                    sw.Write(line.ToString());
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("{{/Подвал}}");
            }

            days.Clear();
            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            start = currentMonth.AddMonths(-1);
            while (start < currentMonth)
            {
                string pageName = "Википедия:К объединению/" + start.ToString("d MMMM yyyy");
                bool archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;

                Day day = new Day();
                day.Exists = true;
                day.Archived = archived;
                day.Date = start;
                Console.Out.WriteLine("Processing " + pageName + "...");
                string text = "";
                if (archived && File.Exists("union-cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                {
                    using (TextReader sr = new StreamReader("union-cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                    {
                        text = sr.ReadToEnd();
                    }
                }
                else
                {
                    try
                    {
                        text = wiki.LoadPage(pageName);
                    }
                    catch (WikiPageNotFound)
                    {
                        day.Archived = false;
                        day.Exists = false;
                        days.Add(day);
                        start = start.AddDays(1);
                        continue;
                    }
                    using (StreamWriter sw =
                        new StreamWriter("union-cache\\" + start.ToString("d MMMM yyyy") + ".txt"))
                    {
                        sw.Write(text);
                    }
                }
                day.Candidates = new List<Candidate>(GetCandidatesFromPage(text, archived));
                days.Add(day);

                start = start.AddDays(1);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter("union-archive.txt"))
            {
                sw.WriteLine("{| class=standard");
                sw.WriteLine("|-");
                sw.WriteLine("!| Дата");
                sw.WriteLine("!| Статьи, вынесенные на объединение");
                sw.WriteLine("|-\n");

                StringBuilder sb = new StringBuilder();
                foreach (Day day in days)
                {
                    sb.Append("{{Объединение статей/День|" + day.Date.ToString("yyyy-M-d") + "|");
                    if (!day.Archived && day.Exists)
                    {
                        sb.Append("''обсуждение не завершено''}}\n\n");
                        continue;
                    }
                    if (!day.Exists)
                    {
                        sb.Append("''нет обсуждений''}}\n\n");
                        continue;
                    }
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {
                        titles.Add(candidate.ToString() + candidate.SubsectionsToString());
                    }
                    sb.Append("\n* " + string.Join("\n* ", titles.ConvertAll(c => c).ToArray()));
                    sb.Append("}}\n\n");
                }
                sb.Replace("<s>", "");
                sb.Replace("</s>", "");
                sb.Replace("<strike>", "");
                sb.Replace("</strike>", "");

                sw.Write(sb.ToString());

                sw.WriteLine("|}");
            }
        }

        private static void UpdateArticlesForDeletion(Wiki wiki)
        {
            Console.Out.WriteLine("Updating articles for deletion...");
            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
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
        }

        private static void ProcessArticlesForDeletion(Wiki wiki)
        {
            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категория:Википедия:Незакрытые обсуждения удаления страниц";
            l10i.Culture = "ru-RU";
            l10i.Prefix = "Википедия:К удалению/";
            l10i.Template = "Удаление статей";
            l10i.TopTemplate = "/Заголовок";
            l10i.BottomTemplate = "/Подвал";
            ProcessArticlesForDeletion(wiki, l10i);
        }

        struct ArticlesForDeletionLocalization
        {
            public string Category;
            public string Prefix;
            public string Culture;
            public string Template;
            public string TopTemplate;
            public string BottomTemplate;
        }

        private static void ProcessArticlesForDeletion(Wiki wiki,
            ArticlesForDeletionLocalization l10i)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", l10i.Category);
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
                string date = pageName.Substring(l10i.Prefix.Length);
                Day day = new Day();
                try
                {
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture(l10i.Culture),
                        System.Globalization.DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
                {
                    continue;
                }
                Console.Out.WriteLine("Processing " + pageName + "...");
                string text = "";
                if (File.Exists("cache\\" + l10i.Culture + "\\" + date + ".txt"))
                {
                    using (TextReader sr = new StreamReader("cache\\" + l10i.Culture + "\\" + date + ".txt"))
                    {
                        string revid = sr.ReadLine();
                        if (revid == page.Attributes["lastrevid"].Value)
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("cache\\" + l10i.Culture + "\\" + date + ".txt"))
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
                        new StreamWriter("main-" + l10i.Culture + ".txt"))
            {
                sw.WriteLine("{{" + l10i.TopTemplate + "}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{" + "Удаление статей" + "|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    foreach (Candidate candidate in day.Candidates)
                    {
                        titles.Add(candidate.ToString() + candidate.SubsectionsToString());
                    }
                    sw.Write(string.Join(" • ", titles.ConvertAll(c => c).ToArray()));
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("{{" + l10i.BottomTemplate + "}}");
            }

            days.Clear();
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            DateTime start = currentMonth.AddMonths(-1);
            while (start < currentMonth)
            {
                string pageName = l10i.Prefix + start.ToString("d MMMM yyyy");
                if (doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") != null)
                {
                    start = start.AddDays(1);
                    continue;
                }

                Day day = new Day();
                day.Date = start;
                Console.Out.WriteLine("Processing " + pageName + "...");
                string text = "";
                if (File.Exists("cache\\" + l10i.Culture + "\\" + start.ToString("d MMMM yyyy") + ".txt"))
                {
                    using (TextReader sr = new StreamReader("cache\\" + l10i.Culture + "\\" + start.ToString("d MMMM yyyy") + ".txt"))
                    {
                        text = sr.ReadToEnd();
                    }
                }
                else
                {
                    text = wiki.LoadPage(pageName);
                    using (StreamWriter sw =
                        new StreamWriter("cache\\" + l10i.Culture + "\\" + start.ToString("d MMMM yyyy") + ".txt"))
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
                        new StreamWriter("archive-" + l10i.Culture + ".txt"))
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
        }

        struct Day
        {
            public List<Candidate> Candidates;
            public DateTime Date;
            public bool Archived;
            public bool Exists;
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
