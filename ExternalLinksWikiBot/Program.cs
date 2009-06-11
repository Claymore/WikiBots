using System;
using Claymore.ExternalLinksWikiBot.Properties;
using Claymore.SharpMediaWiki;
using System.Xml;
using System.IO;

namespace ExternalLinksWikiBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 10;
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

            //string offset = "";
            //using (TextWriter streamWriter = new StreamWriter("data.txt"))
            //{
            //    while (true)
            //    {
            //        ParameterCollection parameters = new ParameterCollection();
            //        parameters.Add("list", "exturlusage");
            //        parameters.Add("euquery", "ru.wikipedia.org");
            //        parameters.Add("eunamespace", "0");
            //        parameters.Add("eulimit", "max");
            //        if (!string.IsNullOrEmpty(offset))
            //        {
            //            parameters.Add("euoffset", offset);
            //        }

            //        XmlDocument xml = null;
            //        for (int i = 0; i < 3; ++i)
            //        {
            //            try
            //            {
            //                xml = wiki.Enumerate(parameters, false);
            //                break;
            //            }
            //            catch (WikiException)
            //            {
            //            }
            //        }
            //        if (xml == null)
            //        {
            //            break;
            //        }
            //        foreach (XmlNode node in xml.SelectNodes("//eu"))
            //        {
            //            string url = node.Attributes["url"].Value;
            //            string title = node.Attributes["title"].Value;
            //            if (!url.Contains("&action=edit") &&
            //                !url.Contains("&action=history") &&
            //                !url.Contains("&diff=0") &&
            //                !url.Contains("action=delete") &&
            //                !url.Contains("&redirect=no") &&
            //                !url.Contains(":Log&page=") &&
            //                !url.Contains("Special:Whatlinkshere"))
            //            {
            //                streamWriter.WriteLine(string.Format("{0}\t{1}",
            //                    title,
            //                    url));
            //            }
            //        }
            //        if (xml.SelectSingleNode("//query-continue") == null)
            //        {
            //            break;
            //        }
            //        else
            //        {
            //            offset = xml.SelectSingleNode("//exturlusage[@euoffset]").Attributes["euoffset"].Value;
            //        }
            //    }
            //}

            long index = 0;
            using (TextWriter streamWriter = new StreamWriter("processed-data.txt"))
            using (TextReader sr = new StreamReader("data.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split(new char[] { '\t' });
                    string url = Uri.UnescapeDataString(parts[1]);
                    string title = parts[0];
                    if (!url.Contains(":Search?search=") &&
                        !url.Contains("?action=") &&
                        !url.Contains("&from=") &&
                        !url.StartsWith("http://ru.wikipedia.org/wiki/Обсуждение_шаблона:") &&
                        !url.StartsWith("http://ru.wikipedia.org/wiki/Шаблон:"))
                    {
                        streamWriter.WriteLine(string.Format("{0}\t{1}", title, url));
                    }
                }
            }
            //Console.Out.WriteLine(index);
        }
    }
}
