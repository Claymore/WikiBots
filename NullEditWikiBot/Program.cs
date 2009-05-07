using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Claymore.NullEditWikiBot.Properties;
using Claymore.SharpMediaWiki;
using System.Xml;

namespace Claymore.NullEditWikiBot
{
    class Program
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
            wiki.Login(Settings.Default.Login, Settings.Default.Password);
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Deleteslow");
            parameters.Add("eilimit", "max");
            parameters.Add("einamespace", "0");
            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList pages = doc.SelectNodes("/api/query/embeddedin/ei");
            int index = 0;
            wiki.SleepBetweenEdits = 10;
            foreach (XmlNode page in pages)
            {
                string pageTitle = page.Attributes["title"].Value;
                Console.Out.WriteLine(string.Format("Processing '{0}' ({1}/{2})...",
                    pageTitle, index++, pages.Count));
                try
                {
                    wiki.SavePage(pageTitle, "", "\n\n", "Сброс кеша нулевой правкой",
                        MinorFlags.None, CreateFlags.NoCreate, WatchFlags.Watch, SaveFlags.Append);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine(string.Format("Caught error: {0}."), e.Message);
                }
            }
            Console.Out.WriteLine("Done.");
            wiki.Logout();
        }
    }
}
