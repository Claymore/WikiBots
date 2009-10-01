using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            string listText = wiki.LoadPage("Википедия:Список подводящих итоги");
            WikiPage listPage = WikiPage.Parse("Википедия:Список подводящих итоги", listText);
            WikiPageSection usersSection = listPage.Sections.FirstOrDefault(ss => ss.Title.ToLower().Trim() == "участники");
            if (usersSection == null)
            {
                return 0;
            }
            StringReader reader = new StringReader(usersSection.SectionText);
            HashSet<string> users = new HashSet<string>();
            Regex userRE = new Regex(@"^\*\s*(.+)\s*$");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Match m = userRE.Match(line);
                if (m.Success)
                {
                    users.Add(m.Groups[1].Value);
                }
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("generator", "embeddedin");
            parameters.Add("geititle", "Template:Db-discussion");
            parameters.Add("geilimit", "max");
            parameters.Add("prop", "info");
            parameters.Add("intoken", "delete");

            Regex templateRE = new Regex(@"\{\{db-discussion\|(\d+)\|(.+?)\}\}", RegexOptions.IgnoreCase);
            XmlDocument doc = wiki.Enumerate(parameters, false);
            foreach (XmlNode page in doc.SelectNodes("//page"))
            {
                string title = page.Attributes["title"].Value;
                parameters.Clear();
                parameters.Add("prop", "revisions");
                parameters.Add("rvprop", "content");
                parameters.Add("rvlimit", "1");

                XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                XmlNode node = xml.SelectSingleNode("//rev");
                string content = node.FirstChild.Value;
                
                Match m = templateRE.Match(content);
                if (m.Success)
                {
                    string timestamp = m.Groups[1].Value;

                    parameters.Clear();
                    parameters.Add("prop", "revisions");
                    parameters.Add("rvprop", "timestamp|user|content");
                    parameters.Add("rvlimit", "1");
                    parameters.Add("rvstart", timestamp);
                    parameters.Add("rvdir", "newer");

                    xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                    node = xml.SelectSingleNode("//rev");
                    if (node != null && users.Contains(node.Attributes["user"].Value))
                    {
                        content = node.FirstChild.Value;
                        m = templateRE.Match(content);
                        if (m.Success && m.Groups[1].Value == timestamp)
                        {
                            string reason = string.Format("[[User:{0}|{0}]], {1}",
                                node.Attributes["user"].Value,
                                m.Groups[2].Value);

                            string token = page.Attributes["deletetoken"].Value;
                            wiki.DeletePage(title, reason, token);
                        }
                    }
                }
            }
            return 0;
        }
    }
}
