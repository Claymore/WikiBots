using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Claymore.NullEditWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NullEditWikiBot
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return -1;
            }
            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            try
            {
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return -1;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            string errorFileName = @"Errors.txt";
            if (!File.Exists(errorFileName))
            {
                using (FileStream stream = File.Create(errorFileName)) { }
            }

            int lastIndex = 0;
            int taskIndex = 0;
            using (TextReader streamReader = new StreamReader(errorFileName))
            {
                string line = streamReader.ReadToEnd();
                if (!string.IsNullOrEmpty(line))
                {
                    lastIndex = int.Parse(line);
                }
            }

            Dictionary<string, string> templates = new Dictionary<string, string>();
            templates.Add("Шаблон:Deleteslow", "Категория:Википедия:К быстрому удалению");
            //templates.Add("Шаблон:К удалению", "Категория:Википедия:Просроченные подведения итогов по удалению страниц");

            int index;
            XmlNodeList pages;
            XmlDocument doc;
            ParameterCollection parameters = new ParameterCollection();
            foreach (var template in templates)
            {
                if (lastIndex <= taskIndex)
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
                        using (TextWriter streamWriter = new StreamWriter(errorFileName))
                        {
                            streamWriter.Write(taskIndex);
                        }
                        return -1;
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
                            wiki.Append(pageTitle,
                                "\n\n",
                                "Сброс кеша нулевой правкой");
                        }
                        catch (WikiException e)
                        {
                            Console.Out.WriteLine(e.Message);
                            using (TextWriter streamWriter = new StreamWriter(errorFileName))
                            {
                                streamWriter.Write(taskIndex);
                            }
                            return -1;
                        }
                    }
                    ++taskIndex;
                }
            }

            if (lastIndex <= taskIndex)
            {
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
                    using (TextWriter streamWriter = new StreamWriter(errorFileName))
                    {
                        streamWriter.Write(taskIndex);
                    }
                    return -1;
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
                        wiki.Append(pageTitle,
                            "\n\n",
                            "Сброс кеша нулевой правкой");
                    }
                    catch (WikiException e)
                    {
                        Console.Out.WriteLine(e.Message);
                        using (TextWriter streamWriter = new StreamWriter(errorFileName))
                        {
                            streamWriter.Write(taskIndex);
                        }
                        break;
                    }
                }

                ++taskIndex;
            }

            if (lastIndex <= taskIndex)
            {
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
                    using (TextWriter streamWriter = new StreamWriter(errorFileName))
                    {
                        streamWriter.Write(taskIndex);
                    }
                    return -1;
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
                        wiki.Append(pageTitle,
                            "\n\n",
                            "Сброс кеша нулевой правкой");
                    }
                    catch (WikiException e)
                    {
                        Console.Out.WriteLine(e.Message);
                        using (TextWriter streamWriter = new StreamWriter(errorFileName))
                        {
                            streamWriter.Write(taskIndex);
                        }
                        return -1;
                    }
                }
            }

            if (File.Exists(errorFileName))
            {
                File.Delete(errorFileName);
            }

            Console.Out.WriteLine("Done.");
            return 0;
        }
    }
}
