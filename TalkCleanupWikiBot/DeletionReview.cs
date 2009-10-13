using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.TalkCleanupWikiBot
{
    internal class DeletionReview : IModule
    {
        private readonly string _cacheDir;
        private string _language;
        private string[] _results;
        private static Regex _wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

        public DeletionReview()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\DeletionReview\\";
            Directory.CreateDirectory(_cacheDir);
            _results = new string[] { "Итог", "Автоматический итог" };
        }

        public void Analyze(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения восстановления страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info|revisions");
            parameters.Add("rvprop", "timestamp");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            List<Day> days = new List<Day>();
            DateTime start = DateTime.Today;
            Regex closedRE = new Regex(@"({{ВПВУС-Навигация}}\s*{{(Закрыто|Closed|закрыто|closed)}})|({{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПВУС-Навигация}})");

            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К восстановлению/".Length);
                DateTime cutOffDate = new DateTime(2008, 11, 13);
                Day day = new Day();
                if (!DateTime.TryParse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day.Date))
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
                    text = wiki.LoadText(pageName);
                    
                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }
                DateTime lastEdit = DateTime.Parse(page.FirstChild.FirstChild.Attributes["timestamp"].Value, null, DateTimeStyles.AssumeUniversal);
                Match m = closedRE.Match(text);
                if ((DateTime.Now - lastEdit).TotalDays > 2 && (m.Success || day.Date < cutOffDate))
                {
                    Console.Out.WriteLine("Closing " + pageName + "...");
                    text = text.Replace("{{ВПВУС-Навигация}}", "{{ВПВУС-Навигация|nocat=1}}");
                    wiki.Save(pageName,
                        text,
                        "обсуждение закрыто");
                    continue;
                }
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);

            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            List<string> sectionTitles = new List<string>();
            foreach (Day day in days)
            {
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    RemoveStrikeOut(section);
                    StrikeOutSection(section);
                    if (section.Title.Contains("<s>"))
                    {
                        Match m = wikiLinkRE.Match(section.Title);
                        if (m.Success)
                        {
                            sectionTitles.Add(m.Groups[1].Value);
                        }
                    }

                    List<WikiPageSection> sections = new List<WikiPageSection>();
                    section.Reduce(sections, SubsectionsList);
                    foreach (WikiPageSection subsection in sections)
                    {
                        if (subsection.Title.Contains("<s>"))
                        {
                            Match m = wikiLinkRE.Match(subsection.Title);
                            if (m.Success)
                            {
                                sectionTitles.Add(m.Groups[1].Value);
                            }
                        }
                    }
                }
            }

            parameters.Clear();
            parameters.Add("prop", "info");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, sectionTitles);

            using (StreamWriter sw =
                        new StreamWriter(_cacheDir + "MainPage.txt"))
            {
                sw.WriteLine("== Текущие обсуждения ==\n");
                sw.WriteLine("{{Восстановление статей/Статьи, вынесенные на обсуждение восстановления}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{Восстановление статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");

                    List<string> titles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string result = "";
                        string filler = "";
                        RemoveStrikeOut(section);
                        StrikeOutSection(section);

                        if (section.Title.Contains("<s>"))
                        {
                            Match m = wikiLinkRE.Match(section.Title);
                            if (m.Success)
                            {
                                string link = m.Groups[1].Value;
                                XmlNode node = xml.SelectSingleNode("//page[@title='" + link.Replace("'", @"&apos;") + "']");
                                if (node != null)
                                {
                                    if (node.Attributes["missing"] == null)
                                    {
                                        WikiPageSection autoresult = section.Subsections.FirstOrDefault(s => s.Title.Trim() == "Автоматический итог");
                                        if (autoresult != null &&
                                            autoresult.SectionText.Contains("Страница была создана заново"))
                                        {
                                            result = " ''(создана заново)''";
                                        }
                                        else
                                        {
                                            result = " ''(восстановлена)''";
                                        }
                                    }
                                    else
                                    {
                                        result = " ''(не восстановлена)''";
                                    }
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
                            if (subsection.Title.Contains("<s>"))
                            {
                                Match m = wikiLinkRE.Match(subsection.Title);
                                if (m.Success)
                                {
                                    string link = m.Groups[1].Value;
                                    XmlNode node = xml.SelectSingleNode("//page[@title='" + link + "']");
                                    if (node != null)
                                    {
                                        if (node.Attributes["missing"] == null)
                                        {
                                            WikiPageSection autoresult = subsection.Subsections.FirstOrDefault(s => s.Title.Trim() == "Автоматический итог");
                                            if (autoresult != null &&
                                                autoresult.SectionText.Contains("Страница была создана заново"))
                                            {
                                                result = " ''(создана заново)''";
                                            }
                                            else
                                            {
                                                result = " ''(восстановлена)''";
                                            }
                                        }
                                        else
                                        {
                                            result = " ''(не восстановлена)''";
                                        }
                                    }
                                }
                            }
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
                        titles[0] = "2=<div>" + titles[0].Substring(2);
                    }
                    sw.Write(string.Join("\n", titles.ConvertAll(c => c).ToArray()));
                    if (titles.Count(s => s.Contains("=")) > 0)
                    {
                        sw.Write("</div>");
                    }
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("{{/Подвал}}");
            }
        }

        public void UpdateMainPage(Wiki wiki)
        {
            Console.Out.WriteLine("Updating deletion reviews...");
            using (TextReader sr =
                        new StreamReader(_cacheDir + "MainPage.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SaveSection("Википедия:К восстановлению",
                    "1",
                    text,
                    "обновление");
            }
        }

        public void UpdateArchivePages(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения восстановления страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            DateTime minDate = DateTime.Now;
            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К восстановлению/".Length);
                DateTime day;
                if (DateTime.TryParse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day))
                {
                    if (day < minDate)
                    {
                        minDate = day;
                    }
                }
                else
                {
                    continue;
                }
            }

            List<string> titles = new List<string>();
            minDate = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime currentMonth = new DateTime(DateTime.Today.Year,
                DateTime.Today.Month, 1);
            DateTime start = minDate;
            while (start <= currentMonth)
            {
                string date = start.ToString("yyyy-MM");
                string pageName = "Википедия:Архив запросов на восстановление/" + date;
                titles.Add(pageName);
                start = start.AddMonths(1);
            }

            parameters.Clear();
            parameters.Add("prop", "info");

            XmlDocument archivesDoc = wiki.Query(QueryBy.Titles, parameters, titles);
            pages = archivesDoc.SelectNodes("//page");
            foreach (XmlNode archivePage in pages)
            {
                string archiveName = archivePage.Attributes["title"].Value;
                string date = archiveName.Substring("Википедия:Архив запросов на восстановление/".Length);
                DateTime archiveDate;
                if (!DateTime.TryParse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out archiveDate))
                {
                    continue;
                }
                
                string fileName = _cacheDir + "Archive-" + date + ".txt";
                start = archiveDate;
                DateTime end = start.AddMonths(1);
                titles.Clear();
                while (start < end)
                {
                    string pageDate = start.ToString("d MMMM yyyy",
                        CultureInfo.CreateSpecificCulture("ru-RU"));
                    string prefix = "Википедия:К восстановлению/";
                    string pageName = prefix + pageDate;
                    titles.Add(pageName);

                    start = start.AddDays(1);
                }

                parameters.Clear();
                parameters.Add("prop", "info");

                List<Day> days = new List<Day>();
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles);
                XmlNodeList archives = xml.SelectNodes("//page");
                foreach (XmlNode page in archives)
                {
                    string pageName = page.Attributes["title"].Value;
                    string dateString = pageName.Substring("Википедия:К восстановлению/".Length);

                    string pageFileName = _cacheDir + dateString + ".bin";
                    Day day = new Day();
                    day.Archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;

                    if (!DateTime.TryParse(dateString,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day.Date))
                    {
                        continue;
                    }

                    if (page.Attributes["missing"] != null)
                    {
                        day.Exists = false;
                        days.Add(day);
                        continue;
                    }

                    string text = LoadPageFromCache(pageFileName,
                        page.Attributes["lastrevid"].Value, pageName);

                    if (string.IsNullOrEmpty(text))
                    {
                        Console.Out.WriteLine("Downloading " + pageName + "...");
                        text = wiki.LoadText(pageName);
                        CachePage(pageFileName, page.Attributes["lastrevid"].Value, text);
                    }

                    day.Exists = true;
                    day.Page = WikiPage.Parse(pageName, text);
                    days.Add(day);
                }

                days.Sort(CompareDays);

                StringBuilder textBuilder = new StringBuilder();

                textBuilder.AppendLine("{| class=standard");
                textBuilder.AppendLine("|-");
                textBuilder.AppendLine("!| Дата");
                textBuilder.AppendLine("!| Статьи, вынесенные на восстановление");
                textBuilder.AppendLine("|-\n");

                StringBuilder sb = new StringBuilder();
                foreach (Day day in days)
                {
                    sb.Append("{{Восстановление статей/День|" + day.Date.ToString("yyyy-M-d") + "|\n");
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
                    List<string> sectionTitles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        sectionTitles.Add(filler + " " + section.Title.Trim());

                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList);
                        foreach (WikiPageSection subsection in sections)
                        {
                            filler = "";
                            for (int i = 0; i < subsection.Level - 1; ++i)
                            {
                                filler += "*";
                            }
                            sectionTitles.Add(filler + " " + subsection.Title.Trim());
                        }
                    }
                    if (sectionTitles.Count(s => s.Contains("=")) > 0)
                    {
                        titles[0] = "2=<li>" + titles[0].Substring(2) + "</li>";
                    }
                    sb.Append(string.Join("\n", sectionTitles.ConvertAll(c => c).ToArray()));
                    sb.Append("}}\n\n");
                }
                sb.Replace("<s>", "");
                sb.Replace("</s>", "");
                sb.Replace("<strike>", "");
                sb.Replace("</strike>", "");

                textBuilder.Append(sb.ToString());

                textBuilder.AppendLine("|}");

                if (File.Exists(fileName))
                {
                    using (TextReader sr = new StreamReader(fileName))
                    {
                        string text = sr.ReadToEnd();
                        if (text == textBuilder.ToString())
                        {
                            continue;
                        }
                    }
                }

                Console.Out.WriteLine("Updating " + archiveName + "...");
                wiki.Save(archiveName,
                    textBuilder.ToString(),
                    "обновление");
                using (StreamWriter sw =
                        new StreamWriter(fileName))
                {
                    sw.Write(textBuilder.ToString());
                }
            }
        }

        internal void UpdatePages(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения восстановления страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            XmlDocument doc = wiki.Enumerate(parameters, true);

            string queryTimestamp = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

            XmlNodeList pages = doc.SelectNodes("//page");
            foreach (XmlNode page in pages)
            {
                string starttimestamp = queryTimestamp;
                int results = 0;
                string prefix = "Википедия:К восстановлению/";
                string pageName = page.Attributes["title"].Value;

                string basetimestamp = page.FirstChild.FirstChild.Attributes["timestamp"].Value;
                string editToken = page.Attributes["edittoken"].Value;

                string date = pageName.Substring(prefix.Length);
                Day day = new Day();
                if (!DateTime.TryParse(date, CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day.Date))
                {
                    continue;
                }

                string text = "";
                string fileName = _cacheDir + date + ".bin";
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
                    try
                    {
                        Console.Out.WriteLine("Downloading " + pageName + "...");
                        text = wiki.LoadText(pageName);
                        starttimestamp = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                    }
                    catch (WikiPageNotFound)
                    {
                        continue;
                    }
                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(page.Attributes["lastrevid"].Value);
                        sw.Write(text);
                    }
                }

                Dictionary<string, List<WikiPageSection>> titles = new Dictionary<string, List<WikiPageSection>>();
                day.Page = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    RemoveStrikeOut(section);
                    StrikeOutSection(section);
                    if (section.Subsections.Count(s => _results.Any(r => r.ToLower() == s.Title.Trim().ToLower())) == 0)
                    {
                        Match m = _wikiLinkRE.Match(section.Title);
                        if (m.Success)
                        {
                            string title = m.Groups[1].Value.Trim();

                            if (titles.ContainsKey(title))
                            {
                                titles[title].Add(section);
                            }
                            else
                            {
                                titles.Add(title, new List<WikiPageSection>());
                                titles[title].Add(section);
                            }
                        }
                    }
                    {
                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList);
                        foreach (WikiPageSection subsection in sections)
                        {
                            Match m = _wikiLinkRE.Match(subsection.Title);
                            if (m.Success &&
                                !subsection.Title.Contains("<s>") &&
                                subsection.Subsections.Count(s => _results.Any(r => r.ToLower() == s.Title.Trim().ToLower())) == 0)
                            {
                                string title = m.Groups[1].Value.Trim();

                                if (titles.ContainsKey(title))
                                {
                                    titles[title].Add(subsection);
                                }
                                else
                                {
                                    titles.Add(title, new List<WikiPageSection>());
                                    titles[title].Add(subsection);
                                }
                            }
                        }
                    }
                }

                parameters.Clear();
                parameters.Add("prop", "info");
                Dictionary<string, string> normalizedTitles = new Dictionary<string, string>();
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles.Keys);
                foreach (XmlNode node in xml.SelectNodes("//n"))
                {
                    normalizedTitles.Add(node.Attributes["to"].Value,
                        node.Attributes["from"].Value);
                }
                List<string> notificationList = new List<string>();
                XmlNodeList missingTitles = xml.SelectNodes("//page");
                foreach (XmlNode node in missingTitles)
                {
                    string title = node.Attributes["title"].Value;

                    IEnumerable<WikiPageSection> sections;
                    if (titles.ContainsKey(title))
                    {
                        sections = titles[title];
                    }
                    else
                    {
                        sections = titles[normalizedTitles[title]];
                    }
                    if (node.Attributes["missing"] == null)
                    {
                        DateTime start = day.Date;
                        parameters.Clear();
                        parameters.Add("list", "logevents");
                        parameters.Add("letype", "delete");
                        parameters.Add("lemlimit", "max");
                        parameters.Add("lestart", start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
                        parameters.Add("ledir", "newer");
                        parameters.Add("letitle", title);
                        parameters.Add("leprop", "comment|type|user|timestamp");
                        XmlDocument log = wiki.Enumerate(parameters, true);
                        XmlNodeList items = log.SelectNodes("//item");
                        List<DeleteLogEvent> events = new List<DeleteLogEvent>();
                        foreach (XmlNode item in items)
                        {
                            DeleteLogEvent ev = new DeleteLogEvent();
                            ev.Comment = item.Attributes["comment"].Value;
                            ev.Restored = item.Attributes["action"].Value == "restore";
                            ev.Deleted = item.Attributes["action"].Value == "delete";
                            ev.User = item.Attributes["user"].Value;
                            ev.Timestamp = DateTime.Parse(item.Attributes["timestamp"].Value,
                                null,
                                DateTimeStyles.AssumeUniversal);
                            events.Add(ev);
                        }
                        events.Sort(CompareDeleteLogEvents);
                        if (events.Count > 0 &&
                            events[0].Restored &&
                            (DateTime.Now - events[0].Timestamp).TotalHours > 2)
                        {
                            Regex commentRE = new Regex(@" восстановлено: (.+)");
                            Match m = commentRE.Match(events[0].Comment);
                            string comment;
                            if (m.Success)
                            {
                                comment = m.Groups[1].Value;
                            }
                            else
                            {
                                comment = "<nowiki>" + events[0].Comment + "</nowiki>";
                            }
                            string message = string.Format("Страница была восстановлена {1} администратором [[User:{0}|]]. Была указана следующая причина: «{2}». Данное сообщение было автоматически сгенерировано ботом ~~~~.\n",
                                events[0].User,
                                events[0].Timestamp.ToUniversalTime().ToString("d MMMM yyyy в HH:mm (UTC)"),
                                comment);
                            if (!titles.ContainsKey(title))
                            {
                                continue;
                            }
                            foreach (WikiPageSection section in titles[title])
                            {
                                WikiPageSection verdict = new WikiPageSection(" Автоматический итог ",
                                    section.Level + 1,
                                    message);
                                section.AddSubsection(verdict);
                                StrikeOutSection(section);
                                ++results;
                            }
                        }
                        else if (events.Count > 0 &&
                            events[0].Deleted)
                        {
                            parameters.Clear();
                            parameters.Add("prop", "revisions");
                            parameters.Add("rvprop", "timestamp|user");
                            parameters.Add("rvdir", "newer");
                            parameters.Add("rvlimit", "1");
                            parameters.Add("redirects");

                            log = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                            XmlNode revision = log.SelectSingleNode("//rev");
                            if (revision != null)
                            {
                                string user = revision.Attributes["user"].Value;
                                string timestamp = revision.Attributes["timestamp"].Value;
                                DateTime time = DateTime.Parse(timestamp,
                                    null,
                                    DateTimeStyles.AssumeUniversal);

                                string message = string.Format("Страница была создана заново {1} участником [[User:{0}|]]. Данное сообщение было автоматически сгенерировано ботом ~~~~.\n",
                                    user,
                                    time.ToUniversalTime().ToString("d MMMM yyyy в HH:mm (UTC)"));
                                
                                if (!titles.ContainsKey(title))
                                {
                                    continue;
                                }
                                foreach (WikiPageSection section in titles[title])
                                {
                                    WikiPageSection verdict = new WikiPageSection(" Автоматический итог ",
                                        section.Level + 1,
                                        message);
                                    section.AddSubsection(verdict);
                                    StrikeOutSection(section);
                                    ++results;
                                }
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
                    string revid = wiki.Save(pageName,
                        "",
                        newText,
                        "зачёркивание заголовков" + (results > 0 ? " и подведение итогов" : ""),
                        MinorFlags.Minor,
                        CreateFlags.NoCreate,
                        WatchFlags.None,
                        SaveFlags.Replace,
                        true,
                        basetimestamp,
                        "",
                        editToken);

                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
                    {
                        sw.WriteLine(revid);
                        sw.Write(newText);
                    }
                }
                catch (WikiException)
                {
                }
            }
        }

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        private void StrikeOutSection(WikiPageSection section)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            if (section.Subsections.Count(s => _results.Any(r => r.ToLower() == s.Title.Trim().ToLower())) > 0)
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
            if (section.Subsections.Count(s => _results.Any(r => r.ToLower() == s.Title.Trim().ToLower())) == 0)
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

        private static string LoadPageFromCache(string fileName,
            string revisionId,
            string pageName)
        {
            if (File.Exists(fileName))
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                using (TextReader sr = new StreamReader(gs))
                {
                    string revid = sr.ReadLine();
                    if (revid == revisionId)
                    {
                        Console.Out.WriteLine("Loading " + pageName + "...");
                        return sr.ReadToEnd();
                    }
                }
            }
            return null;
        }

        private static void CachePage(string fileName, string revisionId, string text)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
            using (StreamWriter sw = new StreamWriter(gs))
            {
                sw.WriteLine(revisionId);
                sw.Write(text);
            }
        }

        static int CompareDeleteLogEvents(DeleteLogEvent x, DeleteLogEvent y)
        {
            return y.Timestamp.CompareTo(x.Timestamp);
        }

        internal void AddNavigationTemplate(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Template:ВПВУС-Навигация");
            parameters.Add("eilimit", "max");
            parameters.Add("einamespace", "4");
            parameters.Add("eifilterredir", "nonredirects");

            XmlDocument doc = wiki.Enumerate(parameters, true);

            List<string> titles = new List<string>();
            DateTime end = DateTime.Today;
            DateTime start = end.AddDays(-7);
            while (start <= end)
            {
                string pageDate = start.ToString("d MMMM yyyy",
                        CultureInfo.CreateSpecificCulture("ru-RU"));
                string prefix = "Википедия:К восстановлению/";
                string pageName = prefix + pageDate;
                if (doc.SelectSingleNode("//ei[@title='" + pageName + "']") == null)
                {
                    titles.Add(pageName);
                }
                start = start.AddDays(1);
            }

            parameters.Clear();
            parameters.Add("prop", "info");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles);
            foreach (XmlNode node in xml.SelectNodes("//page"))
            {
                if (node.Attributes["missing"] == null)
                {
                    Console.Out.WriteLine("Updating " + node.Attributes["title"].Value + "...");
                    wiki.Prepend(node.Attributes["title"].Value,
                        "{{ВПВУС-Навигация}}\n",
                        "добавление навигационного шаблона");
                }
            }
        }

        #region IModule Members

        public void Run(Wiki wiki)
        {
            AddNavigationTemplate(wiki);
            UpdatePages(wiki);
            Analyze(wiki);
            UpdateMainPage(wiki);
            UpdateArchivePages(wiki);
        }

        #endregion
    }
}
