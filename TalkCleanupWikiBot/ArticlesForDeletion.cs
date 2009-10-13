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
    internal class ArticlesForDeletion : IModule
    {
        private ArticlesForDeletionLocalization _l10i;
        private string _cacheDir;

        public ArticlesForDeletion(ArticlesForDeletionLocalization l10i)
        {
            _l10i = l10i;
            _cacheDir = "Cache\\" + _l10i.Language + "\\ArticlesForDeletion\\";
        }

        public void Analyse(Wiki wiki)
        {
            Directory.CreateDirectory(_cacheDir);

            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", _l10i.Category);
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            List<Day> days = new List<Day>();
            foreach (XmlNode page in pages)
            {
                string prefix = _l10i.MainPage + "/";
                string pageName = page.Attributes["title"].Value;
                if (pageName.Length < prefix.Length)
                {
                    continue;
                }
                string date = pageName.Substring(prefix.Length);
                Day day = new Day();
                if (!DateTime.TryParse(date, CultureInfo.CreateSpecificCulture(_l10i.Culture),
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
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);
            using (StreamWriter sw =
                        new StreamWriter(_cacheDir + "Main.txt"))
            {
                sw.WriteLine("{{" + _l10i.TopTemplate + "}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{" + _l10i.Template + "|" + day.Date.ToString("yyyy-M-d") + "|");
                    List<string> titles = new List<string>();

                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        ReplaceEmptyResults(section);
                        RemoveStrikeOut(section);
                        StrikeOutSection(section);
                        string result = section.Reduce("", SubsectionsList);
                        if (result.Length > 0)
                        {
                            result = " • <small>" + result.Substring(3) + "</small>";
                        }
                        string title;
                        if (_l10i.Processor != null)
                        {
                            title = _l10i.Processor(section).Trim();
                        }
                        else
                        {
                            title = section.Title.Trim();
                        }
                        titles.Add(title + result);
                    }
                    sw.Write(string.Join(" • ", titles.ConvertAll(c => c).ToArray()));
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("{{" + _l10i.BottomTemplate + "}}");
            }

            List<string> dates = new List<string>();
            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            for (int i = 0; i < 2; ++i)
            {
                DateTime start = currentMonth.AddMonths(-i);
                DateTime end = start.AddMonths(1);
                while (start < end)
                {
                    string date = start.ToString("d MMMM yyyy",
                        CultureInfo.CreateSpecificCulture(_l10i.Culture));
                    string prefix = _l10i.MainPage + "/";
                    string pageName = prefix + date;
                    dates.Add(pageName);
                    start = start.AddDays(1);
                }
            }

            parameters.Clear();
            parameters.Add("prop", "info");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, dates);
            days.Clear();
            foreach (string pageName in dates)
            {
                string prefix = _l10i.MainPage + "/";
                string date = pageName.Substring(prefix.Length);
                string fileName = _cacheDir + date + ".bin";
                bool archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;
                XmlNode page = xml.SelectSingleNode("//page[@title=\"" + pageName + "\"]");

                Day day = new Day();
                day.Archived = archived;
                if (!DateTime.TryParse(date, CultureInfo.CreateSpecificCulture(_l10i.Culture),
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
                day.Exists = true;
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);

            for (int i = 0; i < 2; ++i)
            {
                DateTime start = currentMonth.AddMonths(-i);
                DateTime end = start.AddMonths(1);
                string archiveDate = start.ToString("yyyy-MM");

                IEnumerable<Day> daysInMonth = days.Where(d => d.Date >= start &&
                    d.Date < end);

                using (StreamWriter sw =
                            new StreamWriter(_cacheDir + "Archive-" + archiveDate + ".txt"))
                {
                    sw.WriteLine("{| class=standard");
                    sw.WriteLine("|-");
                    sw.WriteLine("!| Дата");
                    sw.WriteLine("!| " + _l10i.ArchiveTemplate);
                    sw.WriteLine("|-\n");

                    StringBuilder sb = new StringBuilder();
                    foreach (Day day in daysInMonth)
                    {
                        sb.Append("{{" + _l10i.Template + "|" + day.Date.ToString("yyyy-M-d") + "|");
                        if (!day.Exists)
                        {
                            sb.Append("''" + _l10i.EmptyArchive + "''}}\n\n");
                            continue;
                        }
                        List<string> titles = new List<string>();
                        foreach (WikiPageSection section in day.Page.Sections)
                        {
                            string result = section.Reduce("", SubsectionsList);
                            if (result.Length > 0)
                            {
                                result = " • <small>" + result.Substring(3) + "</small>";
                            }
                            string title;
                            if (_l10i.Processor != null)
                            {
                                title = _l10i.Processor(section).Trim();
                            }
                            else
                            {
                                title = section.Title.Trim();
                            }
                            titles.Add(title + result);
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
        }

        public void UpdateMainPage(Wiki wiki)
        {
            Console.Out.WriteLine("Updating articles for deletion...");
            using (TextReader sr =
                        new StreamReader(_cacheDir + "Main.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.Save(_l10i.MainPage, text, _l10i.MainPageUpdateComment);
            }
        }

        public void UpdateArchive(Wiki wiki)
        {
            Console.Out.WriteLine("Updating archive of articles for deletion...");
            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);

            for (int i = 0; i < 2; ++i)
            {
                DateTime start = currentMonth.AddMonths(-i);
                string archiveDate = start.ToString("yyyy-MM");
                using (TextReader sr =
                            new StreamReader(_cacheDir + "Archive-" + archiveDate + ".txt"))
                {
                    string text = sr.ReadToEnd();
                    wiki.Save(_l10i.ArchivePage + archiveDate,
                        text,
                        _l10i.MainPageUpdateComment);
                }
                start.AddMonths(1);
            }
        }

        public void UpdatePages(Wiki wiki)
        {
            Console.Out.WriteLine("Updating articles for deletion...");
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", _l10i.Category);
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
                string prefix = _l10i.MainPage + "/";
                string pageName = page.Attributes["title"].Value;

                string basetimestamp = page.FirstChild.FirstChild.Attributes["timestamp"].Value;
                string editToken = page.Attributes["edittoken"].Value;

                if (pageName.Length < prefix.Length)
                {
                    continue;
                }
                string date = pageName.Substring(prefix.Length);
                Day day = new Day();
                if (!DateTime.TryParse(date, CultureInfo.CreateSpecificCulture(_l10i.Culture),
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

                List<string> titlesWithResults = new List<string>();
                Dictionary<string, List<WikiPageSection>> titles = new Dictionary<string, List<WikiPageSection>>();
                day.Page = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in day.Page.Sections)
                {
                    ReplaceEmptyResults(section);
                    RemoveStrikeOut(section);
                    StrikeOutSection(section);
                    if (section.Subsections.Count(s => _l10i.Processor != null
                        ? _l10i.Results.Any(r => r == _l10i.Processor(s).Trim())
                        : _l10i.Results.Any(r => r == s.Title.Trim())) == 0)
                    {
                        Match m = wikiLinkRE.Match(section.Title);
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
                        Match m = wikiLinkRE.Match(section.Title);
                        if (m.Success && section.Title.Contains("<s>"))
                        {
                            titlesWithResults.Add(m.Groups[1].Value.Trim());
                        }
                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList);
                        foreach (WikiPageSection subsection in sections)
                        {
                            m = wikiLinkRE.Match(subsection.Title);
                            if (m.Success && subsection.Title.Contains("<s>"))
                            {
                                titlesWithResults.Add(m.Groups[1].Value.Trim());
                            }
                            if (m.Success &&
                                !subsection.Title.Contains("<s>") &&
                                subsection.Subsections.Count(s => _l10i.Processor != null
                                    ? _l10i.Results.Any(r => r == _l10i.Processor(s).Trim())
                                    : _l10i.Results.Any(r => r == s.Title.Trim())) == 0)
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
                    if (node.Attributes["missing"] != null)
                    {
                        DateTime start = day.Date;
                        //foreach (WikiPageSection section in sections)
                        //{
                        //    Match m = timeRE.Match(section.Text);
                        //    if (m.Success)
                        //    {
                        //        start = DateTime.Parse(m.Groups[1].Value,
                        //            CultureInfo.CreateSpecificCulture(_l10i.Culture),
                        //            DateTimeStyles.AssumeUniversal);
                        //    }
                        //}
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
                            ev.Deleted = item.Attributes["action"].Value == "delete";
                            ev.User = item.Attributes["user"].Value;
                            ev.Timestamp = DateTime.Parse(item.Attributes["timestamp"].Value,
                                null,
                                DateTimeStyles.AssumeUniversal);
                            events.Add(ev);
                        }
                        events.Sort(CompareDeleteLogEvents);
                        if (events.Count > 0 &&
                            events[0].Deleted &&
                            (DateTime.Now - events[0].Timestamp).TotalHours > 2)
                        {
                            string comment = FilterWikiMarkup(events[0].Comment);
                            string message = string.Format(_l10i.AutoResultMessage,
                                events[0].User,
                                events[0].Timestamp.ToUniversalTime().ToString(_l10i.DateFormat),
                                comment);
                            if (!titles.ContainsKey(title))
                            {
                                continue;
                            }
                            foreach (WikiPageSection section in titles[title])
                            {
                                WikiPageSection verdict = new WikiPageSection(" " + _l10i.AutoResultSection + " ",
                                    section.Level + 1,
                                    message);
                                section.AddSubsection(verdict);
                                StrikeOutSection(section);
                                ++results;
                            }
                        }
                    }
                }

                if (_l10i.Culture != "ru-RU")
                {
                    parameters.Clear();
                    parameters.Add("prop", "info");
                    xml = wiki.Query(QueryBy.Titles, parameters, titlesWithResults);
                    foreach (XmlNode node in xml.SelectNodes("//page"))
                    {
                        if (node.Attributes["missing"] == null &&
                            node.Attributes["redirect"] == null &&
                            node.Attributes["ns"].Value == "0")
                        {
                            notificationList.Add(node.Attributes["title"].Value);
                        }
                    }
                    if (notificationList.Count > 0)
                    {
                        parameters.Clear();
                        parameters.Add("list", "backlinks");
                        parameters.Add("bltitle", pageName);
                        parameters.Add("blfilterredir", "nonredirects");
                        parameters.Add("blnamespace", "1");
                        parameters.Add("bllimit", "max");

                        XmlDocument backlinks = wiki.Enumerate(parameters, true);
                        foreach (string title in notificationList)
                        {
                            string talkPage = wiki.GetNamespace(1) + ":" + title;
                            if (backlinks.SelectSingleNode("//bl[@title='" + talkPage + "']") == null)
                            {
                                PutNotification(wiki, title, date);
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
                        _l10i.StrikeOutComment + (results > 0 ? _l10i.AutoResultComment : ""),
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

        private void PutNotification(Wiki wiki, string title, string date)
        {
            string talkPage = wiki.GetNamespace(1) + ":" + title;
            Console.Out.WriteLine("Updating " + talkPage + "...");
            try
            {
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("rvprop", "content");
                parameters.Add("rvsection", "0)");
                parameters.Add("prop", "revisions");
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { talkPage });
                string content;
                XmlNode node = xml.SelectSingleNode("//rev");
                if (node != null && node.FirstChild != null)
                {
                    content = node.FirstChild.Value;
                }
                else
                {
                    content = "";
                }
                int index = content.IndexOf("{{" + _l10i.NotificationTemplate + "|", StringComparison.CurrentCultureIgnoreCase);
                if (index != -1)
                {
                    int endIndex = content.IndexOf("}}", index);
                    if (endIndex != -1)
                    {
                        content = content.Insert(endIndex, "|" + date);
                    }
                }
                else
                {
                    index = content.IndexOf("{{talkheader", StringComparison.CurrentCultureIgnoreCase);
                    if (index != -1)
                    {
                        int endIndex = content.IndexOf("}}", index);
                        if (endIndex != -1)
                        {
                            content = content.Insert(endIndex + 2, "\n{{" + _l10i.NotificationTemplate + "|" + date + "}}\n");
                        }
                    }
                    else
                    {
                        index = content.IndexOf("{{заголовок обсуждения", StringComparison.CurrentCultureIgnoreCase);
                        if (index != -1)
                        {
                            int endIndex = content.IndexOf("}}", index);
                            if (endIndex != -1)
                            {
                                content = content.Insert(endIndex + 2, "\n{{" + _l10i.NotificationTemplate + "|" + date + "}}\n");
                            }
                        }
                        else
                        {
                            content = content.Insert(0, "\n{{" + _l10i.NotificationTemplate + "|" + date + "}}\n");
                        }
                    }
                }
                wiki.SaveSection(talkPage,
                    "0",
                    content,
                    _l10i.MainPageUpdateComment);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine("Failed to update " + talkPage + ":" + e.Message);
            }
        }

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        private void StrikeOutSection(WikiPageSection section)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            if (section.Subsections.Count(s => _l10i.Processor != null
                        ? _l10i.Results.Any(r => r == _l10i.Processor(s).Trim())
                        : _l10i.Results.Any(r => r == s.Title.Trim())) > 0)
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
            if (section.Subsections.Count(s => _l10i.Processor != null
                        ? _l10i.Results.Any(r => r == _l10i.Processor(s).Trim())
                        : _l10i.Results.Any(r => r == s.Title.Trim())) == 0)
            {
                if (section.Title.Contains("<s>"))
                {
                    section.Title = section.Title.Replace("<s>", "");
                    section.Title = section.Title.Replace("</s>", "");
                }
            }
            section.ForEach(RemoveStrikeOut);
        }

        private void ReplaceEmptyResults(WikiPageSection section)
        {
            WikiPageSection result = section.Subsections.FirstOrDefault(s => _l10i.Processor != null
                        ? _l10i.Results.Any(r => r == _l10i.Processor(s).Trim())
                        : _l10i.Results.Any(r => r == s.Title.Trim()));
            if (result != null && result.Subsections.Count == 0 &&
                string.IsNullOrEmpty(result.SectionText.Trim()))
            {
                result.Title = _l10i.EmptyResult;
            }
            section.ForEach(ReplaceEmptyResults);
        }

        private string SubsectionsList(WikiPageSection section,
            string aggregator)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Match m = wikiLinkRE.Match(section.Title);
            if (m.Success)
            {
                if (_l10i.Processor != null)
                {
                    aggregator = aggregator + " • " + _l10i.Processor(section).Trim();
                }
                else
                {
                    aggregator = aggregator + " • " + section.Title.Trim();
                }
            }
            aggregator = section.Reduce(aggregator, SubsectionsList);
            return aggregator;
        }

        public delegate string TitleProcessor(WikiPageSection section);

        static int CompareDeleteLogEvents(DeleteLogEvent x, DeleteLogEvent y)
        {
            return y.Timestamp.CompareTo(x.Timestamp);
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

        private static string FilterWikiMarkup(string line)
        {
            Regex commentRE = new Regex(@"\[{2}(File|Файл|Image|Изображение|Category|Категория):(.+?)(\|.+)?(\]{2})?$");
            string comment = line;
            comment = comment.Replace("{{", "<nowiki>{{").Replace("}}", "}}</nowiki>").Replace("'''", "").Replace("''", "").Trim();
            comment = commentRE.Replace(comment, "<nowiki>[[</nowiki>[[:$1:$2]]<nowiki>$3]]</nowiki>");
            if (comment.Contains("<nowiki>"))
            {
                for (int index = comment.IndexOf("<nowiki>");
                     index != -1;
                     index = comment.IndexOf("<nowiki>", index + 1))
                {
                    int endIndex = comment.IndexOf("</nowiki>", index);
                    if (endIndex == -1)
                    {
                        comment += "</nowiki>";
                        break;
                    }
                }
            }
            return comment;
        }

        #region IModule Members

        public void Run(Wiki wiki)
        {
            UpdatePages(wiki);
            Analyse(wiki);
            UpdateMainPage(wiki);
            UpdateArchive(wiki);
        }

        #endregion
    }
}
