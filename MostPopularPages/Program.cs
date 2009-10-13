using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using MostPopularPages.Properties;

namespace MostPopularPages
{
    class Program
    {
        static void Main(string[] args)
        {
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }

            Directory.CreateDirectory("input");
            Directory.CreateDirectory("output");
            
            WebClient client = new WebClient();
            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            try
            {
                wiki.Login(Settings.Default.Login, Settings.Default.Password);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");
            DownloadResources(client, wiki);
            Console.Out.WriteLine("Processing tables...");
            Dictionary<string, PageInfo> oldPages = ParseOldPages("input/Популярные в прошлом.txt");
            Dictionary<string, PageInfo> oldPagesCopy = new Dictionary<string, PageInfo>(oldPages);
            Dictionary<string, PageInfo> currentPages = ParseStatistics("input/Monthly wiki page Hits for ru.wikipedia.htm");
            string post, pre;
            Dictionary<string, PageInfo> tablePages = ParseTable("input/Популярные статьи.txt", out pre, out post);
            List<string> titles = new List<string>(currentPages.Values.
                Select(p => p.Title).Concat(tablePages.Values.Select(p => p.Title)));
            Console.Out.WriteLine("Finished processing tables.");
            Console.Out.WriteLine("Quering...");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "flagged");
            parameters.Add("redirects");
            XmlDocument doc;
            try
            {
                doc = wiki.Query(QueryBy.Titles, parameters, titles.Distinct());
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Finished quering.");
            Console.Out.WriteLine("Processing data...");
            
            XmlNodeList nodes = doc.SelectNodes("//page");
            foreach (XmlNode node in nodes)
            {
                string key = node.Attributes["title"].Value;
                if (node.Attributes["missing"] != null && currentPages.ContainsKey(key))
                {
                    currentPages.Remove(key);
                }
            }
            XmlNodeList redirects = doc.SelectNodes("//r");
            foreach (XmlNode node in redirects)
            {
                string key = node.Attributes["from"].Value;
                if (currentPages.ContainsKey(key))
                {
                    PageInfo page = currentPages[key];
                    string redirectsTo = node.Attributes["to"].Value;
                    if (!currentPages.ContainsKey(redirectsTo))
                    {
                        page.Title = redirectsTo;
                        currentPages.Remove(key);
                        currentPages.Add(redirectsTo, page);
                    }
                    else
                    {
                        PageInfo info = currentPages[redirectsTo];
                        info.Hits += page.Hits;
                        info.Percent += page.Percent;
                        currentPages[redirectsTo] = info;
                        currentPages.Remove(key);
                    }
                }
            }

            foreach (XmlNode node in redirects)
            {
                string key = node.Attributes["from"].Value;
                if (tablePages.ContainsKey(key))
                {
                    PageInfo page = tablePages[key];
                    page.IsRedirect = true;
                    string redirectsTo = node.Attributes["to"].Value;
                    if (!currentPages.ContainsKey(redirectsTo))
                    {
                        List<string> pats = new List<string>(page.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                        if (pats.Count > 0)
                        {
                            if (!oldPages.ContainsKey(page.Title))
                            {
                                oldPages.Add(page.Title, page);
                            }
                        }
                    }
                    else
                    {
                        PageInfo info = currentPages[redirectsTo];
                        List<string> pats = new List<string>(page.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                        pats.AddRange(info.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                        pats = new List<string>(pats.Distinct());
                        if (pats.Count > 0)
                        {
                            info.Status = "{{done}}";
                            pats.CopyTo(0, info.Patrollers, 0, pats.Count < 3 ? pats.Count : 3);
                        }
                        currentPages[redirectsTo] = info;
                    }
                    tablePages[key] = page;
                }
            }

            foreach (PageInfo page in tablePages.Values.Where(p => !p.IsRedirect))
            {
                if (currentPages.ContainsKey(page.Title))
                {
                    PageInfo info = currentPages[page.Title];
                    info.Patrollers = page.Patrollers;
                    info.Status = page.Status;
                    currentPages[page.Title] = info;
                }
                else
                {
                    List<string> pats = new List<string>(page.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                    if (pats.Count > 0)
                    {
                        if (!oldPages.ContainsKey(page.Title))
                        {
                            oldPages.Add(page.Title, page);
                        }
                    }
                }
            }

            List<string> keysForDeletion = new List<string>();
            foreach (PageInfo oldPage in oldPages.Values)
            {
                if (currentPages.ContainsKey(oldPage.Title))
                {
                    PageInfo page = currentPages[oldPage.Title];
                    List<string> pats = new List<string>(page.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                    pats.AddRange(oldPage.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                    pats = new List<string>(pats.Distinct());
                    if (pats.Count > 0)
                    {
                        page.Status = "{{done}}";
                        pats.CopyTo(0, page.Patrollers, 0, pats.Count < 3 ? pats.Count : 3);
                    }
                    keysForDeletion.Add(oldPage.Title);
                    currentPages[oldPage.Title] = page;
                }
            }

            keysForDeletion.ForEach(s => oldPages.Remove(s));

            XmlNodeList patrolledNodes = doc.SelectNodes("/api/query/pages/page/flagged");
            foreach (XmlNode node in patrolledNodes)
            {
                string key = node.ParentNode.Attributes["title"].Value;
                if (currentPages.ContainsKey(key))
                {
                    PageInfo page = currentPages[key];
                    page.Patrolled = true;
                    if (node.Attributes["pending_since"] != null)
                    {
                        page.Pending = true;
                        page.PendingSince = DateTime.Parse(node.Attributes["pending_since"].Value, null, DateTimeStyles.AssumeUniversal);
                    }
                    currentPages[key] = page;
                }
            }
            Console.Out.WriteLine("Finished processing data.");
            Console.Out.WriteLine("Generating output...");
            using (TextWriter streamWriter =
                  new StreamWriter("output/Популярные статьи.txt", false))
            {
                streamWriter.WriteLine(pre);
                streamWriter.WriteLine("== Данные ==");
                streamWriter.Write("Всего " + Articles(currentPages.Values.Count));
                streamWriter.Write(", из них " + Articles(currentPages.Values.Count(p => !string.IsNullOrEmpty(p.Status))) + " под наблюдением");
                streamWriter.Write(", " + Articles(currentPages.Values.Count(p => p.Patrolled)) + " прошли первичное патрулирование");
                streamWriter.Write(" и " + Articles(currentPages.Values.Count(p => p.Pending && (DateTime.Now - p.PendingSince).Days >= 14)) + " с лагом патрулирования более двух недель.\r\n");

                PrintTable(streamWriter, "Более 2000 хитов", currentPages.Values.Where(p => p.Hits >= 2000));
                PrintTable(streamWriter, "1400—1999 хитов", currentPages.Values.Where(p => p.Hits >= 1400 && p.Hits < 2000));
                PrintTable(streamWriter, "1100—1399 хитов", currentPages.Values.Where(p => p.Hits >= 1100 && p.Hits < 1400));
                PrintTable(streamWriter, "1000—1099 хитов", currentPages.Values.Where(p => p.Hits >= 1000 && p.Hits < 1100));
                PrintTable(streamWriter, "900—999 хитов", currentPages.Values.Where(p => p.Hits >= 900 && p.Hits < 1000));
                PrintTable(streamWriter, "800—899 хитов", currentPages.Values.Where(p => p.Hits >= 800 && p.Hits < 900));
                PrintTable(streamWriter, "700—799 хитов", currentPages.Values.Where(p => p.Hits >= 700 && p.Hits < 800));
                PrintTable(streamWriter, "600—699 хитов", currentPages.Values.Where(p => p.Hits >= 600 && p.Hits < 700));
                PrintTable(streamWriter, "500—599 хитов", currentPages.Values.Where(p => p.Hits >= 500 && p.Hits < 600));
                PrintTable(streamWriter, "Менее 500 хитов", currentPages.Values.Where(p => p.Hits < 500));
                streamWriter.WriteLine(post);
            }

            using (TextWriter streamWriter =
                   new StreamWriter("output/Популярные в прошлом.txt", false))
            {
                streamWriter.WriteLine("На этой странице собраны статьи, за которыми следят патрулирующие, но которые исчезли из [[Википедия:Проект:Патрулирование/Популярные статьи|списка популярных статей]] за последний месяц.\n");
                if (oldPages.Count > 0)
                {
                    streamWriter.WriteLine("== Данные ==");
                    streamWriter.WriteLine("{| class=\"sortable wikitable\" width=\"100%\"");
                    streamWriter.WriteLine("! style=\"background:#efefef\" | Название");
                    streamWriter.WriteLine("! style=\"background:#efefef\" | Патрулирование");
                    streamWriter.WriteLine("! style=\"background:#efefef\" | Первый патрулирующий");
                    streamWriter.WriteLine("! style=\"background:#efefef\" | Второй патрулирующий");
                    streamWriter.WriteLine("! style=\"background:#efefef\" | Третий патрулирующий");
                    foreach (PageInfo page in oldPages.Values)
                    {
                        PrintPage(streamWriter, page);
                    }
                    streamWriter.WriteLine("|}\n");
                }
                streamWriter.WriteLine("[[Категория:Википедия:Патрулирование]]");
            }
            Console.Out.WriteLine("Output is ready.");
            try
            {
                UploadResults(wiki, !AreTablesEqual(currentPages, tablePages),
                    !AreOldPagesEqual(oldPagesCopy, oldPages));
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
            }
            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        static bool AreTablesEqual(Dictionary<string, PageInfo> a, Dictionary<string, PageInfo> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            foreach (PageInfo page in a.Values)
            {
                if (!b.ContainsKey(page.Title))
                {
                    return false;
                }
                PageInfo info = b[page.Title];
                if (info.Hits != page.Hits ||
                    info.Patrolled != page.Patrolled)
                {
                    return false;
                }
                bool pending = page.Pending && (DateTime.Now - page.PendingSince).Days >= 14;
                if (info.Pending != pending)
                {
                    return false;
                }
            }
            return true;
        }

        static bool AreOldPagesEqual(Dictionary<string, PageInfo> a, Dictionary<string, PageInfo> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            foreach (PageInfo page in a.Values)
            {
                if (!b.ContainsKey(page.Title))
                {
                    return false;
                }
            }
            return true;
        }

        static string Articles(int number)
        {
            bool exception = (number % 100) / 10 == 1;
            int digit = number % 10;
            if (digit == 1)
            {
                return string.Format("{0} статья", number);
            }
            else if (!exception && digit > 1 && digit < 5)
            {
                return string.Format("{0} статьи", number);
            }
            else
            {
                return string.Format("{0} статей", number);
            }
        }

        static string Hours(int number)
        {
            bool exception = (number % 100) / 10 == 1;
            int digit = number % 10;
            if (digit == 1)
            {
                return string.Format("{0} час", number);
            }
            else if (!exception && digit > 1 && digit < 5)
            {
                return string.Format("{0} часа", number);
            }
            else
            {
                return string.Format("{0} часов", number);
            }
        }

        static void DownloadResources(WebClient client, Wiki wiki)
        {
            Console.Out.WriteLine("Downloading resources...");
            client.DownloadFile("http://wikistics.falsikon.de/latest/wikipedia/ru/", "input/Monthly wiki page Hits for ru.wikipedia.htm");
            using (TextWriter streamWriter =
                  new StreamWriter("input/Популярные статьи.txt", false))
            {
                streamWriter.Write(wiki.LoadText("Википедия:Проект:Патрулирование/Популярные статьи"));
            }

            using (TextWriter streamWriter =
                  new StreamWriter("input/Популярные в прошлом.txt", false))
            {
                streamWriter.Write(wiki.LoadText("Википедия:Проект:Патрулирование/Популярные в прошлом"));
            }
            Console.Out.WriteLine("Finished downloading.");
        }

        static void UploadResults(Wiki wiki, bool uploadPages, bool uploadOldPages)
        {
            Console.Out.WriteLine("Uploading results...");
            string comment = "обновление";
            if (uploadPages)
            {
                using (TextReader sr =
                            new StreamReader("output/Популярные статьи.txt"))
                {
                    string text = sr.ReadToEnd();
                    wiki.Save("Википедия:Проект:Патрулирование/Популярные статьи",
                        text, comment);
                }
            }
            if (uploadOldPages)
            {
                using (TextReader sr =
                            new StreamReader("output/Популярные в прошлом.txt"))
                {
                    string text = sr.ReadToEnd();
                    wiki.Save("Википедия:Проект:Патрулирование/Популярные в прошлом",
                        text, comment);
                }
            }
            Console.Out.WriteLine("Upload finished.");
        }

        static Dictionary<string, PageInfo> ParseTable(string filename, out string pre, out string post)
        {
            List<string> pretext = new List<string>();
            List<string> posttext = new List<string>();
            Regex tableRE = new Regex(@"\[\[(.+)\]\] ?<?");
            bool tableBegin = false;
            Dictionary<string, PageInfo> pages = new Dictionary<string, PageInfo>();

            using (TextReader streamReader =
                new StreamReader(filename))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (!tableBegin)
                    {
                        if (line == "{| class=\"sortable wikitable\" width=\"100%\"")
                        {
                            tableBegin = true;
                        }
                        else if (line.StartsWith("[[Категория:"))
                        {
                            posttext.Add(line);
                        }
                        else if (line == "== Данные ==")
                        {
                            line = streamReader.ReadLine();
                            continue;
                        }
                        else if (!line.StartsWith("=="))
                        {
                            if (string.IsNullOrEmpty(line) && pretext.Count > 0 && string.IsNullOrEmpty(pretext[pretext.Count - 1]))
                            {
                                continue;
                            }
                            pretext.Add(line);
                        }
                    }
                    else
                    {
                        if (line.StartsWith("|- align=\"center\""))
                        {
                            PageInfo page = new PageInfo();
                            page.Hits = int.Parse(streamReader.ReadLine().Substring(1));
                            if (!double.TryParse(streamReader.ReadLine().Substring(1), out page.Percent))
                            {
                                page.Percent = 0;
                            }
                            string title = streamReader.ReadLine();
                            int index = title.IndexOf("<!--");
                            if (index != -1)
                            {
                                title = title.Remove(index);
                            }
                            Match m = tableRE.Match(title);
                            page.IsRedirect = false;
                            page.Patrolled = true;
                            page.Pending = false;
                            page.RedirectsTo = "";
                            page.Title = m.Groups[1].Value;
                            page.Status = streamReader.ReadLine().Substring(1);
                            page.Patrollers = new string[3];
                            page.Patrollers[0] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());
                            page.Patrollers[1] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());
                            page.Patrollers[2] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());

                            if (line.Contains("#FFE8E9"))
                            {
                                page.Patrolled = false;
                            }
                            else if (line.Contains("#FFFDE8"))
                            {
                                page.Pending = true;
                            }

                            if (pages.ContainsKey(page.Title))
                            {
                                PageInfo info = pages[page.Title];
                                info.Hits += page.Hits;
                                info.Percent += page.Percent;
                                List<string> pats = new List<string>(page.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                                pats.AddRange(info.Patrollers.Where(s => !string.IsNullOrEmpty(s)));
                                pats = new List<string>(pats.Distinct());
                                if (pats.Count > 0)
                                {
                                    page.Status = "{{done}}";
                                    pats.CopyTo(0, info.Patrollers, 0, pats.Count < 3 ? pats.Count : 3);
                                }
                                if (page.Patrolled)
                                {
                                    info.Patrolled = true;
                                }
                                if (page.Pending)
                                {
                                    info.Pending = true;
                                }
                                pages[page.Title] = info;
                            }
                            else
                            {
                                pages.Add(page.Title, page);
                            }
                        }
                        else if (line == "|}")
                        {
                            tableBegin = false;
                        }
                    }
                }
            }
            StringBuilder sb = new StringBuilder();
            pretext.ForEach(s => sb.Append("\n" + s));
            sb.Remove(0, 1);
            pre = sb.ToString();
            sb = new StringBuilder();
            posttext.ForEach(s => sb.Append("\n" + s));
            sb.Remove(0, 1);
            post = sb.ToString();
            return pages;
        }

        static Dictionary<string, PageInfo> ParseOldPages(string filename)
        {
            Dictionary<string, PageInfo> oldPages = new Dictionary<string, PageInfo>();
            Regex tableRE = new Regex(@"\[\[(.+)\]\] ?<?");
            bool tableBegin = false;

            using (TextReader streamReader =
                new StreamReader(filename))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (!tableBegin)
                    {
                        if (line == "{| class=\"sortable wikitable\" width=\"100%\"")
                        {
                            tableBegin = true;
                        }
                    }
                    else
                    {
                        if (line == "|- align=\"center\"")
                        {
                            PageInfo page = new PageInfo();
                            string title = streamReader.ReadLine();
                            Match m = tableRE.Match(title);
                            page.Title = m.Groups[1].Value;
                            page.Status = streamReader.ReadLine().Substring(1);
                            page.Patrollers = new string[3];
                            page.Patrollers[0] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());
                            page.Patrollers[1] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());
                            page.Patrollers[2] = ExtractUserName(streamReader.ReadLine().Substring(1).Trim());
                            if (!oldPages.ContainsKey(page.Title))
                            {
                                oldPages.Add(page.Title, page);
                            }
                        }
                    }
                }
            }
            return oldPages;
        }

