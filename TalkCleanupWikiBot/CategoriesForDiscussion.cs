using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using System.Text;

namespace Claymore.TalkCleanupWikiBot
{
    internal class CategoriesForDiscussion
    {
        private readonly string _cacheDir;
        private readonly string _language;
        private static readonly Regex _closedRE;
        private static readonly Regex _wikiLinkRE;
        private static readonly Regex _clRE;

        static CategoriesForDiscussion()
        {
            _closedRE = new Regex(@"({{ВПОК-Навигация}}\s*{{(Закрыто|Closed|закрыто|closed)}})|({{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПОК-Навигация}})");
            _wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            _clRE = new Regex(@"\{{2}(cl|ОБК)\|(.+?)\}{2}");
        }

        public CategoriesForDiscussion()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\CategoriesForDiscussion\\";
            
            Directory.CreateDirectory(_cacheDir);
        }

        public void Analyze(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения категорий");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            List<Day> days = new List<Day>();
            DateTime start = DateTime.Today;

            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:Обсуждение категорий/".Length);
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
                string text = LoadPageFromCache(fileName,
                    page.Attributes["lastrevid"].Value, pageName);

                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);

                    CachePage(fileName, page.Attributes["lastrevid"].Value, text);
                }

                Match m = _closedRE.Match(text);
                if (m.Success)
                {
                    Console.Out.WriteLine("Closing " + pageName + "...");
                    text = text.Replace("{{ВПОК-Навигация}}", "{{ВПОК-Навигация|closed=1}}");
                    wiki.SavePage(pageName, text, "обсуждение закрыто");
                    continue;
                }

                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter(_cacheDir + "MainPage.txt"))
            {
                sw.WriteLine("{{Википедия:Обсуждение категорий/Обсуждаемые категории}}\n");

                foreach (Day day in days)
                {
                    sw.Write("{{Википедия:Обсуждение категорий/Месяц|" + day.Date.ToString("yyyy-M") + "|\n");

                    List<string> titles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        RemoveStrikeOut(section);
                        StrikeOutSection(section);

                        for (int i = 0; i < section.Level - 1; ++i)
                        {
                            filler += "*";
                        }
                        titles.Add(filler + " " + section.Title.Trim());

                        List<WikiPageSection> sections = new List<WikiPageSection>();
                        section.Reduce(sections, SubsectionsList);
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

                sw.WriteLine("|}");
            }
        }

        public void UpdateMainPage(Wiki wiki)
        {
            Console.Out.WriteLine("Updating categories for discussion...");
            using (TextReader sr =
                        new StreamReader(_cacheDir + "MainPage.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Википедия:Обсуждение категорий/Текущие обсуждения",
                    text,
                    "обновление");
            }
        }

        public void UpdateArchivePages(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения категорий");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            DateTime minDate = DateTime.Now;
            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:Обсуждение категорий/".Length);
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
                string date = start.ToString("MMMM yyyy");
                string pageName = "Википедия:Обсуждение категорий/Архив/" + date;
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
                string date = archiveName.Substring("Википедия:Обсуждение категорий/Архив/".Length);
                DateTime archiveDate;
                if (!DateTime.TryParse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out archiveDate))
                {
                    continue;
                }

                string fileName = _cacheDir + "Archive-" + date + ".txt";

                string pageTitle = "Википедия:Обсуждение категорий/" + date.Substring(0, 1).ToLower() + date.Substring(1);
                parameters.Clear();
                parameters.Add("prop", "info");

                List<Day> days = new List<Day>();
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageTitle });
                XmlNodeList archives = xml.SelectNodes("//page");
                foreach (XmlNode page in archives)
                {
                    string pageName = page.Attributes["title"].Value;
                    string dateString = pageName.Substring("Википедия:Обсуждение категорий/".Length);

                    string pageFileName = _cacheDir + dateString + ".bin";
                    Day day = new Day();

                    if (!DateTime.TryParse(dateString,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day.Date))
                    {
                        continue;
                    }

                    if (page.Attributes["missing"] != null)
                    {
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
                    day.Page = WikiPage.Parse(pageName, text);

                    StringBuilder textBuilder = new StringBuilder();
                    textBuilder.AppendLine("{{Википедия:Обсуждение категорий/Обсуждаемые категории}}\n");
                    textBuilder.Append("{{Википедия:Обсуждение категорий/Месяц|" + day.Date.ToString("yyyy-M") + "|\n");
                    List<string> sectionTitles = new List<string>();
                    foreach (WikiPageSection section in day.Page.Sections)
                    {
                        string filler = "";
                        RemoveStrikeOut(section);
                        StrikeOutSection(section);

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
                        sectionTitles[0] = "2=<li>" + sectionTitles[0].Substring(2) + "</li>";
                    }
                    textBuilder.Append(string.Join("\n", sectionTitles.ConvertAll(c => c).ToArray()));
                    textBuilder.Append("}}\n\n");
                    textBuilder.AppendLine("|}");

                    textBuilder.Replace("<s>", "");
                    textBuilder.Replace("</s>", "");
                    textBuilder.Replace("<strike>", "");
                    textBuilder.Replace("</strike>", "");

                    if (File.Exists(fileName))
                    {
                        using (TextReader sr = new StreamReader(fileName))
                        {
                            string txt = sr.ReadToEnd();
                            if (txt == textBuilder.ToString())
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

            titles.Clear();
            start = new DateTime(minDate.Year, 1, 1);
            while (start <= currentMonth)
            {
                string date = start.ToString("yyyy");
                string pageName = "Википедия:Обсуждение категорий/Архив/" + date;
                titles.Add(pageName);
                start = start.AddYears(1);
            }

            parameters.Clear();
            parameters.Add("prop", "info");

            archivesDoc = wiki.Query(QueryBy.Titles, parameters, titles);
            pages = archivesDoc.SelectNodes("//page");
            foreach (XmlNode archivePage in pages)
            {
                string archiveName = archivePage.Attributes["title"].Value;
                string date = archiveName.Substring("Википедия:Обсуждение категорий/Архив/".Length);
                int year;
                if (!int.TryParse(date, out year))
                {
                    continue;
                }
                DateTime archiveDate = new DateTime(year, 1, 1);
                string fileName = _cacheDir + "Archive-" + date + ".txt";

                start = archiveDate;
                DateTime end = start.AddYears(1);
                titles.Clear();
                while (start < end)
                {
                    string pageDate = start.ToString("MMMM yyyy",
                        CultureInfo.CreateSpecificCulture("ru-RU"));
                    string prefix = "Википедия:Обсуждение категорий/";
                    string pageName = prefix + pageDate.Substring(0, 1).ToLower() + pageDate.Substring(1);
                    titles.Add(pageName);

                    start = start.AddMonths(1);
                }

                parameters.Clear();
                parameters.Add("prop", "info");

                List<Day> days = new List<Day>();
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles);
                XmlNodeList archives = xml.SelectNodes("//page");
                foreach (XmlNode page in archives)
                {
                    string pageName = page.Attributes["title"].Value;
                    string dateString = pageName.Substring("Википедия:Обсуждение категорий/".Length);

                    string pageFileName = _cacheDir + dateString + ".bin";
                    Day day = new Day();

                    if (!DateTime.TryParse(dateString,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal, out day.Date))
                    {
                        continue;
                    }

                    if (page.Attributes["missing"] != null)
                    {
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
                    day.Page = WikiPage.Parse(pageName, text);
                    days.Add(day);
                }

                days.Sort(CompareDays);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{{Википедия:Обсуждение категорий/Обсуждаемые категории}}\n");

                foreach (Day day in days)
                {
                    sb.Append("{{Википедия:Обсуждение категорий/Месяц|" + day.Date.ToString("yyyy-M") + "|\n");

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
                        sectionTitles[0] = "2=<li>" + sectionTitles[0].Substring(2) + "</li>";
                    }
                    sb.Append(string.Join("\n", sectionTitles.ConvertAll(c => c).ToArray()));
                    sb.Append("}}\n\n");
                }

                sb.Append("|}");
                sb.Replace("<s>", "");
                sb.Replace("</s>", "");
                sb.Replace("<strike>", "");
                sb.Replace("</strike>", "");

                if (File.Exists(fileName))
                {
                    using (TextReader sr = new StreamReader(fileName))
                    {
                        string txt = sr.ReadToEnd();
                        if (txt == sb.ToString())
                        {
                            continue;
                        }
                    }
                }

                Console.Out.WriteLine("Updating " + archiveName + "...");
                wiki.SavePage(archiveName,
                    "",
                    sb.ToString(),
                    "обновление",
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Replace);
                using (StreamWriter sw =
                        new StreamWriter(fileName))
                {
                    sw.Write(sb.ToString());
                }
            }
        }

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        private void StrikeOutSection(WikiPageSection section)
        {
            if (section.Subsections.Count(s => s.Title.Trim() == "Итог") > 0)
            {
                if (!section.Title.Contains("<s>"))
                {
                    section.Title = string.Format(" <s>{0}</s> ",
                        section.Title.Trim());
                }

                foreach (WikiPageSection subsection in section.Subsections)
                {
                    Match m = _wikiLinkRE.Match(subsection.Title);
                    if (m.Success && !subsection.Title.Contains("<s>"))
                    {
                        subsection.Title = string.Format(" <s>{0}</s> ",
                            subsection.Title.Trim());
                    }
                    m = _clRE.Match(subsection.Title);
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
            if (section.Level < 4)
            {
                aggregator.Add(section);
            }
            else
            {
                Match m = _wikiLinkRE.Match(section.Title);
                if (m.Success)
                {
                    aggregator.Add(section);
                }
                else
                {
                    m = _clRE.Match(section.Title);
                    if (m.Success)
                    {
                        aggregator.Add(section);
                    }
                }
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
