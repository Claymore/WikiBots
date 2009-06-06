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

            WebClient client = new WebClient();
            client.DownloadFile("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang=ru&wikifam=.wikipedia.org&basecat=Ужасы&basedeep=5&mode=rc&hours=720&onlynew=on&go=Сканировать&format=csv&userlang=ru", "Cache\\data.txt");

            List<string> titles = new List<string>();
            using (TextWriter streamWriter = new StreamWriter("Cache\\output.txt"))
            using (TextReader streamReader = new StreamReader("Cache\\data.txt"))
            {
                int index = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == "0")
                    {
                        titles.Add(groups[1].Replace('_', ' '));
                        long ticks = 0;
                        long.TryParse(groups[4], out ticks);
                        DateTime date = DateTime.FromFileTimeUtc(ticks * 100);
                        streamWriter.WriteLine(string.Format("# [[{0}]]", groups[1].Replace('_', ' ')));
                        ++index;
                    }
                    if (index > 20)
                    {
                        break;
                    }
                }
            }

            using (TextReader sr = new StreamReader("Cache\\output.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SavePage("Портал:Хоррор/Новые статьи", text, "обновление");
            }
        }
    }
}
