using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Xml;
using Claymore.NewPagesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            try
            {
                if (!wiki.LoadCookies())
                {
                    wiki.Login(Settings.Default.Login, Settings.Default.Password);
                    wiki.CacheCookies();
                }
                else
                {
                    wiki.Login();
                    if (!wiki.IsBot)
                    {
                        wiki.Logout();
                        wiki.Login(Settings.Default.Login, Settings.Default.Password);
                        wiki.CacheCookies();
                    }
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            List<Portal> portals = new List<Portal>
            {
                new Portal("Религия", "Портал:Религия/Новые статьи", 35, "* [[{0}]]"),
                new Portal("Италия", "Портал:Италия/Новые статьи", 35, "* [[{0}]]"),
                new Portal("Христианство", "Портал:Христианство/Новые статьи", 25, "* [[{0}]]"),
                new Portal("Католицизм", "Портал:Католицизм/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Перу", "Портал:Перу/Новые статьи", 20, "# [[{0}]]"),
                new Portal("Шотландия", "Портал:Шотландия/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Уэльс", "Портал:Уэльс/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Эротика", "Портал:Эротика и порнография/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Греция", "Портал:Греция/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Награды", "Портал:Награды/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Лютеранство", "Портал:Лютеранство/Новые статьи", 15, "* [[{0}]]"),
                new Portal("Мексика", "Портал:Мексика/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Средние века", "Портал:Средневековье/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Одесса", "Портал:Одесса/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Психоактивные вещества", "Портал:Психоактивные субстанции/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Шотландия", "Портал:Шотландия/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Южная Америка", "Портал:Южная Америка/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Испания", "Портал:Испания/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Турция", "Портал:Турция/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Великобритания", "Портал:Великобритания/Новые статьи", 20, "* [[{0}]]"),
                new Portal("США", "Портал:США/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Эстония", "Портал:Эстония/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Генетика", "Портал:Генетика/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Франция", "Портал:Франция/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Каталония", "Портал:Каталония/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Камбоджа", "Портал:Камбоджа/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Таиланд", "Портал:Таиланд/Новые статьи", 20, "* [[{0}]]"),
                new Portal("Православие", "Портал:Православие/Новые статьи", 25, "# [[{0}]]"),
                new Portal("Тула", "Портал:Тула/Новые статьи", 20, "# [[{0}]]"),

                new Portal("Древний Восток", "Портал:Древний Восток/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", true, "d MMMM yyyy"),
                new Portal("Индеанистика", "Портал:Индейцы/Новые статьи", 20, "* [[{0}]] <small>— [[User:{1}|]] {2}</small>", true, "d MMMM yyyy"),
                new Portal("Доисторическая Европа", "Портал:Доисторическая Европа/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", true, "d MMMM yyyy"),
                new Portal("Лингвистика", "Портал:Лингвистика/Новые статьи", 30, "# [[{0}]] — [[User:{1}|]] {2}", true, "d MMMM yyyy"),
                new Portal("Ирландия", "Портал:Ирландия/Новые статьи", 20, "* '''{2}''' — [[{0}]]", true, "[[d MMMM]] [[yyyy]]"),
                new Portal("Монголия", "Портал:Монголия/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", true, "d MMMM yyyy"),
                new Portal("Индия", "Портал:Индия/Новые статьи", 10, "{{{{Новая статья|{0}|{2}|{1}}}}}", true, "d MMMM yyyy"),
                new Portal("Германия", "Портал:Германия/Новые статьи", 20, "* [[{0}]] <small>— {2}</small>", true, "d MMMM yyyy"),
                new Portal("Этнология", "Портал:Этнология/Новые статьи", 20, "* [[{0}]] — [[User:{1}|]] {2}", true, "d MMMM yyyy"),
                new Portal("Канада", "Портал:Канада/Новые статьи", 15, "# [[{0}]] <small>— {2} [[User:{1}|]]</small>", true, "dd.MM.yy"),
                new Portal("Сельское хозяйство", "Портал:Сельское хозяйство/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", true, "d MMMM yyyy"),
                new Portal("Финляндия", "Портал:Финляндия/Новые статьи", 35, "{{{{Новая статья|{0}|{2}|{1}}}}}", true, "d MMMM yyyy"),
                new Portal("Швеция", "Портал:Швеция/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", true, "d MMMM yyyy"),
                new Portal("Ужасы", "Портал:Хоррор/Новые статьи", 20, "# [[{0}]] — {2}", true, "d MMMM"),
                new Portal("Телевидение", "Портал:Телевидение/Новые статьи", 20, "# [[{0}]] — {2}", true, "d MMMM"),
            };

            for (int i = 0; i < portals.Count; ++i)
            {
                try
                {
                    GetData(portals[i], wiki);
                }
                catch (WebException e)
                {
                    Console.Out.WriteLine("Failed to fetch data for " + portals[i].Page + ": " + e.Message);
                    continue;
                }
                try
                {
                    UpdatePage(portals[i], wiki);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine("Failed to update " + portals[i].Page + ": " + e.Message);
                }
            }
        }

        static void GetData(Portal portal, Wiki wiki)
        {
            Console.Out.WriteLine("Downloading data for " + portal.Category);
            string url = string.Format("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang=ru&wikifam=.wikipedia.org&basecat={0}&basedeep=7&mode=rc&hours=720&onlynew=on&go=Сканировать&format=csv&userlang=ru",
                Uri.EscapeDataString(portal.Category));
            WebClient client = new WebClient();
            client.DownloadFile(url, "Cache\\input-" + portal.Category + ".txt");

            Console.Out.WriteLine("Processing data of " + portal.Category);
            using (TextWriter streamWriter = new StreamWriter("Cache\\output-" + portal.Category + ".txt"))
            using (TextReader streamReader = new StreamReader("Cache\\input-" + portal.Category + ".txt"))
            {
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("prop", "revisions");
                parameters.Add("rvprop", "timestamp|user");
                parameters.Add("rvdir", "newer");
                parameters.Add("rvlimit", "1");

                if (portal.GetAuthorInfo)
                {
                    Console.Out.WriteLine("Quering author information for " + portal.Category);
                }

                int index = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == "0")
                    {
                        string title = groups[1].Replace('_', ' ');
                        if (portal.GetAuthorInfo)
                        {
                            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                            XmlNode node = xml.SelectSingleNode("//rev");
                            string user = node.Attributes["user"].Value;
                            string timestamp = node.Attributes["timestamp"].Value;
                            DateTime time = DateTime.Parse(timestamp,
                                null,
                                DateTimeStyles.AssumeUniversal);
                            streamWriter.WriteLine(string.Format(portal.Format,
                                title, user, time.ToString(portal.TimeFormat)));
                        }
                        else
                        {
                            streamWriter.WriteLine(string.Format(portal.Format,
                                title));
                        }
                        ++index;
                    }
                    if (index >= portal.PageLimit)
                    {
                        break;
                    }
                }
            }
        }

        static void UpdatePage(Portal portal, Wiki wiki)
        {
            using (TextReader sr = new StreamReader("Cache\\output-" + portal.Category + ".txt"))
            {
                string text = sr.ReadToEnd();
                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Skipping " + portal.Page);
                    return;
                }
                Console.Out.WriteLine("Updating " + portal.Page);
                wiki.SavePage(portal.Page, text, "обновление");
            }
        }
    }

    internal class Portal
    {
        public string Page { get; private set; }
        public string Category { get; private set; }
        public string Format { get; private set; }
        public string TimeFormat { get; private set; }
        public int PageLimit { get; private set; }
        public bool GetAuthorInfo { get; private set; }

        public Portal(string category, string page, int pageLimit, string format)
            : this(category, page, pageLimit, format, false, null)
        {
        }

        public Portal(string category, string page, int pageLimit, string format, bool getAuthorInfo, string timeFormat)
        {
            Page = page;
            Category = category;
            PageLimit = pageLimit;
            Format = format;
            GetAuthorInfo = getAuthorInfo;
            TimeFormat = timeFormat;
        }
    }
}
