using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Claymore.SharpMediaWiki;
using ReviewStatsWikiBot.Properties;

namespace Claymore.ReviewStatsWikiBot
{
    class Program
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
                wiki.Login(Settings.Default.Login, Settings.Default.Password);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            DateTime now = DateTime.Now.ToUniversalTime();
            DateTime currentMonth = new DateTime(now.Year, now.Month, 1);
            DateTime previousMonth = currentMonth.AddMonths(-1);
            string start = previousMonth.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string stop = currentMonth.ToString("yyyy-MM-ddTHH:mm:ssZ");

            Console.Out.WriteLine("Quering list of editors...");
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("list", "allusers");
            parameters.Add("augroup", "editor");
            parameters.Add("aulimit", "max");
            wiki.SleepBetweenQueries = 10;
            XmlDocument doc = wiki.Enumerate(parameters, true);
            XmlNodeList editors = doc.SelectNodes("//u[@name]");
            
            List<User> users = new List<User>();
            int index = 1;
            foreach (XmlNode user in editors)
            {
                string username = user.Attributes["name"].Value;
                Console.Out.WriteLine(string.Format("Quering actions of user '{0} ({1}/{2})",
                    username, index++, editors.Count));
                parameters.Clear();
                parameters.Add("list", "logevents");
                parameters.Add("letype", "review");
                parameters.Add("lestart", start);
                parameters.Add("lestop", stop);
                parameters.Add("ledir", "newer");
                parameters.Add("lelimit", "max");
                parameters.Add("leuser", username);

                XmlDocument result = wiki.Enumerate(parameters, true);
                XmlNodeList artActions = result.SelectNodes("//item[@action=\"approve\" and @ns=\"0\"] | //item[@action=\"approve-i\" and @ns=\"0\"]");
                XmlNodeList catActions = result.SelectNodes("//item[@action=\"approve\" and @ns=\"14\"] | //item[@action=\"approve-i\" and @ns=\"14\"]");
                XmlNodeList tempActions = result.SelectNodes("//item[@action=\"approve\" and @ns=\"10\"] | //item[@action=\"approve-i\" and @ns=\"10\"]");
                XmlNodeList fileActions = result.SelectNodes("//item[@action=\"approve\" and @ns=\"6\"] | //item[@action=\"approve-i\" and @ns=\"6\"]");
                int totalActions = artActions.Count + catActions.Count + tempActions.Count + fileActions.Count;
                if (totalActions > 0)
                {
                    users.Add(new User(username, artActions.Count, catActions.Count, tempActions.Count, fileActions.Count));
                }
            }

            Console.Out.WriteLine("Processing data...");

            /*using (TextReader sr =
                            new StreamReader("cache.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    int index = line.IndexOf(' ');
                    if (index != -1)
                    {
                        int actions = int.Parse(line.Substring(0, index));
                        string user = line.Substring(index + 1);
                        if (actions > 0)
                        {
                            users.Add(new User(actions, user));
                        }
                    }
                }
            }*/

            users.Sort(CompareUsers);

            using (StreamWriter sw =
                        new StreamWriter("output.txt", false))
            {
                sw.WriteLine("== " + previousMonth.ToString("MMMM") + " ==");
                sw.WriteLine("В скобках указано количество действий в пространстве имён: статей, категорий, шаблонов и файлов.\n");
                sw.WriteLine("<div style=\"-moz-column-count:2; column-count:2; -webkit-column-count:2\">");
                for (int i = 0; i < users.Count; ++i)
                {
                    string line = string.Format("# [[User:{0}|{0}]] — {1} ({2}/{3}/{4}/{5})",
                        users[i].Name,
                        Actions(users[i].Actions),
                        users[i].ArticleActions,
                        users[i].CategoryActions,
                        users[i].TemplateActions,
                        users[i].FileActions);
                    if (i != users.Count - 1)
                    {
                        line += ";";
                    }
                    else
                    {
                        line += ".";
                    }
                    sw.WriteLine(line);
                }
                sw.WriteLine("</div>");
                sw.WriteLine("\n— ~~~~");
            }
            /*Console.Out.WriteLine("Updating the wiki page...");
            using (TextReader sr =
                        new StreamReader("output.txt"))
            {
                string text = sr.ReadToEnd();
                string period = previousMonth.ToString("MMMM yyyy");
                wiki.SavePage("User:Claymore/Sandbox", text, "Статистика патрулирования за " + period[0].ToString().ToLower() + period.Substring(1));
            }*/
            Console.Out.WriteLine("Done.");
            wiki.Logout();
        }

        static int CompareUsers(User x, User y)
        {
            return y.Actions.CompareTo(x.Actions);
        }

        struct User
        {
            public int CategoryActions;
            public int TemplateActions;
            public int ArticleActions;
            public int FileActions;
            public string Name;

            public User(string name, int artActions, int catActions, int tempActions, int fileActions)
            {
                Name = name;
                ArticleActions = artActions;
                CategoryActions = catActions;
                TemplateActions = tempActions;
                FileActions = fileActions;
            }

            public override string ToString()
            {
                return string.Format("[{0}, {1}]", Name, Actions);
            }

            public int Actions
            {
                get
                {
                    return ArticleActions + CategoryActions + TemplateActions + FileActions;
                }
            }
        }

        static string RussianNounPlural(int number, string one, string two, string others)
        {
            bool exception = (number % 100) / 10 == 1;
            int digit = number % 10;
            if (digit == 1)
            {
                return string.Format("{0} {1}", number, one);
            }
            else if (!exception && digit > 1 && digit < 5)
            {
                return string.Format("{0} {1}", number, two);
            }
            else
            {
                return string.Format("{0} {1}", number, others);
            }
        }

        static string Actions(int number)
        {
            return RussianNounPlural(number, "действие", "действия", "действий");
        }
    }
}
