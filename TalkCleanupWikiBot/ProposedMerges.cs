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
    internal class ProposedMerges
    {
        private string _cacheDir;
        private string _language;

        public ProposedMerges()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\ProposedMerges\\";
        }

        public void Analyze(Wiki wiki)
        {
            Directory.CreateDirectory(_cacheDir);
            
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
            Regex closedRE = new Regex(@"({{ВПКОБ-Навигация}}\s*{{(Закрыто|Closed|закрыто|closed)}})|({{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПКОБ-Навигация}})");

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
                    Console.Out.WriteLine("Closing " + pageName + "...");
                    text = text.Replace("{{ВПКОБ-Навигация}}", "{{ВПКОБ-Навигация|nocat=1}}");
                    wiki.SavePage(pageName,
                        text,
                        "обсуждение закрыто, убираем страницу из категории");
                    continue;
                }
                day.Page = WikiPage.Parse(pageName, text);
                days.Add(day);
            }

            days.Sort(CompareDays);

            using (StreamWriter sw =
                        new StreamWriter(_cacheDir + "MainPage.txt"))
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

                sw.WriteLine("{{/Подвал}}");
            }

            days.Clear();

            /*DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            start = currentMonth.AddMonths(-1);
            while (start < currentMonth)
            {
                string date = start.ToString("d MMMM yyyy");
                string pageName = "Википедия:К объединению/" + date;
                bool archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;

                Day day = new Day();
                day.Exists = true;
                day.Archived = archived;
                day.Date = start;
                Console.Out.WriteLine("Processing " + pageName + "...");
                string fileName = _cacheDir + date + ".bin";
                string text = "";
                if (archived && File.Exists(fileName))
                {
                    using (FileStream fs = new FileStream(fileName, FileMode.Open))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                    using (TextReader sr = new StreamReader(gs))
                    {
                        sr.ReadLine();
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
            }*/
        }

        public void UpdateArchivePages(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения объединения страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            DateTime minDate = DateTime.Now;
            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К объединению/".Length);
                try
                {
                    DateTime day = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal);
                    if (day < minDate)
                    {
                        minDate = day;
                    }
                }
                catch (FormatException)
                {
                    continue;
                }
            }

            List<string> titles = new List<string>();
            minDate = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime currentMonth = new DateTime(DateTime.Today.Year,
                DateTime.Today.Month, 1);
            DateTime start = minDate;
            while (start < currentMonth)
            {
                string date = start.ToString("yyyy-MM");
                string pageName = "Википедия:Архив запросов на объединение/" + date;
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
                string date = archiveName.Substring("Википедия:Архив запросов на объединение/".Length);
                DateTime archiveDate;
                try
                {
                    archiveDate = DateTime.Parse(date,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal);
                }
                catch (FormatException)
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
                    string prefix = "Википедия:К объединению/";
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
                    string dateString = pageName.Substring("Википедия:К объединению/".Length);

                    string pageFileName = _cacheDir + dateString + ".bin";
                    Day day = new Day();
                    day.Archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;

                    try
                    {
                        day.Date = DateTime.Parse(dateString,
                            CultureInfo.CreateSpecificCulture("ru-RU"),
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
                    if (File.Exists(pageFileName))
                    {
                        using (FileStream fs = new FileStream(pageFileName, FileMode.Open))
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
                        using (FileStream fs = new FileStream(pageFileName, FileMode.Create))
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

                StringBuilder textBuilder = new StringBuilder();

                textBuilder.AppendLine("{| class=standard");
                textBuilder.AppendLine("|-");
                textBuilder.AppendLine("!| Дата");
                textBuilder.AppendLine("!| Статьи, вынесенные на объединение");
                textBuilder.AppendLine("|-\n");

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

        public void UpdateMainPage(Wiki wiki)
        {
            Console.Out.WriteLine("Updating proposed merges...");
            using (TextReader sr =
                        new StreamReader(_cacheDir + "MainPage.txt"))
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
        }

        public void UpdatePages(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Википедия:Незакрытые обсуждения объединения страниц");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "4");
            parameters.Add("prop", "info");
            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("//page");

            foreach (XmlNode page in pages)
            {
                string pageName = page.Attributes["title"].Value;
                string date = pageName.Substring("Википедия:К объединению/".Length);
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
                    string revid = wiki.SavePage(pageName,
                        newText,
                        "зачёркивание заголовков");
                    
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
