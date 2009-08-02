using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Claymore.SharpMediaWiki;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace Claymore.TalkCleanupWikiBot
{
    internal class AdministratorIntervention
    {
        private string _cacheDir;
        private string _language;
        private static Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
        private static CultureInfo culture = CultureInfo.CreateSpecificCulture("ru-RU");

        public AdministratorIntervention()
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\AdministratorIntervention\\";
            Directory.CreateDirectory(_cacheDir);
        }

        public void Archive(Wiki wiki)
        {
            string starttimestamp = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            parameters.Add("rvprop", "timestamp");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { "Википедия:Запросы к администраторам" });
            XmlNode node = xml.SelectSingleNode("//rev");
            string baseTimeStamp = null;
            if (node != null && node.Attributes["timestamp"] != null)
            {
                baseTimeStamp = node.Attributes["timestamp"].Value;
            }
            node = xml.SelectSingleNode("//page");
            string editToken = node.Attributes["edittoken"].Value;
            
            string pageFileName = _cacheDir + "Запросы к администраторам";
            string text = LoadPageFromCache(pageFileName,
                        node.Attributes["lastrevid"].Value, "Википедия:Запросы к администраторам");

            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading Википедия:Запросы к администраторам...");
                text = wiki.LoadPage("Википедия:Запросы к администраторам");
                CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }

            WikiPage page = WikiPage.Parse("Википедия:Запросы к администраторам", text);
            Dictionary<DateTime, List<WikiPageSection>> archives = new Dictionary<DateTime, List<WikiPageSection>>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();
            foreach (WikiPageSection section in page.Sections)
            {
                if (section.Subsections.Any(ss => ss.Title.Trim() == "Итог"))
                {
                    MatchCollection ms = timeRE.Matches(section.Text);
                    DateTime published = DateTime.Today;
                    DateTime lastReply = DateTime.MinValue;
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, culture,
                            DateTimeStyles.AssumeUniversal);
                        if (time < published)
                        {
                            published = new DateTime(time.Year, time.Month, 1);
                        }
                        if (time > lastReply)
                        {
                            lastReply = time;
                        }
                    }
                    if (lastReply != DateTime.MinValue && (DateTime.Today - lastReply).TotalDays >= 3)
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
            foreach (DateTime month in archives.Keys)
            {
                string pageName = month.ToString("Википедия:Запросы к администраторам\\/Архив\\/yyyy\\/MM");
                pageFileName = _cacheDir + month.ToString("yyyy-MM");
                xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
                node = xml.SelectSingleNode("//page");
                text = LoadPageFromCache(pageFileName,
                            node.Attributes["lastrevid"].Value, pageName);

                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);
                    CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
                }
                WikiPage archivePage = WikiPage.Parse(pageName, text);
                foreach (WikiPageSection section in archives[month])
                {
                    archivePage.Sections.Add(section);
                }
                archivePage.Sections.Sort(CompareSections);
                Console.Out.WriteLine("Saving " + pageName + "...");
                wiki.SavePage(pageName, archivePage.Text, "архивация");
            }
            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            wiki.SavePage("Википедия:Запросы к администраторам", page.Text, "архивация");
            string revid = wiki.SavePage("Википедия:Запросы к администраторам",
                        "",
                        page.Text,
                        "архивация",
                        MinorFlags.Minor,
                        CreateFlags.NoCreate,
                        WatchFlags.None,
                        SaveFlags.Replace,
                        baseTimeStamp,
                        starttimestamp,
                        editToken);
            if (revid != null)
            {
                CachePage("Запросы к администраторам", revid, page.Text);
            }
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
    }
}
