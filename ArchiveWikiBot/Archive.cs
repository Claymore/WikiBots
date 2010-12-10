using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class Archive : IArchive
    {
        protected string _cacheDir;
        protected static Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? \S+? \d{4}) \(UTC\)");

        protected string MainPage { get; set; }
        protected string Format { get; set; }
        protected int Delay { get; set; }
        protected bool CheckForResult { get; set; }
        protected bool NewSectionsDown { get; set; }
        protected string Header { get; set; }
        public TitleProcessor Processor;
        protected IEnumerable<string> LookForLines { get; set; }
        protected IEnumerable<string> OnHold { get; set; }
        protected string RemoveFromText { get; set; }
        protected L10i L10i { get; set; }
        protected int MinimalSize { get; set; }
        protected int ForcedArchivationDelay { get; set; }
        
        public Archive(L10i l10i,
                       string title,
                       string directory,
                       int days,
                       string format,
                       string header,
                       IEnumerable<string> lookForLines,
                       IEnumerable<string> onHold,
                       string removeFromText,
                       bool checkForResult,
                       bool newSectionsDown,
                       int minimalSize,
                       int forceArchivationDelay)
        {
            MainPage = title;
            _cacheDir = directory;
            Format = format;
            Delay = days * 24;
            CheckForResult = checkForResult;
            NewSectionsDown = newSectionsDown;
            Header = header;
            LookForLines = lookForLines;
            OnHold = onHold;
            RemoveFromText = removeFromText;
            L10i = l10i;
            MinimalSize = minimalSize;
            ForcedArchivationDelay = forceArchivationDelay * 24;
        }

        public virtual Dictionary<string, string> Process(Wiki wiki, WikiPage page, ref int diffSize, ref int topics)
        {
            var results = new Dictionary<string, string>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();
            foreach (WikiPageSection section in page.Sections)
            {
                WikiPageSection result = section.Subsections.FirstOrDefault(ss => ss.Title.Trim().ToLower() == "итог");
                bool forceArchivation = LookForLines.Any(s => section.Text.ToLower().Contains(s.ToLower()));
                if (!OnHold.Any(s => section.Text.ToLower().Contains(s.ToLower())) &&
                    ((result != null && !string.IsNullOrEmpty(result.SectionText.Trim())) ||
                     forceArchivation ||
                     !CheckForResult))
                {
                    MatchCollection ms = timeRE.Matches(FilterQuotes(section.Text));
                    DateTime published = DateTime.Today;
                    DateTime lastReply = DateTime.MinValue;
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, L10i.Culture,
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
                    if (lastReply != DateTime.MinValue &&
                        ((forceArchivation && (DateTime.Today - lastReply).TotalHours >= ForcedArchivationDelay) ||
                        (DateTime.Today - lastReply).TotalHours >= Delay))
                    {
                        archivedSections.Add(section);
                    }
                }
                if (IsMovedSection(section))
                {
                    section.SectionText = section.SectionText.Trim(new char[] { ' ', '\t', '\n' }) + "\n~~~~\n";
                }
            }
            
            if (archivedSections.Count == 0)
            {
                diffSize = 0;
                return results;
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");
            string pageName = Format;
            string pageFileName = _cacheDir + Cache.EscapePath(pageName);
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, pageName);
            XmlNode node = xml.SelectSingleNode("//page");
            string text;
            if (node.Attributes["missing"] == null)
            {
                text = Cache.LoadPageFromCache(pageFileName,
                        node.Attributes["lastrevid"].Value, pageName);

                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadText(pageName);
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
            if (!string.IsNullOrEmpty(RemoveFromText))
            {
                archivePage.Text = archivePage.Text.Replace(RemoveFromText, "");
            }
            results.Add(pageName, archivePage.Text);

            diffSize = 0;
            topics = 0;
            foreach (var section in archivedSections)
            {
                diffSize += Encoding.UTF8.GetByteCount(section.Text);
                ++topics;
                page.Sections.Remove(section);
            }
            return results;
        }

        public virtual void Save(Wiki wiki, WikiPage page, Dictionary<string, string> archives, int topics)
        {
            StringBuilder linksToArchives = new StringBuilder();
            foreach (var archive in archives)
            {
                linksToArchives.AppendFormat(", [[{0}]]", archive.Key);
            }
            linksToArchives.Remove(0, 2);
            Console.Out.WriteLine("Saving " + MainPage + "...");
            string revid = page.Save(wiki, string.Format("{0} ({1}) → {2}",
                L10i.UpdateComment,
                topics,
                linksToArchives));
            if (revid != null)
            {
                string fileName = _cacheDir + Cache.EscapePath(MainPage);
                Cache.CachePage(fileName, revid, page.Text);
            }
            foreach (var archive in archives)
            {
                WikiPage a = new WikiPage(archive.Key, archive.Value);
                Console.Out.WriteLine("Saving " + a.Title + "...");
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        a.Save(wiki, string.Format("{0} ← [[{1}]]", L10i.UpdateComment, page.Title));
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
            int diffSize = 0;
            int topics = 0;
            var pages = Process(wiki, page, ref diffSize, ref topics);
            if (pages.Count > 0 && diffSize >= MinimalSize)
            {
                Save(wiki, page, pages, topics);
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
            Regex re = new Regex(@"(<blockquote>.+?</blockquote>)|(\{{2}(н|Н)ачало цитаты\|?.*?\}{2}.+?\{{2}(к|К)онец цитаты\|?.*?\}{2})", RegexOptions.Singleline);
            return re.Replace(text, "");
        }

        protected int SectionsUp(WikiPageSection x, WikiPageSection y)
        {
            MatchCollection ms = timeRE.Matches(x.Text);
            DateTime publishedX = DateTime.MaxValue;
            foreach (Match match in ms)
            {
                string value = match.Groups[1].Value;
                DateTime time;
                if (DateTime.TryParse(value, L10i.Culture,
                    DateTimeStyles.AssumeUniversal, out time) &&
                    time < publishedX)
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
                DateTime time;
                if (DateTime.TryParse(value, L10i.Culture,
                    DateTimeStyles.AssumeUniversal, out time) &&
                    time < publishedY)
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
                DateTime time = DateTime.Parse(value, L10i.Culture,
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
                DateTime time = DateTime.Parse(value, L10i.Culture,
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
