using System;
using System.Xml;
using Claymore.NullEditWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NullEditWikiBot
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
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "embeddedin");
            parameters.Add("geititle", "Шаблон:Deleteslow");
            parameters.Add("geilimit", "max");
            parameters.Add("geinamespace", "0");
            parameters.Add("prop", "categories");
            parameters.Add("clcategories", "Категория:Википедия:К быстрому удалению");

            XmlDocument doc;
            try
            {
                doc = wiki.Enumerate(parameters, true);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            
            XmlNodeList pages = doc.SelectNodes("//page");
            int index = 1;
            foreach (XmlNode page in pages)
            {
                XmlNode category = page.SelectSingleNode("categories/cl");
                if (category != null)
                {
                    continue;
                }
                string pageTitle = page.Attributes["title"].Value;
                Console.Out.WriteLine(string.Format("Processing '{0}' ({1}/{2})...",
                    pageTitle, index++, pages.Count));
                try
                {
                    wiki.AppendTextToPage(pageTitle,
                        "\n\n",
                        "Сброс кеша нулевой правкой",
                        MinorFlags.None,
                        WatchFlags.Watch);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine(e.Message);
                }
            }
            Console.Out.WriteLine("Done.");
        }
    }
}
