using System;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.InUseCleanupWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.InUseCleanupWikiBot
{
    class Program
    {
        static int Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");
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
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return 0;
            }
            Console.Out.WriteLine("Logged in as " + wiki.User + ".");

            ParameterCollection parameters = new ParameterCollection
            {
                { "list", "embeddedin" },
                { "eititle", "Шаблон:Редактирую" },
                { "eilimit", "max"},
                { "einamespace", "0" },
            };

            XmlDocument doc;
            try
            {
                doc = wiki.Enumerate(parameters, true);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return -1;
            }

            Regex templateRE = new Regex(@"\{\{(Редактирую|Перерабатываю|Inuse-by|Пишу|Inuse|Правлю|Перевожу|In-use|Processing|Process|Редактирую раздел)\|1=(.+?)\|2=.+?}}\s?",
                RegexOptions.IgnoreCase);

            Regex templateRE2 = new Regex(@"\{\{(Редактирую|Перерабатываю|Inuse-by|Пишу|Inuse|Правлю|Перевожу|In-use|Processing|Process|Редактирую раздел)\|(.*?\[\[.+?]].*?)(\|.+?)?}}\s?",
                RegexOptions.IgnoreCase);

            Regex templateRE3 = new Regex(@"\{\{(Редактирую|Перерабатываю|Inuse-by|Пишу|Inuse|Правлю|Перевожу|In-use|Processing|Process|Редактирую раздел)}}\s?",
                RegexOptions.IgnoreCase);

            Regex userRE = new Regex(@"\[\[(User:|Участник:|special:contributions/|Служебная:contributions/)(.+?)(\|.+?)*?\]\]",
                RegexOptions.IgnoreCase);

            bool failed = false;
            foreach (XmlNode page in doc.SelectNodes("//ei"))
            {
                string title = page.Attributes["title"].Value;
                parameters = new ParameterCollection
                {
                    { "prop", "revisions" },
                    { "rvprop", "timestamp|content" },
                    { "rvlimit", "1" }
                };

                XmlDocument xml;
                try
                {
                    xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title }, 500, false);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine(e.Message);
                    failed = true;
                    continue;
                }
                XmlNode node = xml.SelectSingleNode("//rev");
                if (node == null || node.FirstChild == null)
                {
                    failed = true;
                    continue;
                }

                string content = node.FirstChild.Value;
                DateTime timestamp = DateTime.Parse(node.Attributes["timestamp"].Value,
                    null,
                    System.Globalization.DateTimeStyles.AssumeUniversal);
                if ((DateTime.Now - timestamp).TotalDays >= 3)
                {
                    Match m = templateRE.Match(content);
                    string text = content;
                    string user = "";
                    if (m.Success)
                    {
                        text = templateRE.Replace(content, "");
                        user = m.Groups[2].Value;
                    }
                    else
                    {
                        m = templateRE2.Match(content);
                        if (m.Success)
                        {
                            text = templateRE2.Replace(content, "");
                            user = m.Groups[2].Value;
                        }
                        else
                        {
                            m = templateRE3.Match(content);
                            if (m.Success)
                            {
                                text = templateRE3.Replace(content, "");
                            }
                        }
                    }
                    if (text == content)
                    {
                        continue;
                    }

                    try
                    {
                        Console.WriteLine("Updating " + title + "...");
                        wiki.Save(title,
                            text,
                            "автоматическое удаление просроченного шаблона [[Шаблон:Редактирую|редактирую]]");
                    }
                    catch (WikiException e)
                    {
                        Console.Out.WriteLine(e.Message);
                        failed = true;
                        continue;
                    }

                    Match um = userRE.Match(user);
                    if (um.Success)
                    {
                        string notification = "\n== Уведомление ==\nЗдравствуйте! Пожалуйста, обратите внимание, что в статье [[" +
                            title + "]] был автоматически удалён установленный вами шаблон {{tl|Редактирую}}. ~~~~";
                        string userTalk = "Обсуждение участника:" + um.Groups[2].Value;
                        try
                        {
                            Console.WriteLine("Creating a topic on " + userTalk + "...");
                            wiki.Append(userTalk,
                                notification,
                                "/* Уведомление */ новая тема: автоматическое удаление просроченного шаблона",
                                MinorFlags.NotMinor,
                                false);
                        }
                        catch (WikiException e)
                        {
                            Console.Out.WriteLine(e.Message);
                            failed = true;
                        }
                    }
                }
            }

            return failed ? -1 : 0;
        }
    }
}
