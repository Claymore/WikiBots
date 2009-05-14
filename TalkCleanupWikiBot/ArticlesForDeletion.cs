using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using System.IO.Compression;
using System.Text;

namespace Claymore.TalkCleanupWikiBot
{
    internal class ArticlesForDeletion
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
                string date = pageName.Substring(prefix.Length);
                Day day = new Day();
                try
                {
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture(_l10i.Culture),
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
                bool archived = doc.SelectSingleNode("//page[@title=\""+ pageName + "\"]") == null;
                XmlNode page = xml.SelectSingleNode("//page[@title=\"" + pageName + "\"]");
                
                Day day = new Day();
                day.Archived = archived;
                try
                {
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture(_l10i.Culture),
                        DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
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
                    text = wiki.LoadPage(pageName);
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
                wiki.SavePage(_l10i.MainPage, text, _l10i.MainPageUpdateComment);
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
                    wiki.SavePage(_l10i.ArchivePage + archiveDate,
                        "",
                        text,
                        _l10i.MainPageUpdateComment,
                        MinorFlags.Minor,
                        CreateFlags.None,
                        WatchFlags.None,
                        SaveFlags.Replace);
                }
                start.AddMonths(1);
            }
        }

        public void UpdatePages(Wiki wiki)
        {
            Console.Out.WriteLine("Updating articles for deletion...");
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Regex re = new Regex(@"<s>\[{2}(.+?)(\|.+?)?]{2}</s>");
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
                
                string date = pageName.Substring(prefix.Length);
                Day day = new Day();
                try
                {
                    day.Date = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture(_l10i.Culture),
                        DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
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
                        text = wiki.LoadPage(pageName);
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
                    if (section.Subsections.Count(s => _l10i.Processor != null
                        ? _l10i.Processor(s).Trim() == _l10i.Result
                        : s.Title.Trim() == _l10i.Result) == 0)
                    {
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
                
                parameters.Clear();
                parameters.Add("prop", "info");
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles.Keys);
                XmlNodeList missingTitles = xml.SelectNodes("//page[@missing]");
                foreach (XmlNode node in missingTitles)
                {
                    string title = node.Attributes["title"].Value;
                    DateTime start = day.Date;
                    foreach (WikiPageSection section in titles[title])
                    {
                        Match m = timeRE.Match(section.Text);
                        if (m.Success)
                        {
                            start = DateTime.Parse(m.Groups[1].Value,
                                CultureInfo.CreateSpecificCulture(_l10i.Culture),
                                DateTimeStyles.AssumeUniversal);
                        }
                    }
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
                    if (events.Count > 0 && events[0].Deleted)
                    {
                        Regex commentRE = new Regex(@"(.+?):&#32;(.+)");
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
                        string message = string.Format(_l10i.AutoResultMessage,
                            events[0].User,
                            events[0].Timestamp.ToUniversalTime().ToString(_l10i.DateFormat),
                            comment);
                        foreach (WikiPageSection section in titles[title])
                        {
                            WikiPageSection verdict = new WikiPageSection(" " + _l10i.Result + " ",
                                section.Level + 1,
                                message);
                            section.AddSubsection(verdict);
                            StrikeOutSection(section);
                            ++results;
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
                        "",
                        newText,
                        _l10i.StrikeOutComment + (results > 0 ? _l10i.AutoResultComment : ""),
                        MinorFlags.Minor,
                        CreateFlags.NoCreate,
                        WatchFlags.None,
                        SaveFlags.Replace,
                        basetimestamp,
                        starttimestamp,
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

            if (section.Subsections.Count(s => _l10i.Processor != null
                        ? _l10i.Processor(s).Trim() == _l10i.Result
                        : s.Title.Trim() == _l10i.Result) > 0)
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
                        ? _l10i.Processor(s).Trim() == _l10i.Result
                        : s.Title.Trim() == _l10i.Result) == 0)
            {
                if (section.Title.Contains("<s>"))
                {
                    section.Title = section.Title.Replace("<s>", "");
                    section.Title = section.Title.Replace("</s>", "");
                }
            }
            section.ForEach(RemoveStrikeOut);
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
    }
}