        static string ExtractUserName(string patroller)
        {
            string result = "";
            int index = patroller.IndexOf("[[User:");
            if (index == -1)
            {
                index = patroller.IndexOf("[[user:");
            }
            if (index != -1)
            {
                int startIndex = patroller.IndexOf('|', index);
                int endIndex = patroller.IndexOf(']', index);
                if (startIndex != -1)
                {
                    result = patroller.Substring(index + 7,
                        startIndex - index - 7);
                }
                else
                {
                    result = patroller.Substring(index + 7,
                        endIndex - index - 7);
                }
            }
            else
            {
                index = patroller.IndexOf("[[Участник:");
                if (index == -1)
                {
                    index = patroller.IndexOf("[[участник:");
                }
                if (index != -1)
                {
                    int startIndex = patroller.IndexOf('|', index);
                    int endIndex = patroller.IndexOf(']', index);
                    if (startIndex != -1)
                    {
                        result = patroller.Substring(index + 11,
                            startIndex - index - 11);
                    }
                    else
                    {
                        result = patroller.Substring(index + 11,
                            endIndex - index - 11);
                    }
                }
            }
            if (!string.IsNullOrEmpty(result))
            {
                return "[[User:" + result + "|" + result + "]]";
            }
            return patroller;
        }

        private static int ComparePages(PageInfo x, PageInfo y)
        {
            int result = y.Hits.CompareTo(x.Hits);
            if (result == 0)
            {
                CompareInfo info = CompareInfo.GetCompareInfo("ru-RU");
                return info.Compare(x.Title, y.Title);
            }
            return result;
        }

