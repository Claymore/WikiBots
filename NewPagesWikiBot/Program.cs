using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.NewPagesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Out.WriteLine("NewPagesWikiBot <language> <update comment>");
                return 0;
            }
            Directory.CreateDirectory(@"Cache\" + args[0]);

            Wiki wiki = new Wiki(string.Format("http://{0}.wikipedia.org/", args[0]));
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return 0;
            }

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            try
            {
                string cookieFile = @"Cache\"+ args[0] + @"\cookie.jar";
                if (!wiki.LoadCookies(cookieFile))
                {
                    wiki.Login(Settings.Default.Login, Settings.Default.Password);
                    wiki.CacheCookies(cookieFile);
                }
                else
                {
                    wiki.Login();
                    if (!wiki.IsBot)
                    {
                        wiki.Logout();
                        wiki.Login(Settings.Default.Login, Settings.Default.Password);
                        wiki.CacheCookies(cookieFile);
                    }
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return 0;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            PortalModule portal = new PortalModule(args[0], args[1]);
            Directory.CreateDirectory("Cache\\" + args[0] + "\\NewPages\\");

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "embeddedin");
            parameters.Add("geititle", "User:ClaymoreBot/Новые статьи");
            parameters.Add("geilimit", "max");
            parameters.Add("prop", "info");
            parameters.Add("intoken", "edit");
            parameters.Add("redirects");

            string path = @"Cache\" + args[0] + @"\";
            Directory.CreateDirectory(path);

            List<string> pages = new List<string>();
            XmlDocument doc = wiki.Enumerate(parameters, true);
            foreach (XmlNode node in doc.SelectNodes("//page"))
            {
                string title = node.Attributes["title"].Value;
                pages.Add(title);
            }

            pages.Sort();

            for (int i = 0; i < pages.Count; ++i)
            {
                WikiPage page = Cache.Load(wiki, pages[i], path);
                IPortalModule module;
                if (TryParse(page, path, portal, out module))
                {
                    try
                    {
                        module.Update(wiki);
                    }
                    catch (WikiException)
                    {
                    }
                    catch (System.Net.WebException)
                    {
                    }
                }
            }

            Console.Out.WriteLine("Done.");
            return 0;
        }

        private static bool TryParseTemplate(string text, out Dictionary<string, string> parameters)
        {
            parameters = null;
            Regex templateRE = new Regex(@"\{\{(User):ClaymoreBot/Новые стать(и).",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match m = templateRE.Match(text);
            if (!m.Success)
            {
                return false;
            }
            int index = 1;
            int begin = m.Groups[2].Index + 1;
            int end = -1;
            for (int i = begin; i < text.Length - 1; ++i)
            {
                if (text[i] == '{' && text[i + 1] == '{')
                {
                    ++index;
                }
                else if (text[i] == '}' && text[i + 1] == '}')
                {
                    --index;
                    if (index == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end == -1)
            {
                return false;
            }

            parameters = new Dictionary<string, string>();
            string parameterString = text.Substring(begin, end - begin);
            string[] ps = parameterString.Split(new char[] { '|' });
            string lastKey = "";
            foreach (var p in ps)
            {
                string[] keyvalue = p.Split(new char[] { '=' });
                if (keyvalue.Length == 2)
                {
                    parameters.Add(keyvalue[0].Trim().ToLower(), keyvalue[1].Trim());
                    lastKey = keyvalue[0].Trim().ToLower();
                }
                else if (keyvalue.Length == 1)
                {
                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        parameters[lastKey] = parameters[lastKey] + "|" + keyvalue[0].Trim();
                    }
                }
            }
            return true;
        }

        public static bool TryParse(WikiPage page,
                                    string directory,
                                    PortalModule portal,
                                    out IPortalModule module)
        {
            module = null;
            Dictionary<string, string> options;
            if (!TryParseTemplate(page.Text, out options))
            {
                return false;
            }

            var categories = new List<string>();
            if (options.ContainsKey("категория"))
            {
                categories.Add(options["категория"]);
            }

            if (options.ContainsKey("категории"))
            {
                string[] separators;
                if (options["категории"].Contains("\""))
                {
                    separators = new string[] { "\"," };
                }
                else
                {
                    separators = new string[] { "," };
                }
                string[] cats = options["категории"].Split(separators,
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < cats.Length; ++i)
                {
                    string cat = cats[i].Replace("\"", "").Trim();
                    if (!string.IsNullOrEmpty(cat))
                    {
                        categories.Add(cat);
                    }
                }
            }

            var categoriesToIgnore = new List<string>();
            if (options.ContainsKey("игнорировать"))
            {
                string[] separators;
                if (options["игнорировать"].Contains("\""))
                {
                    separators = new string[] { "\"," };
                }
                else
                {
                    separators = new string[] { "," };
                }
                string[] cats = options["игнорировать"].Split(separators,
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < cats.Length; ++i)
                {
                    string cat = cats[i].Replace("\"", "").Trim();
                    if (!string.IsNullOrEmpty(cat))
                    {
                        categoriesToIgnore.Add(cat);
                    }
                }
            }

            string title = "";
            if (options.ContainsKey("страница"))
            {
                title = options["страница"];
            }

            string archive = "";
            if (options.ContainsKey("архив"))
            {
                archive = options["архив"];
            }

            string header = "";
            if (options.ContainsKey("шапка"))
            {
                header = options["шапка"];
            }

            string footer = "";
            if (options.ContainsKey("подвал"))
            {
                footer = options["подвал"];
            }

            string format = "";
            if (options.ContainsKey("формат элемента"))
            {
                format = options["формат элемента"].Replace("{", "{{").Replace("}", "}}");
            }

            int depth = 15;
            if (options.ContainsKey("глубина"))
            {
                int.TryParse(options["глубина"], out depth);
            }

            int hours = 720;
            if (options.ContainsKey("часов"))
            {
                int.TryParse(options["часов"], out hours);
            }

            int maxItems = int.MaxValue;
            if (options.ContainsKey("элементов"))
            {
                int.TryParse(options["элементов"], out maxItems);
            }

            int normalSize = 40 * 1000;
            if (options.ContainsKey("нормальная"))
            {
                int.TryParse(options["нормальная"], out normalSize);
            }

            int shortSize = 10 * 1000;
            if (options.ContainsKey("небольшая"))
            {
                int.TryParse(options["небольшая"], out shortSize);
            }

            string longColor = "#F2FFF2";
            if (options.ContainsKey("цвет крупной"))
            {
                longColor = options["цвет крупной"];
            }

            string shortColor = "#FFE8E9";
            if (options.ContainsKey("цвет небольшой"))
            {
                archive = options["цвет небольшой"];
            }

            string normalColor = "#FFFDE8";
            if (options.ContainsKey("цвет нормальной"))
            {
                archive = options["цвет нормальной"];
            }

            string delimeter = "\n";
            if (options.ContainsKey("разделитель"))
            {
                delimeter = options["разделитель"].Replace("\"", "");
            }

            if (options.ContainsKey("тип"))
            {
                string t = options["тип"].ToLower();
                if (t == "список новых статей")
                {
                    if (!options.ContainsKey("архив"))
                    {
                        module = new NewPages(portal,
                            categories,
                            categoriesToIgnore,
                            title,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer);
                    }
                    else
                    {
                        module = new NewPagesWithArchive(portal,
                            categories,
                            categoriesToIgnore,
                            title,
                            archive,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer);
                    }
                }
                else if (t == "список наблюдения")
                {
                    module = new WatchList(portal,
                        categories[0],
                        title,
                        format,
                        depth);
                }
                else if (t == "отсортированный список статей, которые должны быть во всех проектах")
                {
                    module = new EncyShell(portal,
                        title,
                        shortSize,
                        normalSize,
                        shortColor,
                        normalColor,
                        longColor);
                }
            }
            return module != null;
        }
    }

    internal interface IPortalModule
    {
        void Update(Wiki wiki);
    }

    internal class PortalModule
    {
        public string Language { private set; get; }
        public string UpdateComment { private set; get; }

        public PortalModule(string language, string comment)
        {
            Language = language;
            UpdateComment = comment;
        }
    }
}
