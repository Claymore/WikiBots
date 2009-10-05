using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.ArchiveWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return 0;
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
                return 0;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            string listText = wiki.LoadPage("Участник:ClaymoreBot/Архивация/Список");
            StringReader reader = new StringReader(listText);
            HashSet<string> pages = new HashSet<string>();
            Regex pageRE = new Regex(@"^\*\s*(.+)\s*$");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Match m = pageRE.Match(line);
                if (m.Success)
                {
                    pages.Add(m.Groups[1].Value);
                }
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "embeddedin");
            parameters.Add("geititle", "Участник:ClaymoreBot/Архивация");
            parameters.Add("geilimit", "max");
            parameters.Add("prop", "info");
            parameters.Add("intoken", "edit");
            parameters.Add("redirects");

            XmlDocument doc = wiki.Enumerate(parameters, true);
            foreach (XmlNode page in doc.SelectNodes("//page"))
            {
                string pageName = page.Attributes["title"].Value;
                string text = wiki.LoadPage(pageName);
                Archive archive;
                if (TryParse(pageName, text, pages.Contains(pageName), out archive))
                {
                    try
                    {
                        archive.Archivate(wiki);
                    }
                    catch (WikiException)
                    {
                    }
                }
            }
            return 0;
        }

        public static bool TryParse(string pageName,
                                    string text,
                                    bool allowSource,
                                    out Archive archive)
        {
            archive = null;
            Regex templateRE = new Regex(@"\{\{(Участник|User):ClaymoreBot/Архивация(.+?)\}\}",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match m = templateRE.Match(text);
            var values = new Dictionary<string, string>();
            if (m.Success)
            {
                string[] ps = m.Groups[2].Value.Split(new char[] { '|' });
                foreach (var p in ps)
                {
                    string[] keyvalue = p.Split(new char[] { '=' });
                    if (keyvalue.Length == 2)
                    {
                        values.Add(keyvalue[0].Trim().ToLower(), keyvalue[1].Trim());
                    }
                }
            }
            else
            {
                return false;
            }
            
            int days = 14;
            if (values.ContainsKey("срок"))
            {
                int.TryParse(values["срок"], out days);
            }
            
            int archiveSize = 70 * 1024;
            if (values.ContainsKey("размер архива"))
            {
                int.TryParse(values["размер архива"], out archiveSize);
            }

            bool checkForResult = false;
            if (values.ContainsKey("итог"))
            {
                string value = values["итог"].ToLower();
                if (value == "да")
                {
                    checkForResult = true;
                }
                else
                {
                    checkForResult = true;
                }
            }

            string page = pageName;
            if (allowSource && values.ContainsKey("обрабатывать"))
            {
                page = values["обрабатывать"];
            }

            string format = pageName + "/Архив/%(номер)";
            if (values.ContainsKey("формат"))
            {
                format = page + "/" + values["формат"];
            }

            if (values.ContainsKey("страница"))
            {
                format = page + "/" + values["страница"];
            }

            if (allowSource && values.ContainsKey("абсолютный путь"))
            {
                format = values["абсолютный путь"];
            }

            int topics = 0;
            if (values.ContainsKey("тем в архиве"))
            {
                int.TryParse(values["тем в архиве"], out topics);
            }
            bool newSectionsDown = true;
            if (values.ContainsKey("новые"))
            {
                if (values["новые"].ToLower() == "сверху")
                {
                    newSectionsDown = false;
                }
            }
            if (values.ContainsKey("тип"))
            {
                string t = values["тип"].ToLower();
                if (t == "страница")
                {
                    archive = new Archive(page, days, format, checkForResult, newSectionsDown);
                }
                else if (t == "месяц")
                {
                    archive = new ArchiveByMonth(page, days, format, checkForResult, newSectionsDown);
                }
                else if (t == "год")
                {
                    archive = new ArchiveByYear(page, days, format, checkForResult, newSectionsDown);
                }
                else if (t == "полгода")
                {
                    archive = new ArchiveByHalfYear(page, days, format, checkForResult, newSectionsDown);
                }
                else if (t == "статьи для рецензирования")
                {
                    archive = new ReviewArchive(page, days, format);
                }
                else if (t == "нумерация" && topics > 0)
                {
                    archive = new ArchiveByTopicNumber(page, days, format, checkForResult, newSectionsDown, topics);
                }
            }
            if (archive != null)
            {
                if (values.ContainsKey("убирать ссылки") &&
                        values["убирать ссылки"].ToLower() == "да")
                {
                    archive.Processor = RemoveHttp;
                }
                return true;
            }
            return false;
        }

        private static string RemoveHttp(string title)
        {
            return title.Replace("http://", "");
        }
    }
}
