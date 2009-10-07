using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class ReviewArchive : Archive
    {
        public ReviewArchive(string title, string directory, int days, string archive, string header)
            : base(title, directory, days, archive, header, false, false)
        {
        }

        public override Dictionary<string, string> Process(Wiki wiki, WikiPage page)
        {
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");

            var archives = new Dictionary<string, WikiPageSection>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();

            string pageFileName = _cacheDir + "archive.txt";
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { Format });
            XmlNode node = xml.SelectSingleNode("//page");
            string text = Cache.LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, Format);
            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + Format + "...");
                text = wiki.LoadPage(Format);
                Cache.CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }
            var arch = WikiPage.Parse(Format, text);

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

            Dictionary<string, string> archiveTexts = new Dictionary<string, string>();
            if (archivedSections.Count == 0)
            {
                return archiveTexts;
            }

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
            return archiveTexts;
        }

        public override void Save(Wiki wiki, WikiPage page, Dictionary<string, string> archives)
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

            Regex reviewRE = new Regex(@"\{\{рецензия(\|.+?)?\}\}", RegexOptions.IgnoreCase);

            foreach (var archiveText in archives)
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
                string text = "";
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
    }
}
