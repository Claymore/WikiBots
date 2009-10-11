using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class Archive : IArchive
    {
        protected string _cacheDir;
        protected static Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");
        protected static CultureInfo culture = CultureInfo.CreateSpecificCulture("ru-RU");

        protected string MainPage { get; set; }
        protected string Format { get; set; }
        protected int Delay { get; set; }
        protected bool CheckForResult { get; set; }
        protected bool NewSectionsDown { get; set; }
        protected string Header { get; set; }
        public TitleProcessor Processor;

        public Archive(string title,
                       string directory,
                       int days,
                       string format,
                       string header,
                       bool checkForResult,
                       bool newSectionsDown)
        {
            MainPage = title;
            _cacheDir = directory;
            Format = format;
            Delay = days * 24;
            CheckForResult = checkForResult;
            NewSectionsDown = newSectionsDown;
            Header = header;
        }

        public virtual Dictionary<string, string> Process(Wiki wiki, WikiPage page)
        {
            var results = new Dictionary<string, string>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();
            foreach (WikiPageSection section in page.Sections)
            {
                WikiPageSection result = section.Subsections.FirstOrDefault(ss => ss.Title.Trim().ToLower() == "итог");
                if ((result != null && !string.IsNullOrEmpty(result.SectionText.Trim())) || !CheckForResult)
                {
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
                        archivedSections.Add(section);
                    }
                }
                if (IsMovedSection(section))
                {
                    section.SectionText = section.SectionText.Trim(new char[] { ' ', '\t', '\n' }) + "\n~~~~";
                }
            }
            
            if (archivedSections.Count == 0)
            {
                return results;
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");
            string pageName = Format;
            string pageFileName = _cacheDir + Cache.EscapePath(pageName);
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
            XmlNode node = xml.SelectSingleNode("//page");
            string text;
            if (node.Attributes["missing"] == null)
            {
                text = Cache.LoadPageFromCache(pageFileName,
                        node.Attributes["lastrevid"].Value, pageName);

                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadPage(pageName);
                    Cache.CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
                }
            }
            else
            {
                text = Header;
            }

            WikiPage archivePage = WikiPage.Parse(pageName, text);
            foreach (WikiPageSection section in archivedSections)
            {
                section.Title = ProcessSectionTitle(section.Title);
                archivePage.Sections.Add(section);
            }
            if (NewSectionsDown)
            {
                archivePage.Sections.Sort(SectionsDown);
            }
            else
            {
                archivePage.Sections.Sort(SectionsUp);
            }
            results.Add(pageName, archivePage.Text);
            
            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            
            return results;
        }

        public virtual void Save(Wiki wiki, WikiPage page, Dictionary<string, string> archives)
        {
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
                        page.BaseTimestamp,
                        "",
                        page.Token);
            if (revid != null)
            {
                string fileName = _cacheDir + Cache.EscapePath(MainPage);
                Cache.CachePage(fileName, revid, page.Text);
            }
            foreach (var archive in archives)
            {
                Console.Out.WriteLine("Saving " + archive.Key + "...");
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        wiki.SavePage(archive.Key,
                            "",
                            archive.Value,
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

        public void Archivate(Wiki wiki)
        {
            WikiPage page = Cache.Load(wiki, MainPage, _cacheDir);
            var pages = Process(wiki, page);
            if (pages.Count > 0)
            {
                Save(wiki, page, pages);
            }
        }

        public delegate string TitleProcessor(string title);

        protected string ProcessSectionTitle(string title)
        {
            if (Processor != null)
            {
                return Processor(title);
            }
            return title;
        }

        protected static bool IsMovedSection(WikiPageSection section)
        {
            Regex re = new Regex(@"^\s*\{\{(moved|перенесено на|обсуждение перенесено|moved to|перенесено в)\|(.+?)\}\}\s*$", RegexOptions.IgnoreCase);
            Match m = re.Match(section.SectionText);
            return m.Success;
        }

        protected static string FilterQuotes(string text)
        {
            Regex re = new Regex(@"(<blockquote>.+?</blockquote>)|(\{{2}(н|Н)ачало цитаты\|?.*?\}{2}.+?\{{2}(к|К)онец цитаты\|?.*?\}{2})");
            return re.Replace(text, "");
        }

        protected int SectionsUp(WikiPageSection x, WikiPageSection y)
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

        protected int SectionsDown(WikiPageSection x, WikiPageSection y)
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
            return publishedX.CompareTo(publishedY);
        }
    }
}
