using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using System.Collections.Generic;

namespace Claymore.NewPagesWikiBot
{
    internal class Cache
    {
        internal class PageInfo
        {
            public string Title;
            public string Author;
            public DateTime FirstEdit;
            public long FirstEditId;
            public string Redirect;

            public PageInfo(string title, string author, DateTime firstEdit, long firstEditId)
            {
                Title = title;
                Author = author;
                FirstEdit = firstEdit;
                FirstEditId = firstEditId;
                Redirect = "";
            }

            public PageInfo(string title, string author, DateTime firstEdit, string redirect)
            {
                Title = title;
                Author = author;
                FirstEdit = firstEdit;
                FirstEditId = 0;
                Redirect = redirect;
            }
        }

        public static void PurgeCache(string language)
        {
            if (!File.Exists(string.Format(@"Cache\{0}\pages.txt", language)))
            {
                return;
            }
            List<PageInfo> pages = new List<PageInfo>();
            using (TextReader streamReader = new StreamReader(string.Format(@"Cache\{0}\pages.txt", language)))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    DateTime time = DateTime.Parse(groups[2],
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                    if ((DateTime.Now - time).TotalHours < 720)
                    {
                        pages.Add(new PageInfo(groups[0], groups[1], time, groups[3]));
                    }
                }
            }

            using (TextWriter streamWriter = new StreamWriter(string.Format(@"Cache\{0}\pages.txt", language), false))
            {
                foreach (var page in pages)
                {
                    streamWriter.WriteLine("{0}\t{1}\t{2}Z\t{3}",
                        page.Title,
                        page.Author,
                        page.FirstEdit.ToUniversalTime().ToString("s"),
                        page.Redirect);
                }
            }
        }

        public static void LoadPageList(WebClient client, string category, string language, int depth, int hours)
        {
            string fileName = "Cache\\" + language + "\\NewPages\\" + Cache.EscapePath(category) + ".txt";
            if (!File.Exists(fileName) ||
                (DateTime.Now - File.GetLastWriteTime(fileName)).TotalHours > 1)
            {
                Console.Out.WriteLine("Downloading data for " + category);
                string url = string.Format("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang={0}&wikifam=.wikipedia.org&basecat={1}&basedeep={2}&mode=rc&hours={3}&onlynew=on&go=Сканировать&format=csv&userlang=ru",
                    language,
                    Uri.EscapeDataString(category),
                    depth,
                    hours);
                client.DownloadFile(url, fileName);
            }
        }

        public static PageInfo LoadPageInformation(Wiki wiki, string language, string title)
        {
            if (!File.Exists(string.Format(@"Cache\{0}\pages.txt", language)))
            {
                FileStream stream = File.Create(string.Format(@"Cache\{0}\pages.txt", language));
                stream.Close();
            }

            using (TextReader streamReader = new StreamReader(string.Format(@"Cache\{0}\pages.txt", language)))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == title || (groups.Length > 3 && groups[3] == title))
                    {
                        DateTime time = DateTime.Parse(groups[2],
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                        return new PageInfo(groups[0], groups[1], time, 0);
                    }
                }
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "revisions");
            parameters.Add("rvprop", "timestamp|user");
            parameters.Add("rvdir", "newer");
            parameters.Add("rvlimit", "1");
            parameters.Add("redirects");

            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
            XmlNode node = xml.SelectSingleNode("//rev");
            if (node != null)
            {
                string pageName = xml.SelectSingleNode("//page").Attributes["title"].Value;
                string user = node.Attributes["user"].Value;
                string timestamp = node.Attributes["timestamp"].Value;
                DateTime time = DateTime.Parse(timestamp,
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                using (TextWriter streamWriter = new StreamWriter(string.Format(@"Cache\{0}\pages.txt", language), true))
                {
                    streamWriter.WriteLine("{0}\t{1}\t{2}\t{3}",
                        pageName,
                        user,
                        timestamp,
                        pageName != title ? title : "");
                }
                return new PageInfo(pageName, user, time, 0);
            }
            return null;
        }

        public static string EscapePath(string path)
        {
            Regex charsRE = new Regex(@"[:/\*\?<>\|\n]");
            return charsRE.Replace(path, "_").Replace('"', '_').Replace('\\', '_');
        }

        public static WikiPage Load(Wiki wiki, string title, string directory)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            parameters.Add("rvprop", "timestamp");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
            XmlNode node = xml.SelectSingleNode("//rev");
            string baseTimeStamp = null;
            if (node != null && node.Attributes["timestamp"] != null)
            {
                baseTimeStamp = node.Attributes["timestamp"].Value;
            }
            node = xml.SelectSingleNode("//page");
            string editToken = node.Attributes["edittoken"].Value;

            string pageFileName = directory + EscapePath(title);
            string text = LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, title);

            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + title + "...");
                text = wiki.LoadText(title);
                CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }

            WikiPage page = WikiPage.Parse(title, text);
            page.BaseTimestamp = baseTimeStamp;
            page.Token = editToken;
            return page;
        }

        public static string LoadPageFromCache(string fileName,
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

        public static void CachePage(string fileName, string revisionId, string text)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
            using (StreamWriter sw = new StreamWriter(gs))
            {
                sw.WriteLine(revisionId);
                sw.Write(text);
            }
        }

        internal static void LoadPageList(WebClient client, string language, string category, int depth)
        {
            string fileName = "Cache\\" + language + "\\PagesInCategory\\" + Cache.EscapePath(category) + ".txt";
            if (!File.Exists(fileName) ||
                (DateTime.Now - File.GetLastWriteTime(fileName)).TotalHours > 1)
            {
                Console.Out.WriteLine("Downloading data for " + category);
                string query = string.Format("language={0}&depth={2}&categories={1}&sortby=title&format=tsv&doit=submit",
                    language,
                    Uri.EscapeDataString(category),
                    depth);

                UriBuilder ub = new UriBuilder("http://toolserver.org");
                ub.Path = "/~magnus/catscan_rewrite.php";
                ub.Query = query;
                client.DownloadFile(ub.Uri, fileName);
            }
        }

        internal static void LoadPageList(WebClient client, string category, string templates, string language, int depth)
        {
            string fileName = "Cache\\" + language + "\\PagesInCategoryWithTemplates\\" + Cache.EscapePath(category + "-" + templates) + ".txt";
            if (!File.Exists(fileName) ||
                (DateTime.Now - File.GetLastWriteTime(fileName)).TotalHours > 1)
            {
                Console.Out.WriteLine("Downloading data for " + category);
                string query = string.Format("language={0}&depth={3}&categories={1}&templates_any={2}&sortby=title&format=tsv&doit=submit",
                    language,
                    Uri.EscapeDataString(category),
                    templates,
                    depth);

                UriBuilder ub = new UriBuilder("http://toolserver.org");
                ub.Path = "/~magnus/catscan_rewrite.php";
                ub.Query = query;
                client.DownloadFile(ub.Uri, fileName);
            }
        }
    }
}
