using System;
using System.Xml;
using Claymore.NullEditWikiBot.Properties;
using Claymore.SharpMediaWiki;
using System.Collections.Generic;

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

            Dictionary<string, string> templates = new Dictionary<string, string>();
            templates.Add("Шаблон:Deleteslow", "Категория:Википедия:К быстрому удалению");
            //templates.Add("Шаблон:К удалению", "Категория:Википедия:Просроченные подведения итогов по удалению страниц");

            int index;
            XmlNodeList pages;
            XmlDocument doc;
            ParameterCollection parameters = new ParameterCollection();
            foreach (var template in templates)
            {
                parameters.Clear();
                parameters.Add("generator", "embeddedin");
                parameters.Add("geititle", template.Key);
                parameters.Add("geilimit", "max");
                parameters.Add("geinamespace", "0");
                parameters.Add("prop", "categories");
                parameters.Add("clcategories", template.Value);

                try
                {
                    doc = wiki.Enumerate(parameters, true);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine(e.Message);
                    return;
                }

                pages = doc.SelectNodes("//page");
                index = 1;
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
            }

            parameters.Clear();
            parameters.Add("generator", "embeddedin");
            parameters.Add("geititle", "Шаблон:Orphaned-fairuse");
            parameters.Add("geilimit", "max");
            parameters.Add("geinamespace", "6");
            parameters.Add("prop", "categories");
            parameters.Add("clcategories", "Категория:Файлы:Неиспользуемые несвободные:Просроченные подведения итогов");

            try
            {
                doc = wiki.Enumerate(parameters, true);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }

            pages = doc.SelectNodes("//page");
            index = 1;
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
                    break;
                }
            }

            parameters.Clear();
            parameters.Add("generator", "categorymembers");
            parameters.Add("gcmtitle", "Категория:Файлы:Перенесённые ботом");
            parameters.Add("gcmlimit", "max");
            parameters.Add("gcmnamespace", "6");
            parameters.Add("prop", "categories");
            parameters.Add("clcategories", "Категория:Файлы:Перенесённые на Викисклад");

            try
            {
                doc = wiki.Enumerate(parameters, true);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }

            pages = doc.SelectNodes("//page");
            index = 1;
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
                    break;
                }
            }
            Console.Out.WriteLine("Done.");
        }
    }
}