        private static void PrintTable(TextWriter streamWriter, string title, IEnumerable<PageInfo> list)
        {
            List<PageInfo> values = new List<PageInfo>(list);
            values.Sort(ComparePages);
            streamWriter.WriteLine("=== " + title + " ===");
            streamWriter.WriteLine("{| class=\"sortable wikitable\" width=\"100%\"");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Хитов");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Процент");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Название");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Патрулирование");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Первый патрулирующий");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Второй патрулирующий");
            streamWriter.WriteLine("! style=\"background:#efefef\" | Третий патрулирующий");
            foreach (PageInfo page in values)
            {
                string style = "|- align=\"center\"";
                if (!page.Patrolled)
                {
                    style += " bgcolor=#FFE8E9";
                }
                if (page.Pending)
                {
                    TimeSpan offset = DateTime.Now - page.PendingSince;
                    if (offset.Days >= 14)
                    {
                        style += " bgcolor=#FFFDE8";
                    }
                }
                streamWriter.WriteLine(style);
                streamWriter.WriteLine("|" + page.Hits);
                streamWriter.WriteLine(string.Format("|{0:N2}", page.Percent));
                streamWriter.WriteLine("|[[" + page.Title + "]]");
                streamWriter.WriteLine("|" + page.Status);
                PrintUser(streamWriter, page.Patrollers[0]);
                PrintUser(streamWriter, page.Patrollers[1]);
                PrintUser(streamWriter, page.Patrollers[2]);
            }
            streamWriter.WriteLine("|}\n");
        }

        private static void PrintUser(TextWriter streamWriter, string user)
        {
            streamWriter.WriteLine("|" + user);
        }

        private static void PrintPage(TextWriter streamWriter, PageInfo page)
        {
            streamWriter.WriteLine("|- align=\"center\"");
            streamWriter.WriteLine("|[[" + page.Title + "]]");
            streamWriter.WriteLine("|" + page.Status);
            PrintUser(streamWriter, page.Patrollers[0]);
            PrintUser(streamWriter, page.Patrollers[1]);
            PrintUser(streamWriter, page.Patrollers[2]);
        }

        static Dictionary<string, PageInfo> ParseStatistics(string filename)
        {
            Dictionary<string, PageInfo> pages = new Dictionary<string, PageInfo>();
            string[] exceptions = new string[] { "Main Page",
                "Заглавная страница",
                "ÐÐ°Ð³Ð»Ð°Ð²Ð½Ð°Ñ ÑÑÑÐ°Ð½Ð¸ÑÐ°",
                "%s" };
            Regex re = new Regex(@"<li.*><b.+>([,0-9]+)</b> \[.+<small>(.+) %</small>\]: <a href=.+><b>(.+)</b></a>.*</li>");
            using (TextReader streamReader =
                new StreamReader(filename))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    Match m = re.Match(line);
                    if (!m.Success)
                    {
                        continue;
                    }
                    PageInfo pageInfo = new PageInfo();
                    pageInfo.Title = m.Groups[3].Value;
                    if (exceptions.Any(s => pageInfo.Title == s))
                    {
                        continue;
                    }
                    pageInfo.Hits = int.Parse(m.Groups[1].Value.Replace(",", ""));
                    if (!double.TryParse(m.Groups[2].Value.Replace(".", ","), out pageInfo.Percent))
                    {
                        pageInfo.Percent = 0;
                    }
                    pageInfo.Patrollers = new string[3];
                    pageInfo.Patrolled = false;
                    pageInfo.Pending = false;
                    pages.Add(pageInfo.Title, pageInfo);
                }
            }
            return pages;
        }

        struct PageInfo
        {
            public string Title;
            public double Percent;
            public int Hits;
            public string[] Patrollers;
            public string Status;
            public bool IsRedirect;
            public string RedirectsTo;
            public bool Patrolled;
            public bool Pending;
            public DateTime PendingSince;
        }
    }
}
