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
    internal class DeletionReview
    {
        private readonly string _cacheDir;
        private string _language;

        public DeletionReview()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\DeletionReview\\";
            Directory.CreateDirectory(_cacheDir);
        }

        public void Analyze(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения восстановления страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

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
                if (m.Success || day.Date < cutOffDate)
                {
                    Console.Out.WriteLine("Closing " + pageName + "...");
                    text = text.Replace("{{ВПВУС-Навигация}}", "{{ВПВУС-Навигация|nocat=1}}");
                    wiki.SavePage(pageName,
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
                    bool hasVerdict = section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0;
                    bool hasAutoVerdict = section.Subsections.Count(s => s.Title.Trim() == "Автоматический итог") > 0;
                    if (hasVerdict || hasAutoVerdict || section.Title.Contains("<s>"))
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
                        hasVerdict = subsection.Subsections.Count(s => s.Title.Trim() == "Итог") > 0;
                        hasAutoVerdict = subsection.Subsections.Count(s => s.Title.Trim() == "Автоматический итог") > 0;
                        if (hasVerdict || hasAutoVerdict || subsection.Title.Contains("<s>"))
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

                        bool hasVerdict = section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0;
                        bool hasAutoVerdict = section.Subsections.Count(s => s.Title.Trim() == "Автоматический итог") > 0;
                        if (hasVerdict || hasAutoVerdict || section.Title.Contains("<s>"))
                        {
                            Match m = wikiLinkRE.Match(section.Title);
                            if (m.Success)
                            {
                                string link = m.Groups[1].Value;
                                XmlNode node = xml.SelectSingleNode("//page[@title='" + link + "']");
                                if (node != null)
                                {
                                    if (node.Attributes["missing"] == null)
                                    {
                                        result = " ''(восстановлено)''";
                                    }
                                    else
                                    {
                                        result = " ''(не восстановлено)''";
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
                            hasVerdict = subsection.Subsections.Count(s => s.Title.Trim() == "Итог") > 0;
                            hasAutoVerdict = subsection.Subsections.Count(s => s.Title.Trim() == "Автоматический итог") > 0;
                            if (hasVerdict || hasAutoVerdict || subsection.Title.Contains("<s>"))
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
                                            result = " ''(восстановлено)''";
                                        }
                                        else
                                        {
                                            result = " ''(не восстановлено)''";
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
                wiki.SavePage("Википедия:К восстановлению",
                    "1",
                    text,
                    "обновление",
                    MinorFlags.Minor,
                    CreateFlags.NoCreate,
                    WatchFlags.Watch,
                    SaveFlags.Replace);
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
                        text = wiki.LoadPage(pageName);
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
                wiki.SavePage(archiveName,
                    "",
                    textBuilder.ToString(),
                    "обновление",
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Replace);
                using (StreamWriter sw =
                        new StreamWriter(fileName))
                {
                    sw.Write(textBuilder.ToString());
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
    }
}
