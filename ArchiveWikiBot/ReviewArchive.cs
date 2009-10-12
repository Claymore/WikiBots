﻿using System;
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
            var pageTexts = new Dictionary<string, string>();
            var talkPages = new Dictionary<string, WikiPageSection>();
            var archivedSections = new List<WikiPageSection>();
            Regex reviewRE = new Regex(@"\{\{рецензия(\|.+?)?\}\}", RegexOptions.IgnoreCase);
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");
            parameters.Add("redirects");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, Format);
            XmlNode node = xml.SelectSingleNode("//page");

            string pageFileName = _cacheDir + "archive.txt";
            string text = Cache.LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, Format);
            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + Format + "...");
                text = WikiPage.LoadText(Format, wiki);
                Cache.CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }

            var archive = WikiPage.Parse(Format, text);

            foreach (WikiPageSection section in page.Sections)
            {
                Match m = wikiLinkRE.Match(section.Title);
                if (m.Success)
                {
                    string title = m.Groups[1].Value;
                    xml = wiki.Query(QueryBy.Titles, parameters, title);
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

                    if (lastReply != DateTime.MinValue &&
                        (DateTime.Today - lastReply).TotalHours >= Delay)
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

                        string archiveSectionTitle = lastReply.ToString("MMMM yyyy");
                        var archiveSection = archive.Sections.FirstOrDefault(ss => ss.Title.Trim() == archiveSectionTitle);

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
                            archive.Sections.Insert(0, archiveSection);
                        }
                        archivedSections.Add(section);

                        xml = wiki.Query(QueryBy.Titles, parameters, talk);
                        node = xml.SelectSingleNode("//page");
                        if (node.Attributes["missing"] == null)
                        {
                            Console.Out.WriteLine("Downloading " + talk + "...");
                            text = WikiPage.LoadText(talk, wiki);
                        }
                        else
                        {
                            text = "";
                        }

                        WikiPage talkPage = WikiPage.Parse(talk, text);
                        talkPage.Sections.Add(section);
                        pageTexts.Add(talk, talkPage.Text);

                        WikiPage article = new WikiPage(title);
                        article.Load(wiki);
                        string newText = reviewRE.Replace(article.Text, "");
                        if (newText != article.Text)
                        {
                            pageTexts.Add(title, newText);
                        }
                    }
                }
            }

            if (archivedSections.Count == 0)
            {
                return pageTexts;
            }
            pageTexts.Add(archive.Title, archive.Text);

            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            return pageTexts;
        }

        public override void Save(Wiki wiki, WikiPage page, Dictionary<string, string> archives)
        {
            Console.Out.WriteLine("Saving " + MainPage + "...");
            string revid = page.Save(wiki, "архивация");
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
                        a.Save(wiki, a.Title == Format ? "архивация" : "статья прошла рецензирование");
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
