using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
            //Wiki wiki = new Wiki("http://uk.wikipedia.org");
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

            /*ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категорія:Незавершені обговорення вилучення сторінок";
            l10i.Culture = "uk-UA";
            l10i.MainPage = "Вікіпедія:Статті-кандидати на вилучення";
            l10i.Template = "Вилучення статей";
            l10i.TopTemplate = "/шапка";
            l10i.BottomTemplate = "/низ";
            l10i.Result = "Підсумок";
            l10i.Language = "uk";
            l10i.MainPageUpdateComment = "оновлення даних";
            l10i.ArchiveTemplate = "Статті, винесені на вилучення";
            l10i.ArchivePage = "Вікіпедія:Архів запитів на вилучення/";
            l10i.EmptyArchive = "обговорення не розпочато";
            l10i.Processor = RemoveVotes;
            l10i.StrikeOutComment = "закреслення заголовків";
            l10i.AutoResultMessage = "Сторінка була вилучена адміністратором [[User:{0}|]]. Була вказана наступна причина: «{2}». Це повідомлення було автоматично згенеровано ботом ~~~~.\n";
            l10i.DateFormat = "d MMMM yyyy в HH:mm (UTC)";
            l10i.AutoResultComment = ", підбиття підсумків";

            ArticlesForDeletion afd = new ArticlesForDeletion(l10i);
            afd.Analyse(wiki);
            afd.UpdateMainPage(wiki);
            afd.UpdateArchive(wiki);
            afd.UpdatePages(wiki);*/

            ProcessArticlesForDeletion(wiki);
            UpdateArticlesForDeletion(wiki);

            ProcessProposedMerges(wiki);
            UpdateProposedMerges(wiki);

            ProcessRequestedMoves(wiki);
            UpdateRequestedMoves(wiki);

            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        private static void StrikeOutSection(WikiPageSection section)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                section.Subsections.Count(s => s.Title.Trim() == "Общий итог") > 0)
            {
                if (!section.Title.Contains("<s>"))
                {
                    section.Title = string.Format(" <s>{0}</s> ", section.Title.Trim());
                }

                foreach (WikiPageSection subsection in section.Subsections)
                {
                    Match m = wikiLinkRE.Match(subsection.Title);
                    if (m.Success && !subsection.Title.Contains("<s>"))
                    {
                        subsection.Title = string.Format(" <s>{0}</s> ", subsection.Title.Trim());
                    }
                }
            }
            section.ForEach(StrikeOutSection);
        }

        private static void RemoveStrikeOut(WikiPageSection section)
        {
            section.ForEach(RemoveStrikeOut);
            if (section.Subsections.Count(s => s.Title.Trim() == "Итог") == 0 &&
                section.Subsections.Count(s => s.Title.Trim() == "Общий итог") == 0)
            {
                if (section.Title.Contains("<s>"))
                {
                    section.Title = section.Title.Replace("<s>", "");
                    section.Title = section.Title.Replace("</s>", "");
                }
            }
        }

        private static string SubsectionsList(WikiPageSection section, string aggregator)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Match m = wikiLinkRE.Match(section.Title);
            if (m.Success)
            {
                aggregator = aggregator + " • " + section.Title.Trim();
            }
            aggregator = section.Reduce(aggregator, SubsectionsList);
            return aggregator;
        }

        private static List<WikiPageSection> SubsectionsList2(WikiPageSection section, List<WikiPageSection> aggregator)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Match m = wikiLinkRE.Match(section.Title);
            if (m.Success)
            {
                aggregator.Add(section);
            }
            return section.Reduce(aggregator, SubsectionsList2);
        }

        private static void UpdateRequestedMoves(Wiki wiki)
        {
            Console.Out.WriteLine("Updating requested moves...");
            using (TextReader sr =
                        new StreamReader("move.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К переименованию", text, "обновление");
            }
            
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения переименования страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList pages = doc.SelectNodes("//page");
            foreach (XmlNode page in pages)
            {
                int results = 0;
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К переименованию/".Length);
                Day day = new Day();
                try
                {
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        System.Globalization.DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
                {
                    continue;
                }
                Console.Out.WriteLine("Updating " + pageName + "...");
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
                day.Page = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    RemoveStrikeOut(section);
                    section.ForEach(RemoveStrikeOut);
                    section.ForEach(StrikeOutSection);
                    if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                        section.Subsections.Count(s => s.Title.Trim() == "Общий итог") > 0)
                    {
                        if (!section.Title.Contains("<s>"))
                        {
                            section.Title = string.Format("<s>{0}</s>", section.Title.Trim());
                        }

                        foreach (WikiPageSection subsection in section.Subsections)
                        {
                            Match m = wikiLinkRE.Match(subsection.Title);
                            if (m.Success && !subsection.Title.Contains("<s>"))
                            {
                                subsection.Title = string.Format(" <s>{0}</s> ", subsection.Title.Trim());
                            }
                        }
                    }
                    else if (section.Subsections.Count(s => s.Title.Trim() == "Оспоренный итог") == 0)
                    {
                        Match m = wikiLinkRE.Match(section.Title);
                        if (m.Success)
                        {
                            string link = m.Groups[1].Value;
                            string movedTo;
                            string movedBy;
                            DateTime movedAt;

                            DateTime start = day.Date;
                            m = timeRE.Match(section.Text);
                            if (m.Success)
                            {
                                start = DateTime.Parse(m.Groups[1].Value,
                                    CultureInfo.CreateSpecificCulture("ru-RU"),
                                    DateTimeStyles.AssumeUniversal);
                            }

                            bool moved = MovedTo(wiki,
                                link,
                                start,
                                out movedTo,
                                out movedBy,
                                out movedAt);

                            if (moved && !string.IsNullOrEmpty(movedTo))
                            {
                                string message = string.Format("Страница была переименована {2} в «[[{0}]]» участником [[User:{1}|]]. Данное сообщение было автоматически сгенерировано ботом ~~~~.\n",
                                movedTo,
                                movedBy,
                                movedAt.ToUniversalTime().ToString("d MMMM yyyy в HH:mm (UTC)"));
                                WikiPageSection verdict = new WikiPageSection(" Итог ",
                                    section.Level + 1,
                                    message);
                                section.AddSubsection(verdict);
                                ++results;
                            }
                        }
                    }
                }
                string newText = day.Page.Text;
                if (newText.Trim() == text.Trim())
                {
                    continue;
                }
                try
                {
                    wiki.SavePage(pageName,
                        newText,
                        "зачёркивание заголовков" + (results > 0 ? ", сообщение об итогах" : ""));
                }
                catch (WikiException)
                {
                }
            }
        }

        private static void ProcessRequestedMoves(Wiki wiki)
        {
            Directory.CreateDirectory("moves-cache");
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
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter("move.txt"))
            {
                sw.WriteLine("{{/Шапка}}\n");
                sw.WriteLine("{{Переименование статей/Статьи, вынесенные на переименование}}\n");

                Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

                foreach (Day day in days)
                {
                    sw.Write("{{Переименование статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");
                    List<string> titles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        string result = "";
                        RemoveStrikeOut(section);
                        section.ForEach(RemoveStrikeOut);
                        section.ForEach(StrikeOutSection);
                        if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                            section.Subsections.Count(s => s.Title.Trim() == "Общий итог") > 0 ||
                            section.Title.Contains("<s>"))
                        {
                            if (!section.Title.Contains("<s>"))
                            {
                                section.Title = string.Format("<s>{0}</s>", section.Title.Trim());
                            }
                            Match m = wikiLinkRE.Match(section.Title);
                            if (m.Success)
                            {
                                string link = m.Groups[1].Value;
                                string movedTo;
                                bool moved = MovedTo(wiki, link, day.Date, out movedTo);
                                
                                if (moved && string.IsNullOrEmpty(movedTo))
                                {
                                    result = " ''(переименовано)''";
                                }
                                else if (moved)
                                {
                                    result = string.Format(" ''(переименовано в «[[{0}]]»)''", movedTo);
                                }
                                else
                                {
                                    result = " ''(не переименовано)''";
                                }
                            }

                            foreach (WikiPageSection subsection in section.Subsections)
                            {
                                m = wikiLinkRE.Match(subsection.Title);
                                if (m.Success && !subsection.Title.Contains("<s>"))
                                {
                                    subsection.Title = string.Format(" <s>{0}</s> ", subsection.Title.Trim());
                                }
                            }
                        }
                        
                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        titles.Add(filler + " " + section.Title.Trim() + result);
                        
                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList2);
                        foreach (WikiPageSection subsection in sections)
                        {
                            result = "";
                            if (subsection.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                                subsection.Title.Contains("<s>"))
                            {
                                Match m = wikiLinkRE.Match(subsection.Title);
                                if (m.Success)
                                {
                                    string link = m.Groups[1].Value;
                                    string movedTo;
                                    bool moved = MovedTo(wiki, link, day.Date, out movedTo);

                                    if (moved && string.IsNullOrEmpty(movedTo))
                                    {
                                        result = " ''(переименовано)''";
                                    }
                                    else if (moved)
                                    {
                                        result = string.Format(" ''(переименовано в «[[{0}]]»)''", movedTo);
                                    }
                                    else
                                    {
                                        result = " ''(не переименовано)''";
                                    }
                                }
                            }
                            filler = "";
                            for (int i = 0; i < subsection.Level - 1; ++i)
                            {
                                filler += "*";
                            }
                            titles.Add(filler + " " + subsection.Title.Trim() + result);
                        }
                    }
                    if (titles.Count(s => s.Contains("=")) > 0)
                    {
                        titles[0] = "2=<li>" + titles[0].Substring(2) + "</li>";
                    }
                    sw.Write(string.Join("\n", titles.ConvertAll(c => c).ToArray()));
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("|}\n");
                sw.WriteLine("{{/Окончание}}");
            }
        }

        private static bool MovedTo(Wiki wiki,
            string title,
            DateTime start,
            out string movedTo)
        {
            string movedBy;
            DateTime movedAt;
            return MovedTo(wiki, title, start, out movedTo, out movedBy, out movedAt);
        }

        private static bool MovedTo(Wiki wiki,
            string title,
            DateTime start,
            out string movedTo,
            out string movedBy,
            out DateTime movedAt)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("list", "logevents");
            parameters.Add("letitle", title);
            parameters.Add("letype", "move");
            parameters.Add("lestart", start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
            parameters.Add("ledir", "newer");
            parameters.Add("lelimit", "max");
            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList moved = doc.SelectNodes("//move");
            List<Revision> revs = new List<Revision>();
            foreach (XmlNode revision in moved)
            {
                revs.Add(new Revision(revision.Attributes["new_title"].Value,
                    revision.ParentNode.Attributes["comment"].Value,
                    revision.ParentNode.Attributes["timestamp"].Value,
                    revision.ParentNode.Attributes["user"].Value));
            }
            revs.Sort(CompareRevisions);
            if (revs.Count > 0)
            {
                bool result = MovedTo(wiki,
                    revs[0].MovedTo,
                    revs[0].Time,
                    out movedTo,
                    out movedBy,
                    out movedAt);
                if (result)
                {
                    return movedTo != title;
                }
                else
                {
                    movedTo = revs[0].MovedTo;
                    movedBy = revs[0].User;
                    movedAt = revs[0].Time;
                    return movedTo != title;
                }
            }
            movedTo = "";
            movedBy = "";
            movedAt = new DateTime();
            return false;
        }

        struct Revision
        {
            public string Comment;
            public DateTime Time;
            public string User;
            public string MovedTo;

            public Revision(string movedTo, string comment, string time, string user)
            {
                MovedTo = movedTo;
                Comment = comment;
                Time = DateTime.Parse(time, null, DateTimeStyles.AssumeUniversal);
                User = user;
            }
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
                    "обновление",
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
                wiki.SavePage("Википедия:Архив запросов на объединение/" + page, text, "обновление");
            }

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
                Console.Out.WriteLine("Updating " + pageName + "...");
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
                day.Page = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    RemoveStrikeOut(section);
                    section.ForEach(RemoveStrikeOut);
                    StrikeOutSection(section);
                    section.ForEach(StrikeOutSection);
                }
                
                string newText = day.Page.Text;
                if (newText.Trim() == text.Trim())
                {
                    continue;
                }
                try
                {
                    wiki.SavePage(pageName, newText, "зачёркивание заголовков");
                }
                catch (WikiException)
                {
                }
            }
        }

        private static void ProcessProposedMerges(Wiki wiki)
        {
            Directory.CreateDirectory("union-cache");
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
                    wiki.SavePage(pageName, text, "обсуждение закрыто, убираем страницу из категории");
                    continue;
                }
                day.Page = WikiPage.Parse(pageName, text);
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
                    sw.Write("{{Объединение статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");

                    List<string> titles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        RemoveStrikeOut(section);
                        section.ForEach(RemoveStrikeOut);
                        StrikeOutSection(section);
                        section.ForEach(StrikeOutSection);

                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        titles.Add(filler + " " + section.Title.Trim());
                        
                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList2);
                        foreach (WikiPageSection subsection in sections)
                        {
                            filler = "";
                            for (int i = 0; i < subsection.Level - 1; ++i)
                            {
                                filler += "*";
                            }
                            titles.Add(filler + " " + subsection.Title.Trim());
                        }
                    }
                    if (titles.Count(s => s.Contains("=")) > 0)
                    {
                        titles[0] = "2=<li>" + titles[0].Substring(2) + "</li>";
                    }
                    sw.Write(string.Join("\n", titles.ConvertAll(c => c).ToArray()));
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
                day.Page = WikiPage.Parse(pageName, text);
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
                    sb.Append("{{Объединение статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");
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
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        titles.Add(filler + " " + section.Title.Trim());

                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList2);
                        foreach (WikiPageSection subsection in sections)
                        {
                            filler = "";
                            for (int i = 0; i < subsection.Level - 1; ++i)
                            {
                                filler += "*";
                            }
                            titles.Add(filler + " " + subsection.Title.Trim());
                        }
                    }
                    if (titles.Count(s => s.Contains("=")) > 0)
                    {
                        titles[0] = "2=<li>" + titles[0].Substring(2) + "</li>";
                    }
                    sb.Append(string.Join("\n", titles.ConvertAll(c => c).ToArray()));
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
                        new StreamReader("main-ru-RU.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К удалению", text, "обновление");
            }
            using (TextReader sr =
                        new StreamReader("archive-ru-RU.txt"))
            {
                string page = currentMonth.AddMonths(-1).ToString("yyyy-MM");
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:Архив запросов на удаление/" + page, text, "обновление");
            }

            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категория:Википедия:Незакрытые обсуждения удаления страниц";
            l10i.Culture = "ru-RU";
            l10i.MainPage = "Википедия:К удалению/";
            l10i.Template = "Удаление статей";
            l10i.TopTemplate = "/Заголовок";
            l10i.BottomTemplate = "/Подвал";

            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Regex re = new Regex(@"<s>\[{2}(.+?)(\|.+?)?]{2}</s>");
            Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", l10i.Category);
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            XmlNodeList pages = doc.SelectNodes("//page");
            foreach (XmlNode page in pages)
            {
                int results = 0;
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring(l10i.MainPage.Length);
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
                Console.Out.WriteLine("Updating " + pageName + "...");
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
                day.Page = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    section.ForEach(RemoveStrikeOut);
                    if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                        section.Subsections.Count(s => s.Title.Trim() == "Общий итог") > 0)
                    {
                        if (!section.Title.Contains("<s>"))
                        {
                            section.Title = string.Format("<s>{0}</s>", section.Title.Trim());
                        }

                        foreach (WikiPageSection subsection in section.Subsections)
                        {
                            Match m = wikiLinkRE.Match(subsection.Title);
                            if (m.Success && !subsection.Title.Contains("<s>"))
                            {
                                subsection.Title = string.Format(" <s>{0}</s> ", subsection.Title.Trim());
                            }
                        }
                    }
                    else
                    {
                        DateTime start = day.Date;
                        string title = "";
                        Match m = re.Match(section.Title);
                        if (m.Success)
                        {
                            title = m.Groups[1].Value.Trim();
                        }
                        else
                        {
                            m = wikiLinkRE.Match(section.Title);
                            if (m.Success)
                            {
                                title = m.Groups[1].Value.Trim();
                            }
                        }
                        m = timeRE.Match(section.Text);
                        if (m.Success)
                        {
                            start = DateTime.Parse(m.Groups[1].Value,
                                CultureInfo.CreateSpecificCulture("ru-RU"),
                                DateTimeStyles.AssumeUniversal);
                        }
                        parameters.Clear();
                        parameters.Add("list", "logevents");
                        parameters.Add("letype", "delete");
                        parameters.Add("lemlimit", "max");
                        parameters.Add("lestart", start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        parameters.Add("ledir", "newer");
                        parameters.Add("letitle", title);
                        XmlDocument xml = wiki.Enumerate(parameters, true);
                        XmlNodeList items = xml.SelectNodes("//item");
                        List<DeleteLogEvent> events = new List<DeleteLogEvent>();
                        foreach (XmlNode item in items)
                        {
                            DeleteLogEvent ev = new DeleteLogEvent();
                            ev.Comment = item.Attributes["comment"].Value;
                            ev.Deleted = item.Attributes["action"].Value == "delete";
                            ev.User = item.Attributes["user"].Value;
                            ev.Timestamp = DateTime.Parse(item.Attributes["timestamp"].Value,
                                null,
                                DateTimeStyles.AssumeUniversal);
                            events.Add(ev);
                        }
                        events.Sort(CompareDeleteLogEvents);
                        if (events.Count > 0 && events[0].Deleted)
                        {
                            Regex commentRE = new Regex(@"(.+?):&#32;(.+)");
                            m = commentRE.Match(events[0].Comment);
                            string comment;
                            if (m.Success)
                            {
                                comment = m.Groups[1].Value;
                            }
                            else
                            {
                                comment = events[0].Comment;
                            }
                            string message = string.Format("Страница была удалена {1} администратором [[User:{0}|]]. Была указана следующая причина: «{2}». Данное сообщение было автоматически сгенерировано ботом ~~~~.\n",
                                events[0].User,
                                events[0].Timestamp.ToUniversalTime().ToString("d MMMM yyyy в HH:mm (UTC)"),
                                comment);
                            WikiPageSection verdict = new WikiPageSection(" Итог ",
                                section.Level + 1,
                                message);
                            section.AddSubsection(verdict);
                            ++results;
                        }
                    }
                    section.ForEach(StrikeOutSection);
                }
                string newText = day.Page.Text;
                if (newText.Trim() == text.Trim())
                {
                    continue;
                }
                try
                {
                    wiki.SavePage(pageName,
                        newText,
                        "зачёркивание заголовков" + (results > 0 ? " и подведение итогов" : ""));
                }
                catch (WikiException)
                {
                }
            }
        }

        

        private static void ProcessArticlesForDeletion(Wiki wiki)
        {
            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категория:Википедия:Незакрытые обсуждения удаления страниц";
            l10i.Culture = "ru-RU";
            l10i.MainPage = "Википедия:К удалению/";
            l10i.Template = "Удаление статей";
            l10i.TopTemplate = "/Заголовок";
            l10i.BottomTemplate = "/Подвал";
            ProcessArticlesForDeletion(wiki, l10i);
        }

        private static void ProcessArticlesForDeletion(Wiki wiki,
            ArticlesForDeletionLocalization l10i)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            Directory.CreateDirectory("cache\\" + l10i.Culture);
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
                string date = pageName.Substring(l10i.MainPage.Length);
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
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);
            using (StreamWriter sw =
                        new StreamWriter("main-" + l10i.Culture + ".txt"))
            {
                sw.WriteLine("{{" + l10i.TopTemplate + "}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{" + "Удаление статей" + "|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();
                    
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                            section.Subsections.Count(s => s.Title.Trim() == "Общий итог") > 0)
                        {
                            if (!section.Title.Contains("<s>"))
                            {
                                section.Title = string.Format("<s>{0}</s>", section.Title.Trim());
                            }

                            foreach (WikiPageSection subsection in section.Subsections)
                            {
                                Match m = wikiLinkRE.Match(subsection.Title);
                                if (m.Success && !subsection.Title.Contains("<s>"))
                                {
                                    subsection.Title = string.Format(" <s>{0}</s> ", subsection.Title.Trim());
                                }
                            }
                        }
                        section.ForEach(StrikeOutSection);
                        string result = section.Reduce("", SubsectionsList);
                        if (result.Length > 0)
                        {
                            result = " • <small>" + result.Substring(3) + "</small>";
                        }
                        titles.Add(section.Title.Trim() + result);
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
                string pageName = l10i.MainPage + start.ToString("d MMMM yyyy");
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
                day.Page = WikiPage.Parse(pageName, text);
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
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string result = section.Reduce("", SubsectionsList);
                        if (result.Length > 0)
                        {
                            result = " • <small>" + result.Substring(3) + "</small>";
                        }
                        titles.Add(section.Title.Trim() + result);
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

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        static int CompareRevisions(Revision x, Revision y)
        {
            return y.Time.CompareTo(x.Time);
        }

        static int CompareDeleteLogEvents(DeleteLogEvent x, DeleteLogEvent y)
        {
            return y.Timestamp.CompareTo(x.Timestamp);
        }

        static string RemoveVotes(WikiPageSection section)
        {
            Regex re = new Regex(@"\s+\d{1,3}—\d{1,3}(—\d{1,3})?\s*(</s>)?\s*$");
            return re.Replace(section.Title, "$2");
        }
    }

    internal struct Day
    {
        public WikiPage Page;
        public DateTime Date;
        public bool Archived;
        public bool Exists;
    }

    internal struct DeleteLogEvent
    {
        public string Comment;
        public string User;
        public bool Deleted;
        public DateTime Timestamp;
    }

    internal struct ArticlesForDeletionLocalization
    {
        public string Language;
        public string Category;
        public string MainPage;
        public string Culture;
        public string Template;
        public string TopTemplate;
        public string BottomTemplate;
        public string Result;
        public string MainPageUpdateComment;
        public string ArchiveTemplate;
        public string ArchivePage;
        public string EmptyArchive;
        public ArticlesForDeletion.TitleProcessor Processor;
        public string StrikeOutComment;
        public string AutoResultMessage;
        public string DateFormat;
        public string AutoResultComment;
    }
}
