using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class ArchiveByPeriod : Archive
    {
        public ArchiveByPeriod(string title, int days, string format, bool checkForResult, bool newSectionsDown)
            : base(title, days, format, checkForResult, newSectionsDown)
        {
            Regex escapeChars = new Regex(@"([dfFghHKmMstyYz:/OoRrsuGTU])");
            Format = escapeChars.Replace(format, "\\$1");
            Format = Format.Replace("%(год)", "yyyy").Replace("%(месяц)", "MM");
        }

        public override Dictionary<string, string> Process(Wiki wiki, WikiPage page)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            Dictionary<DateTime, List<WikiPageSection>> archives = new Dictionary<DateTime, List<WikiPageSection>>();
            List<WikiPageSection> archivedSections = new List<WikiPageSection>();
            foreach (WikiPageSection section in page.Sections)
            {
                WikiPageSection result = section.Subsections.FirstOrDefault(ss => ss.Title.Trim().ToLower() == "итог");
                if ((result != null && !string.IsNullOrEmpty(result.SectionText.Trim())) || !CheckForResult)
                {
                    MatchCollection ms = timeRE.Matches(FilterQuotes(section.Text));
                    DateTime published = NormalizeDate(DateTime.Today);
                    DateTime lastReply = DateTime.MinValue;
                    foreach (Match match in ms)
                    {
                        string value = match.Groups[1].Value;
                        DateTime time = DateTime.Parse(value, culture,
                            DateTimeStyles.AssumeUniversal);
                        if (time < published)
                        {
                            published = NormalizeDate(time);
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
                if (IsMovedSection(section))
                {
                    section.SectionText = section.SectionText.Trim(new char[] { ' ', '\t', '\n' }) + "\n~~~~";
                }
            }

            if (archivedSections.Count == 0)
            {
                return results;
            }

            foreach (DateTime period in archives.Keys)
            {
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("prop", "info");
                string pageName = DateToPageName(period);
                string pageFileName = _cacheDir + period.ToString(Format).Replace('/', '-').Replace(':', '-');
                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { pageName });
                XmlNode node = xml.SelectSingleNode("//page");
                string text;
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
            }

            foreach (var section in archivedSections)
            {
                page.Sections.Remove(section);
            }
            return results;
        }

        protected virtual string DateToPageName(DateTime date)
        {
            return date.ToString(Format);
        }

        protected virtual DateTime NormalizeDate(DateTime date)
        {
            return date;
        }
    }

    internal class ArchiveByMonth : ArchiveByPeriod
    {
        public ArchiveByMonth(string title, int days, string format, bool checkForResult, bool newSectionsDown)
            : base(title, days, format, checkForResult, newSectionsDown)
        {
        }

        protected override DateTime NormalizeDate(DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }
    }

    internal class ArchiveByYear : ArchiveByPeriod
    {
        public ArchiveByYear(string title, int days, string format, bool checkForResult, bool newSectionsDown)
            : base(title, days, format, checkForResult, newSectionsDown)
        {
        }

        protected override DateTime NormalizeDate(DateTime date)
        {
            return new DateTime(date.Year, 1, 1);
        }
    }

    internal class ArchiveByHalfYear : ArchiveByPeriod
    {
        public ArchiveByHalfYear(string title, int days, string format, bool checkForResult, bool newSectionsDown)
            : base(title, days, format, checkForResult, newSectionsDown)
        {
            Regex escapeChars = new Regex(@"([dfFghHKmMstyYz:/OoRrsuGTU])");
            Format = escapeChars.Replace(MainPage + "/" + format, "\\$1");
            Format = Format.Replace("%(год)", "yyyy").Replace("%(полугодие)", "{0}");
        }

        protected override DateTime NormalizeDate(DateTime date)
        {
            return new DateTime(date.Year, date.Month < 7 ? 1 : date.Month, 1);
        }

        protected override string DateToPageName(DateTime date)
        {
            int number = date.Month < 7 ? 1 : 2;
            return date.ToString(string.Format(Format, number));
        }
    }
}
