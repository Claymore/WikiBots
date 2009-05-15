using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Claymore.SharpMediaWiki;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Globalization;

namespace Claymore.TalkCleanupWikiBot
{
    internal class RequestedMoves
    {
        private string _cacheDir;
        private string _language;

        public RequestedMoves()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\RequestedMoves\\";
        }

        public void Analyze(Wiki wiki)
        {
            Directory.CreateDirectory(_cacheDir);
            
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
            Regex closedRE = new Regex(@"(\{{2}ВПКПМ-Навигация\}{2}\s*\{{2}(Закрыто|Closed|закрыто|closed)\}{2})|(\{{2}(Закрыто|Closed|закрыто|closed)\}{2}\s*\{{2}ВПКПМ-Навигация\}{2})");

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
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
                {
                    continue;
                }

                string fileName = _cacheDir + date + ".bin";
                string text = "";
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                    using (TextReader sr = new StreamReader(gs))
                    {
                        string revid = sr.ReadLine();
                        if (revid == page.Attributes["lastrevid"].Value)
                        {
                            Console.Out.WriteLine("Loading " + pageName + "...");
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);

                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }
                Match m = closedRE.Match(text);
                if (m.Success)
                {
                    text = text.Replace("{{ВПКПМ-Навигация}}", "{{ВПКПМ-Навигация|nocat=1}}");
                    try
                    {
                        string revid = wiki.SavePage(pageName,
                            text,
                            "обсуждение закрыто, убираем страницу из категории");

                        using (FileStream fs = new FileStream(fileName, FileMode.Create))
                        using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                        using (StreamWriter sw = new StreamWriter(gs))
                        {
                            sw.WriteLine(revid);
                            sw.Write(text);
                        }
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
                        new StreamWriter(_cacheDir + "MainPage.txt"))
            {
                sw.WriteLine("{{/Шапка}}\n");
                sw.WriteLine("{{Переименование статей/Статьи, вынесенные на переименование}}\n");

                Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

                foreach (Day day in days)
                {
                    Console.Out.WriteLine("Analyzing " + day.Date.ToString("d MMMM yyyy") + "...");
                    sw.Write("{{Переименование статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");
                    List<string> titles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        string result = "";
                        RemoveStrikeOut(section);
                        StrikeOutSection(section);
                        if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0 ||
                            section.Title.Contains("<s>"))
                        {
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
                        }

                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        titles.Add(filler + " " + section.Title.Trim() + result);

                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList);
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

        public void UpdateMainPage(Wiki wiki)
        {
            Console.Out.WriteLine("Updating requested moves...");
            using (TextReader sr =
                        new StreamReader(_cacheDir + "MainPage.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:К переименованию", text, "обновление");
            }
        }

        public void UpdatePages(Wiki wiki)
        {
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
                        DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
                {
                    continue;
                }
                
                string fileName = _cacheDir + date + ".bin";
                string text = "";
                if (File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                    using (TextReader sr = new StreamReader(gs))
                    {
                        string revid = sr.ReadLine();
                        if (revid == page.Attributes["lastrevid"].Value)
                        {
                            Console.Out.WriteLine("Loading " + pageName + "...");
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);

                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }
                day.Page = WikiPage.Parse(pageName, text);
                
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    RemoveStrikeOut(section);
                    StrikeOutSection(section);
                    if (section.Subsections.Count(s => s.Title.Trim() == "Оспоренный итог") == 0 &&
                        section.Subsections.Count(s => s.Title.Trim() == "Итог") == 0)
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
                                StrikeOutSection(section);
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
                    Console.Out.WriteLine("Updating " + pageName + "...");
                    string revid = wiki.SavePage(pageName,
                        newText,
                        "зачёркивание заголовков" + (results > 0 ? ", сообщение об итогах" : ""));

                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(revid);
                        sw.Write(text);
                    }
                }
                catch (WikiException)
                {
                }
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

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        static int CompareRevisions(Revision x, Revision y)
        {
            return y.Time.CompareTo(x.Time);
        }

        private void StrikeOutSection(WikiPageSection section)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0)
            {
                if (!section.Title.Contains("<s>"))
                {
                    section.Title = string.Format(" <s>{0}</s> ",
                        section.Title.Trim());
                }

                foreach (WikiPageSection subsection in section.Subsections)
                {
                    Match m = wikiLinkRE.Match(subsection.Title);
                    if (m.Success && !subsection.Title.Contains("<s>"))
                    {
                        subsection.Title = string.Format(" <s>{0}</s> ",
                            subsection.Title.Trim());
                    }
                }
            }
            section.ForEach(StrikeOutSection);
        }

        private void RemoveStrikeOut(WikiPageSection section)
        {
            if (section.Subsections.Count(s => s.Title.Trim() == "Итог") == 0)
            {
                if (section.Title.Contains("<s>"))
                {
                    section.Title = section.Title.Replace("<s>", "");
                    section.Title = section.Title.Replace("</s>", "");
                }
            }
            section.ForEach(RemoveStrikeOut);
        }

        private static List<WikiPageSection> SubsectionsList(WikiPageSection section,
            List<WikiPageSection> aggregator)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Match m = wikiLinkRE.Match(section.Title);
            if (m.Success)
            {
                aggregator.Add(section);
            }
            return section.Reduce(aggregator, SubsectionsList);
        }
    }
}
