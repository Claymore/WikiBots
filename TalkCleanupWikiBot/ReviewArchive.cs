using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using Claymore.SharpMediaWiki;

namespace Claymore.TalkCleanupWikiBot
{
    internal class ReviewArchive : IModule
    {
        private string _cacheDir;
        private string _language;
        private static Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
        private static CultureInfo culture = CultureInfo.CreateSpecificCulture("ru-RU");

        private string MainPage { get; set; }
        private int Delay { get; set; }
        private string Archive { get; set; }

        public ReviewArchive(string mainPage, string archive, string dirName, int delay)
        {
            _language = "ru";
            _cacheDir = "Cache\\" + _language + "\\Archive\\" + dirName + "\\";
            MainPage = mainPage;
            Delay = delay;
            Archive = archive;
            Directory.CreateDirectory(_cacheDir);
        }

        public void Archivate(Wiki wiki)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            parameters.Add("rvprop", "timestamp");
            parameters.Add("redirects");
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

            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            WikiPage page = WikiPage.Parse(MainPage, text);
            var archives = new Dictionary<string, WikiPageSection>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();

            pageFileName = _cacheDir + "archive.txt";
            xml = wiki.Query(QueryBy.Titles, parameters, new string[] { Archive });
            node = xml.SelectSingleNode("//page");
            text = LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, Archive);
            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + Archive + "...");
                text = wiki.LoadPage(Archive);
                CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }
            var arch = WikiPage.Parse(Archive, text);

            foreach (WikiPageSection section in page.Sections)
            {
                Match m = wikiLinkRE.Match(section.Title);
                if (m.Success)
                {
                    string title = m.Groups[1].Value;
                    xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                    node = xml.SelectSingleNode("//page");
                    if (node.Attributes["missing"] == null)
                    {
                        title = node.Attributes["title"].Value;
                    }
                    else
                    {
                        continue;
                    }
                    MatchCollection ms = timeRE.Matches(FilterQuotes(section.Text));
                    DateTime published = DateTime.Today;
                    DateTime lastReply = DateTime.MinValue;
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, culture,
                            DateTimeStyles.AssumeUniversal);
                        if (time < published)
                        {
                            published = time;
                        }
                        if (time > lastReply)
                        {
                            lastReply = time;
                        }
                    }
                    if (lastReply != DateTime.MinValue && (DateTime.Today - lastReply).TotalHours >= Delay)
                    {
                        string period;
                        if (published.Month == lastReply.Month && published.Year == lastReply.Year)
                        {
                            period = string.Format("Рецензия с {0} по {1} года",
                                published.Day, lastReply.ToString("d MMMM yyyy"));
                        }
                        else if (published.Year == lastReply.Year)
                        {
                            period = string.Format("Рецензия с {0} по {1} года",
                                published.ToString("d MMMM"), lastReply.ToString("d MMMM yyyy"));
                        }
                        else
                        {
                            period = string.Format("Рецензия с {0} по {1} года",
                                published.ToString("d MMMM yyy"), lastReply.ToString("d MMMM yyyy"));
                        }
                        section.Title = period;
                        section.SectionText = "{{closed}}\n" + section.SectionText;
                        if (section.Subsections.Count != 0)
                        {
                            section.Subsections[section.Subsections.Count - 1].SectionText += "{{ecs}}";
                        }
                        else
                        {
                            section.SectionText += "{{ecs}}";
                        }
                        string talk = title.StartsWith("Портал:")
                                   ? "Обсуждение портала:" + title.Substring(7)
                                   : "Обсуждение:" + title;
                        if (!archives.ContainsKey(talk))
                        {
                            archives.Add(talk, section);
                        }
                        string archiveSectionTitle = lastReply.ToString("MMMM yyyy");
                        var archiveSection = arch.Sections.FirstOrDefault(ss => ss.Title.Trim() == archiveSectionTitle);
                        
                        if (archiveSection != null)
                        {
                            archiveSection.SectionText = "# [[" + title +
                                                         "]]: [[" + talk + "#" + period + "|" + period + "]];\n"
                                                         + archiveSection.SectionText;
                        }
                        else
                        {
                            archiveSection = new WikiPageSection(period, 2, "# [[" + title +
                                                         "]]: [[" + talk + "#" + period + "|" + period + "]];\n");
                            arch.Sections.Insert(0, archiveSection);
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
            foreach (string pageName in archives.Keys)
            {
                xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
                node = xml.SelectSingleNode("//page");
                if (node.Attributes["missing"] == null)
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);
                }
                else
                {
                    text = "";
                }

                WikiPage archivePage = WikiPage.Parse(pageName, text);
                var section = archives[pageName];
                archivePage.Sections.Add(section);
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

            for (int i = 0; i < 5; ++i)
            {
                try
                {
                    revid = wiki.SavePage(Archive,
                                "",
                                arch.Text,
                                "обновление",
                                MinorFlags.Minor,
                                CreateFlags.NoCreate,
                                WatchFlags.None,
                                SaveFlags.Replace,
                                baseTimeStamp,
                                "",
                                editToken);
                    if (revid != null)
                    {
                        CachePage(_cacheDir + "archive.txt", revid, arch.Text);
                    }
                    break;
                }
                catch (WikiException)
                {
                }
            }

            Regex reviewRE = new Regex(@"\{\{рецензия(\|.+?)?\}\}", RegexOptions.IgnoreCase);

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
                            "рецензия",
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

                string talk = archiveText.Key.StartsWith("Портал:") ? "Обсуждение портала:" : "Обсуждение:";
                string pageName = archiveText.Key.Substring(talk.Length);
                text = "";
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        text = wiki.LoadPage(pageName);
                        break;
                    }
                    catch (WikiException)
                    {
                    }
                }
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                Console.Out.WriteLine("Saving " + pageName + "...");
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        wiki.SavePage(pageName,
                            "",
                            reviewRE.Replace(text, ""),
                            "статья прошла рецензирование",
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
