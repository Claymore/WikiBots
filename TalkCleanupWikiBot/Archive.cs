using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using Claymore.TalkCleanupWikiBot;

namespace Claymore.TalkCleanupWikiBot
{
    internal class Archive : IModule
    {
        public enum Period
        {
            Month,
            Year
        }

        private string _cacheDir;
        private string _language;
        private static Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
        private static CultureInfo culture = CultureInfo.CreateSpecificCulture("ru-RU");

        private string MainPage { get; set; }

        private string Format { get; set; }
        private Period UsePeriod { get; set; }
        private int Delay { get; set; }
        private bool CheckForResult { get; set; }

        public Archive(string mainPage, string format, string dirName, int delay, Period period)
            : this(mainPage, format, dirName, delay, period, true)
        {
        }

        public Archive(string mainPage, string format, string dirName, int delay, Period period, bool checkForResult)
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\Archive\\" + dirName + "\\";
            MainPage = mainPage;
            Format = format;
            UsePeriod = period;
            Delay = delay;
            CheckForResult = checkForResult;
            Directory.CreateDirectory(_cacheDir);
        }

        public void Archivate(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            parameters.Add("rvprop", "timestamp");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { MainPage });
            XmlNode node = xml.SelectSingleNode("//rev");
            string baseTimeStamp = null;
            if (node != null && node.Attributes["timestamp"] != null)
            {
                baseTimeStamp = node.Attributes["timestamp"].Value;
            }
            node = xml.SelectSingleNode("//page");
            string editToken = node.Attributes["edittoken"].Value;
            
            string pageFileName = _cacheDir + "input.txt";
            string text = LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, MainPage);

            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + MainPage + "...");
                text = wiki.LoadPage(MainPage);
                CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }

            WikiPage page = WikiPage.Parse(MainPage, text);
            Dictionary<DateTime, List<WikiPageSection>> archives = new Dictionary<DateTime, List<WikiPageSection>>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();
            foreach (WikiPageSection section in page.Sections)
            {
                WikiPageSection result = section.Subsections.FirstOrDefault(ss => ss.Title.Trim().ToLower() == "итог");
                if ((result != null && !string.IsNullOrEmpty(result.SectionText.Trim())) || !CheckForResult)
                {
                    MatchCollection ms = timeRE.Matches(FilterQuotes(section.Text));
                    DateTime published = new DateTime(DateTime.Today.Year, UsePeriod == Period.Month ? DateTime.Today.Month : 1, 1);
                    DateTime lastReply = DateTime.MinValue;
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, culture,
                            DateTimeStyles.AssumeUniversal);
                        if (time < published)
                        {
                            published = new DateTime(time.Year, UsePeriod == Period.Month ? time.Month : 1, 1);
                        }
                        if (time > lastReply)
                        {
                            lastReply = time;
                        }
                    }
                    if (lastReply != DateTime.MinValue && (DateTime.Today - lastReply).TotalHours >= Delay)
                    {
                        if (archives.ContainsKey(published))
                        {
                            archives[published].Add(section);
                        }
                        else
                        {
                            List<WikiPageSection> sections = new List<WikiPageSection>();
                            sections.Add(section);
                            archives.Add(published, sections);
                        }
                        archivedSections.Add(section);
                    }
                }
            }

            if (archivedSections.Count == 0)
            {
                return;
            }

            Dictionary<string, string> archiveTexts = new Dictionary<string, string>();
            foreach (DateTime period in archives.Keys)
            {
                string pageName = period.ToString(Format);
                pageFileName = _cacheDir + period.ToString(Format).Replace('/', '-').Replace(':', '-');
                xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
                node = xml.SelectSingleNode("//page");
                if (node.Attributes["missing"] == null)
                {
                    text = LoadPageFromCache(pageFileName,
                            node.Attributes["lastrevid"].Value, pageName);

                    if (string.IsNullOrEmpty(text))
                    {
                        Console.Out.WriteLine("Downloading " + pageName + "...");
                        text = wiki.LoadPage(pageName);
                        CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
                    }
                }
                else
                {
                    text = "{{closed}}\n";
                }
                
                WikiPage archivePage = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in archives[period])
                {
                    archivePage.Sections.Add(section);
                }
                archivePage.Sections.Sort(CompareSections);
                archiveTexts.Add(pageName, archivePage.Text);
            }

            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            Console.Out.WriteLine("Saving " + MainPage + "...");
            wiki.SavePage(MainPage, page.Text, "архивация");
            string revid = wiki.SavePage(MainPage,
                        "",
                        page.Text,
                        "архивация",
                        MinorFlags.Minor,
                        CreateFlags.NoCreate,
                        WatchFlags.None,
                        SaveFlags.Replace,
                        baseTimeStamp,
                        "",
                        editToken);
            if (revid != null)
            {
                CachePage(MainPage, revid, page.Text);
            }
            foreach (var archiveText in archiveTexts)
            {
                Console.Out.WriteLine("Saving " + archiveText.Key + "...");
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        wiki.SavePage(archiveText.Key,
                            "",
                            archiveText.Value,
                            "архивация",
                            MinorFlags.Minor,
                            CreateFlags.None,
                            WatchFlags.None,
                            SaveFlags.Replace);
                        break;
                    }
                    catch (WikiException)
                    {
                    }
                }
            }
        }

        static string FilterQuotes(string text)
        {
            Regex re = new Regex(@"(<blockquote>.+?</blockquote>)|(\{{2}(н|Н)ачало цитаты\|?.*?\}{2}.+?\{{2}(к|К)онец цитаты\|?.*?\}{2})");
            return re.Replace(text, "");
        }

        static int CompareSections(WikiPageSection x, WikiPageSection y)
        {
            MatchCollection ms = timeRE.Matches(x.Text);
            DateTime publishedX = DateTime.MaxValue;
            foreach (Match match in ms)
            {
                string value = match.Groups[1].Value;
                DateTime time = DateTime.Parse(value, culture,
                    DateTimeStyles.AssumeUniversal);
                if (time < publishedX)
                {
                    publishedX = time;
                }
            }
            if (publishedX == DateTime.MaxValue)
            {
                publishedX = DateTime.MinValue;
            }
            ms = timeRE.Matches(y.Text);
            DateTime publishedY = DateTime.MaxValue;
            foreach (Match match in ms)
            {
                string value = match.Groups[1].Value;
                DateTime time = DateTime.Parse(value, culture,
                    DateTimeStyles.AssumeUniversal);
                if (time < publishedY)
                {
                    publishedY = time;
                }
            }
            if (publishedY == DateTime.MaxValue)
            {
                publishedY = DateTime.MinValue;
            }
            return publishedY.CompareTo(publishedX);
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

        #region IModule Members

        public void Run(Wiki wiki)
        {
            Archivate(wiki);
        }

        #endregion
    }
}
