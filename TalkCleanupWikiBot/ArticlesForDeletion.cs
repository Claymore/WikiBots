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

        public void AnalyseArchivedPages(Wiki wiki)
        {
            
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
                Console.Out.WriteLine("Downloading " + pageName + "...");
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
                            text = sr.ReadToEnd();
                        }
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    text = wiki.LoadPage(pageName);
                    using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                    using (StreamWriter sw = new StreamWriter(gs))
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
                        titles.Add(section.Title.Trim() + result);
                    }
                    sw.Write(string.Join(" • ", titles.ConvertAll(c => c).ToArray()));
                    sw.Write("}}\n\n");
                }

                sw.WriteLine("{{" + _l10i.BottomTemplate + "}}");
            }

            DateTime today = DateTime.Today;
            DateTime currentMonth = new DateTime(today.Year, today.Month, 1);
            for (int i = 0; i < 2; ++i)
            {
                days.Clear();
                DateTime start = currentMonth.AddMonths(-i);
                DateTime end = start.AddMonths(1);
                string archiveDate = start.ToString("yyyy-MM");
                while (start < end)
                {
                    string date = start.ToString("d MMMM yyyy",
                        CultureInfo.CreateSpecificCulture(_l10i.Culture));
                    string prefix = _l10i.MainPage + "/";
                    string pageName = prefix + date;
                    string fileName = _cacheDir + date + ".bin";
                    bool archived = doc.SelectSingleNode("//page[@title=\"" + pageName + "\"]") == null;

                    Day day = new Day();
                    day.Archived = archived;
                    day.Date = start;
                    Console.Out.WriteLine("Processing " + pageName + "...");
                    string text = "";
                    if (File.Exists(fileName))
                    {
                        using (FileStream fs = new FileStream(fileName, FileMode.Open))
                        using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                        using (TextReader sr = new StreamReader(gs))
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
                            day.Exists = false;
                            days.Add(day);
                            start = start.AddDays(1);
                            continue;
                        }
                        using (FileStream fs = new FileStream(fileName, FileMode.Create))
                        using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                        using (StreamWriter sw = new StreamWriter(gs))
                        {
                            sw.Write(text);
                        }
                    }
                    day.Exists = true;
                    day.Page = WikiPage.Parse(pageName, text);
                    days.Add(day);

                    start = start.AddDays(1);
                }

                days.Sort(CompareDays);

                using (StreamWriter sw =
                            new StreamWriter(_cacheDir + "Archive-" + archiveDate + ".txt"))
                {
                    sw.WriteLine("{| class=standard");
                    sw.WriteLine("|-");
                    sw.WriteLine("!| Дата");
                    sw.WriteLine("!| " + _l10i.ArchiveTemplate);
                    sw.WriteLine("|-\n");

                    StringBuilder sb = new StringBuilder();
                    foreach (Day day in days)
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
                    wiki.SavePage(_l10i.ArchivePage + archiveDate, text,
                        _l10i.MainPageUpdateComment);
                }
                start.AddMonths(1);
            }
        }

        internal static int CompareDays(Day x, Day y)
        {
            return y.Date.CompareTo(x.Date);
        }

        private void StrikeOutSection(WikiPageSection section)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            if (section.Subsections.Count(s => s.Title.Trim() == _l10i.Result) > 0)
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
            if (section.Subsections.Count(s => s.Title.Trim() == _l10i.Result) == 0)
            {
                if (section.Title.Contains("<s>"))
                {
                    section.Title = section.Title.Replace("<s>", "");
                    section.Title = section.Title.Replace("</s>", "");
                }
            }
            section.ForEach(RemoveStrikeOut);
        }

        private static string SubsectionsList(WikiPageSection section,
            string aggregator)
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
    }
}
