using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Claymore.NewPagesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
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

            List<Portal> portals = new List<Portal>();
            portals.Add(new Portal("Ужасы", "Портал:Хоррор/Новые статьи", 20, "# [[{0}]]"));
            portals.Add(new Portal("Эротика", "Портал:Эротика и порнография/Новые статьи", 20, "* [[{0}]]"));

            for (int i = 0; i < portals.Count; ++i)
            {
                GetData(portals[i]);
                UpdatePage(portals[i], wiki);
            }
        }

        static void GetData(Portal portal)
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
                int index = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == "0")
                    {
                        streamWriter.WriteLine(string.Format(portal.Format, groups[1].Replace('_', ' ')));
                        ++index;
                    }
                    if (index > portal.PageLimit)
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
                Console.Out.WriteLine("Updating " + portal.Page);
                wiki.SavePage(portal.Page, text, "обновление");
            }
        }
    }

    internal class Portal
    {
        private string _page;
        private string _category;
        private int _pageLimit;
        private string _format;

        public Portal(string category, string page, int pageLimit, string format)
        {
            _page = page;
            _category = category;
            _pageLimit = pageLimit;
            _format = format;
        }

        public string Page
        {
            get { return _page; }
        }

        public string Category
        {
            get { return _category; }
        }

        public string Format
        {
            get { return _format; }
        }

        public int PageLimit
        {
            get { return _pageLimit; }
        }
    }
}
