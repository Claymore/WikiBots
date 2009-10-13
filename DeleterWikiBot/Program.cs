using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.DeleterWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.DeleterWikiBot
{
    internal class Program
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
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            string listText;
            try
            {
                listText = wiki.LoadText("Шаблон:Список подводящих итоги");
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return -1;
            }

            StringReader reader = new StringReader(listText);
            HashSet<string> users = new HashSet<string>();
            Regex userRE = new Regex(@"^\*\s*\[\[(User|Участник):(.+)\|.+\]\]\s*$", RegexOptions.IgnoreCase);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Match m = userRE.Match(line);
                if (m.Success)
                {
                    users.Add(m.Groups[2].Value);
                }
            }

            ParameterCollection parameters = new ParameterCollection
            {
                { "generator", "embeddedin" },
                { "geititle", "Template:Db-discussion" },
                { "geilimit", "max"},
                { "prop", "info" },
                { "intoken", "delete" },
                { "inprop", "talkid" }
            };

            Regex templateRE = new Regex(@"\{\{db-discussion\|(\d+?)\|(.+?)\}\}", RegexOptions.IgnoreCase);
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

            bool failed = false;
            foreach (XmlNode page in doc.SelectNodes("//page"))
            {
                string title = page.Attributes["title"].Value;
                parameters = new ParameterCollection
                {
                    { "prop", "revisions" },
                    { "rvprop", "content" },
                    { "rvlimit", "1" }
                };

                XmlDocument xml;
                try
                {
                    xml = wiki.Query(QueryBy.Titles, parameters, title);
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
                
                Match m = templateRE.Match(content);
                if (m.Success)
                {
                    string timestamp = m.Groups[1].Value;

                    parameters = new ParameterCollection
                    {
                        { "prop", "revisions" },
                        { "rvprop", "timestamp|user|content" },
                        { "rvlimit", "1" },
                        { "rvstart", timestamp },
                        { "rvdir", "newer" },
                    };

                    try
                    {
                        xml = wiki.Query(QueryBy.Titles, parameters, title);
                    }
                    catch (WikiException e)
                    {
                        Console.Out.WriteLine(e.Message);
                        failed = true;
                        continue;
                    }
                    node = xml.SelectSingleNode("//rev");
                    if (node != null && users.Contains(node.Attributes["user"].Value))
                    {
                        content = node.FirstChild.Value;
                        m = templateRE.Match(content);
                        if (m.Success && m.Groups[1].Value == timestamp)
                        {
                            string reason = string.Format("удалил [[User:{0}|{0}]]: {1}",
                                node.Attributes["user"].Value,
                                m.Groups[2].Value);

                            string token = page.Attributes["deletetoken"].Value;
                            try
                            {
                                wiki.Delete(title, reason, token);
                            }
                            catch (WikiException e)
                            {
                                Console.Out.WriteLine(e.Message);
                                failed = true;
                                continue;
                            }

                            parameters = new ParameterCollection
                            {
                                { "list", "backlinks" },
                                { "blfilterredir", "redirects" },
                                { "bllimit", "max" },
                                { "bltitle", title },
                            };

                            try
                            {
                                xml = wiki.Enumerate(parameters, true);
                            }
                            catch (WikiException e)
                            {
                                Console.Out.WriteLine(e.Message);
                                failed = true;
                                continue;
                            }

                            foreach (XmlNode backlink in xml.SelectNodes("//bl"))
                            {
                                try
                                {
                                    wiki.Delete(backlink.Attributes["title"].Value,
                                       "[[ВП:КБУ#П1|П1]]: перенаправление в никуда",
                                      wiki.Token);
                                }
                                 catch (WikiException e)
                                 {
                                     Console.Out.WriteLine(e.Message);
                                     failed = true;
                                     continue;
                                 }
                            }
                        }
                    }
                }
            }
            return failed ? -1 : 0;
        }
    }
}
