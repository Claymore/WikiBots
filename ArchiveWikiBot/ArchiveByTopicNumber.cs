using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class ArchiveByTopicNumber : Archive
    {
        private int Topics { get; set; }

        public ArchiveByTopicNumber(L10i l10i,
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
                               int topics)
            : base(l10i, title, directory, days, format, header, lookForLines, onHold, removeFromText, checkForResult, newSectionsDown)
        {
            Topics = topics;
            Format = format.Replace("%(номер)", "{0}");
        }

        public override Dictionary<string, string> Process(Wiki wiki, WikiPage page)
        {
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
                        (forceArchivation || (DateTime.Today - lastReply).TotalHours >= Delay))
                    {
                        archivedSections.Add(section);
                    }
                }
                if (IsMovedSection(section))
                {
                    section.SectionText = section.SectionText.Trim(new char[] { ' ', '\t', '\n' }) + "\n~~~~\n";
                    archivedSections.Add(section);
                }
            }
            Dictionary<string, string> archiveTexts = new Dictionary<string, string>();

            if (archivedSections.Count == 0)
            {
                return archiveTexts;
            }

            string talkPrefix = "Обсуждение участника:";
            var parameters = new ParameterCollection();
            parameters.Add("list", "allpages");
            parameters.Add("apprefix", MainPage.Substring(talkPrefix.Length));
            parameters.Add("apnamespace", "3");
            XmlDocument xml = wiki.Enumerate(parameters, true);

            int maxNumber = 1;
            foreach (XmlNode p in xml.SelectNodes("//p"))
            {
                string title = p.Attributes["title"].Value;
                string format = Format.Replace("{0}", "");
                if (title.StartsWith(format))
                {
                    int number;
                    if (int.TryParse(title.Substring(format.Length), out number))
                    {
                        if (number > maxNumber)
                        {
                            maxNumber = number;
                        }
                    }
                }
            }

            int index = 0;
            string pageName = string.Format(Format, maxNumber);
            parameters.Clear();
            parameters.Add("prop", "info");
            xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
            XmlNode node = xml.SelectSingleNode("//page");
            if (node.Attributes["missing"] == null)
            {
                string pageFileName = _cacheDir + Cache.EscapePath(pageName);
                string text = Cache.LoadPageFromCache(pageFileName,
                                node.Attributes["lastrevid"].Value, pageName);

                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Downloading " + pageName + "...");
                    text = wiki.LoadText(pageName);
                    Cache.CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
                }
                WikiPage archivePage = WikiPage.Parse(pageName, text);
                if (archivePage.Sections.Count < Topics)
                {
                    int topics = Topics - archivePage.Sections.Count;
                    for (int i = 0; i < topics && index < archivedSections.Count; ++i, ++index)
                    {
                        WikiPageSection section = archivedSections[index];
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
                    archiveTexts.Add(pageName, archivePage.Text);
                }
            }
            if (index < archivedSections.Count)
            {
                string text = Header;
                pageName = string.Format(Format, maxNumber + 1);
                WikiPage archivePage = WikiPage.Parse(pageName, text);
                for (; index < archivedSections.Count; ++index)
                {
                    WikiPageSection section = archivedSections[index];
                    section.Title = ProcessSectionTitle(section.Title);
                    archivePage.Sections.Add(section);
                }
                archivePage.Sections.Sort(SectionsUp);
                if (!string.IsNullOrEmpty(RemoveFromText))
                {
                    archivePage.Text = archivePage.Text.Replace(RemoveFromText, "");
                }
                archiveTexts.Add(pageName, archivePage.Text);
            }

            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            return archiveTexts;
        }
    }
}
