﻿using System;
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
            string path = @"Cache\" + args[0] + @"\";
            Directory.CreateDirectory(@"Cache\" + args[0]);

            Wiki wiki = new Wiki(string.Format("http://{0}.wikipedia.org/w/", args[0]));
            wiki.SleepBetweenQueries = 5;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return 0;
            }

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            try
            {
                string cookieFile = @"Cache\" + args[0] + @"\cookie.jar";
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password, cookieFile);

                string namespacesFile = @"Cache\" + args[0] + @"\namespaces.dat";
                if (!WikiCache.LoadNamespaces(wiki, namespacesFile))
                {
                    wiki.GetNamespaces();
                    WikiCache.CacheNamespaces(wiki, namespacesFile);
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return 0;
            }
            Console.Out.WriteLine("Logged in as " + wiki.User + ".");

            PortalModule portal = new PortalModule(args[0], args[1]);
            Directory.CreateDirectory("Cache\\" + args[0] + "\\NewPages\\");
            Directory.CreateDirectory("Cache\\" + args[0] + "\\PagesInCategory\\");
            Directory.CreateDirectory("Cache\\" + args[0] + "\\PagesInCategoryWithTemplates\\");
            Cache.PurgeCache(args[0]);

            if (!File.Exists("Cache\\" + args[0] + "\\processed.txt"))
            {
                FileStream stream = File.Create("Cache\\" + args[0] + "\\processed.txt");
                stream.Close();
            }

            HashSet<string> processedPages = new HashSet<string>();
            using (StreamReader sr = new StreamReader("Cache\\" + args[0] + "\\processed.txt"))
            {
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                    processedPages.Add(line);
                }
            }

            ParameterCollection parameters = new ParameterCollection()
            {
                { "generator", "embeddedin" },
                { "geititle", "User:ClaymoreBot/Новые статьи" },
                { "geilimit", "max" },
                { "prop", "info" },
                { "intoken", "edit" },
                { "redirects", "1" }
            };

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
                try
                {
                    if (processedPages.Contains(pages[i]))
                    {
                        continue;
                    }
                    WikiPage page = Cache.Load(wiki, pages[i], path);
                    IPortalModule module;
                    if (TryParse(page, path, portal, out module))
                    {
                        module.Update(wiki);
                        using (StreamWriter sw = new StreamWriter("Cache\\" + args[0] + "\\processed.txt", true))
                        {
                            sw.WriteLine(pages[i]);
                        }
                    }
                }
                catch (WikiException)
                {
                    return -1;
                }
                catch (System.Net.WebException)
                {
                    return -1;
                }
            }

            File.Delete("Cache\\" + args[0] + "\\processed.txt");

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
            Regex commentRE = new Regex(@"<!--(.+?)-->");
            parameters = new Dictionary<string, string>();
            string parameterString = text.Substring(begin, end - begin);
            string[] ps = parameterString.Split(new char[] { '|' });
            string lastKey = "";
            foreach (var p in ps)
            {
                string[] keyvalue = p.Split(new char[] { '=' });
                if (keyvalue.Length == 2)
                {
                    string value = commentRE.Replace(keyvalue[1], "").Trim();
                    parameters.Add(keyvalue[0].Trim().ToLower(), value);
                    lastKey = keyvalue[0].Trim().ToLower();
                }
                else if (keyvalue.Length == 1)
                {
                    if (!string.IsNullOrEmpty(lastKey))
                    {
                        string value = commentRE.Replace(keyvalue[0], "").Trim();
                        parameters[lastKey] = parameters[lastKey] + "|" + value;
                    }
                }
                else if (keyvalue.Length > 2)
                {
                    string value = string.Join("=", keyvalue, 1, keyvalue.Length - 1);
                    value = commentRE.Replace(value, "").Trim();
                    parameters.Add(keyvalue[0].Trim().ToLower(), value);
                    lastKey = keyvalue[0].Trim().ToLower();
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

            var usersToIgnore = new List<string>();
            if (options.ContainsKey("игнорировать авторов"))
            {
                string[] separators;
                if (options["игнорировать авторов"].Contains("\""))
                {
                    separators = new string[] { "\"," };
                }
                else
                {
                    separators = new string[] { "," };
                }
                string[] cats = options["игнорировать авторов"].Split(separators,
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < cats.Length; ++i)
                {
                    string cat = cats[i].Replace("\"", "").Trim();
                    if (!string.IsNullOrEmpty(cat))
                    {
                        usersToIgnore.Add(cat);
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

            string prefix = "";
            if (options.ContainsKey("префикс"))
            {
                prefix = options["префикс"];
            }

            bool markEdits = true;
            if (options.ContainsKey("помечать правки") && options["помечать правки"].ToLower() == "нет")
            {
                markEdits = false;
            }

            int ns = 0;
            if (options.ContainsKey("пространство имён"))
            {
                int.TryParse(options["пространство имён"], out ns);
            }

            string header = "";
            if (options.ContainsKey("шапка"))
            {
                header = options["шапка"].Replace("\\n", "\n");
            }

            string footer = "";
            if (options.ContainsKey("подвал"))
            {
                footer = options["подвал"].Replace("\\n", "\n");
            }

            string templates = "";
            if (options.ContainsKey("шаблоны"))
            {
                templates = options["шаблоны"].Replace("\\n", "\n");
            }

            string format = "* [[%(название)]]";
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

            int maxItems = 20;
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
                delimeter = options["разделитель"].Replace("\"", "").Replace("\\n", "\n");
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
                            usersToIgnore,
                            title,
                            ns,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
                    }
                    else
                    {
                        module = new NewPagesWithArchive(portal,
                            categories,
                            categoriesToIgnore,
                            usersToIgnore,
                            title,
                            ns,
                            archive,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
                    }
                }
                else if (t == "список наблюдения")
                {
                    module = new WatchList(portal,
                        categories[0],
                        title,
                        ns,
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
                else if (t == "список новых статей с изображениями в карточке")
                {
                    module = new NewPagesWithImages(portal,
                            categories,
                            categoriesToIgnore,
                            title,
                            ns,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
                }
                else if (t == "список новых статей с изображениями")
                {
                    Regex regex = new Regex(@"\[{2}(Image|File|Файл|Изображение):(?'fileName'.+?)\|");
                    module = new NewPagesWithImages(portal,
                            categories,
                            categoriesToIgnore,
                            title,
                            ns,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            regex,
                            markEdits);
                }
                else if (t == "списки новых статей по дням")
                {
                    module = new NewPagesWithWeeks(portal,
                            categories,
                            categoriesToIgnore,
                            title,
                            ns,
                            depth,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
                }
                else if (t == "список страниц с заданными категориями и шаблонами")
                {
                    module = new CategoryTemplateIntersection(portal,
                            categories,
                            categoriesToIgnore,
                            templates,
                            title,
                            ns,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
                }
                else if (t == "список страниц с заданными категориями, шаблонами и обсуждением")
                {
                    module = new CategoryIntersectionAndTalkPages(portal,
                            categories,
                            categoriesToIgnore,
                            templates,
                            prefix,
                            title,
                            ns,
                            depth,
                            hours,
                            maxItems,
                            format,
                            delimeter,
                            header,
                            footer,
                            markEdits);
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
