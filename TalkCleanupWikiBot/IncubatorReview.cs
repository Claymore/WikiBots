using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.TalkCleanupWikiBot
{
    internal class IncubatorReview : IModule
    {
        private string _cacheDir;
        private string _page;

        public IncubatorReview(string page)
        {
            _cacheDir = "Cache\\ru\\IncubatorReview\\";
            _page = page;
            Directory.CreateDirectory(_cacheDir);
        }

        public void Run(Wiki wiki)
        {
            Directory.CreateDirectory(_cacheDir);

            Console.Out.WriteLine("Updating " + _page);
            Regex wikiLinkRE = new Regex(@"\[{2}(.+?)(\|.+?)?]{2}");
            Regex timeRE = new Regex(@"(\d{2}:\d{2}\, \d\d? [а-я]+ \d{4}) \(UTC\)");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            XmlDocument doc = wiki.Query(QueryBy.Titles, parameters, _page);
            XmlNode page = doc.SelectSingleNode("//page");
            string queryTimestamp = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            string pageName = page.Attributes["title"].Value;
            string basetimestamp = page.FirstChild.FirstChild.Attributes["timestamp"].Value;
            string editToken = page.Attributes["edittoken"].Value;
            string starttimestamp = queryTimestamp;

            string text = "";
            string fileName = _cacheDir + WikiCache.EscapePath(_page);
            if (File.Exists(fileName))
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                using (TextReader sr = new StreamReader(gs))
                {
                    string revid = sr.ReadLine();
                    if (revid == page.Attributes["lastrevid"].Value)
                    {
                        Console.Out.WriteLine("Loading " + pageName + "...");
                        text = sr.ReadToEnd();
                    }
                }
            }
            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + pageName + "...");
                text = wiki.LoadText(pageName);
                starttimestamp = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

                using (FileStream fs = new FileStream(fileName, FileMode.Create))
                using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
                using (StreamWriter sw = new StreamWriter(gs))
                {
                    sw.WriteLine(page.Attributes["lastrevid"].Value);
                    sw.Write(text);
                }
            }

            Dictionary<string, List<WikiPageSection>> titles = new Dictionary<string, List<WikiPageSection>>();
            WikiPage wikiPage = WikiPage.Parse(pageName, text);
            foreach (WikiPageSection section in wikiPage.Sections)
            {
                if (section.Subsections.Count(s => s.Title.ToLower(CultureInfo.CreateSpecificCulture("ru-RU")).Trim() == "итог") == 0)
                {
                    Match m = wikiLinkRE.Match(section.Title);
                    if (m.Success)
                    {
                        string title = m.Groups[1].Value.Trim();
                        if (titles.ContainsKey(title))
                        {
                            titles[title].Add(section);
                        }
                        else
                        {
                            titles.Add(title, new List<WikiPageSection>());
                            titles[title].Add(section);
                        }
                    }
                }
            }

            parameters.Clear();
            parameters.Add("prop", "info");
            Dictionary<string, string> normalizedTitles = new Dictionary<string, string>();
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, titles.Keys);
            foreach (XmlNode node in xml.SelectNodes("//n"))
            {
                normalizedTitles.Add(node.Attributes["to"].Value,
                    node.Attributes["from"].Value);
            }
            List<string> notificationList = new List<string>();
            XmlNodeList missingTitles = xml.SelectNodes("//page");
            foreach (XmlNode node in missingTitles)
            {
                string title = node.Attributes["title"].Value;

                IEnumerable<WikiPageSection> sections;
                if (titles.ContainsKey(title))
                {
                    sections = titles[title];
                }
                else
                {
                    sections = titles[normalizedTitles[title]];
                }

                if (node.Attributes["missing"] != null)
                {
                    parameters.Clear();
                    parameters.Add("list", "logevents");
                    parameters.Add("letype", "delete");
                    parameters.Add("lemlimit", "max");
                    parameters.Add("ledir", "newer");
                    parameters.Add("letitle", title);
                    parameters.Add("leprop", "comment|type|user|timestamp");
                    XmlDocument log = wiki.Enumerate(parameters, true);
                    XmlNodeList items = log.SelectNodes("//item");
                    List<DeleteLogEvent> events = new List<DeleteLogEvent>();
                    foreach (XmlNode item in items)
                    {
                        DeleteLogEvent ev = new DeleteLogEvent();
                        ev.Comment = item.Attributes["comment"].Value;
                        ev.Deleted = item.Attributes["action"].Value == "delete";
                        ev.User = item.Attributes["user"].Value;
                        ev.Timestamp = DateTime.Parse(item.Attributes["timestamp"].Value,
                            null,
                            DateTimeStyles.AssumeUniversal);
                        events.Add(ev);
                    }
                    events.Sort(CompareDeleteLogEvents);
                    if (events.Count > 0 && events[0].Deleted)
                    {
                        string comment = FilterWikiMarkup(events[0].Comment);
                        string message = string.Format("Страница была удалена {1} администратором [[User:{0}|]]. Была указана следующая причина: «{2}». Данное сообщение было автоматически сгенерировано ботом ~~~~.\n",
                            events[0].User,
                            events[0].Timestamp.ToUniversalTime().ToString("d MMMM yyyy в HH:mm (UTC)",
                                CultureInfo.CreateSpecificCulture("ru-RU")),
                                comment);
                        var pageSections = titles.ContainsKey(title)
                            ? titles[title]
                            : titles[normalizedTitles[title]];
                        foreach (WikiPageSection section in pageSections)
                        {
                            WikiPageSection verdict = new WikiPageSection(" Итог ",
                                section.Level + 1,
                                message);
                            section.AddSubsection(verdict);
                        }
                    }
                }       
            }

            string newText = wikiPage.Text;
            if (newText.Trim() == text.Trim())
            {
                return;
            }
            Console.Out.WriteLine("Updating " + pageName + "...");
            string id = wiki.Save(pageName,
                "",
                newText,
                "автоматическое подведение итогов",
                MinorFlags.Minor,
                CreateFlags.NoCreate,
                WatchFlags.None,
                SaveFlags.Replace,
                true,
                basetimestamp,
                "",
                editToken);

            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
            using (StreamWriter sw = new StreamWriter(gs))
            {
                sw.WriteLine(id);
                sw.Write(newText);
            }
        }

        static int CompareDeleteLogEvents(DeleteLogEvent x, DeleteLogEvent y)
        {
            return y.Timestamp.CompareTo(x.Timestamp);
        }

        private static string FilterWikiMarkup(string line)
        {
            Regex commentRE = new Regex(@"\[{2}(File|Файл|Image|Изображение|Category|Категория):(.+?)(\|.+)?(\]{2})?$");
            string comment = line;
            comment = comment.Replace("{{", "<nowiki>{{").Replace("}}", "}}</nowiki>").Replace("'''", "").Replace("''", "").Trim();
            comment = comment.Replace("<ref>", "<nowiki><ref>").Replace("</ref>", "</ref></nowiki>");
            comment = comment.Replace("<!--", "<nowiki><!--").Replace("-->", "--></nowiki>");
            comment = commentRE.Replace(comment, "<nowiki>[[</nowiki>[[:$1:$2]]<nowiki>$3]]</nowiki>");
            if (comment.Contains("<nowiki>"))
            {
                for (int index = comment.IndexOf("<nowiki>");
                     index != -1;
                     index = comment.IndexOf("<nowiki>", index + 1))
                {
                    int endIndex = comment.IndexOf("</nowiki>", index);
                    if (endIndex == -1)
                    {
                        comment += "</nowiki>";
                        break;
                    }
                }
            }
            return comment;
        }
    }
}
