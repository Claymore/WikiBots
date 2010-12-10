using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using ReviewStatsWikiBot.Properties;

namespace Claymore.ReviewStatsWikiBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");
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
            wiki.SleepBetweenQueries = 3;

            Regex re = new Regex(@"\*\s*\[\[User:(.+?)\]\]\s*→\s*\[\[User:(.+?)\]\]");
            Dictionary<string, string> renamedUsers = new Dictionary<string, string>();
            string renamedUsersData = wiki.LoadText("Википедия:Проект:Патрулирование/Статистика/1k+/Переименования");
            using (TextReader sr = new StringReader(renamedUsersData))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match m = re.Match(line);
                    if (m.Success)
                    {
                        string oldName = m.Groups[1].Value;
                        string newName = m.Groups[2].Value;
                        if (!renamedUsers.ContainsKey(oldName))
                        {
                            renamedUsers.Add(oldName, newName);
                        }
                    }
                }
            }

            DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime now = currentMonth;
            DateTime firstReviewMonth = new DateTime(2008, 9, 1);
            while (currentMonth > firstReviewMonth)
            {
                DateTime previousMonth = currentMonth.AddMonths(-1);
                if (File.Exists("output" + previousMonth.ToString("yyyy-MM") + ".txt"))
                {
                    currentMonth = currentMonth.AddMonths(-1);
                    continue;
                }
                string start = previousMonth.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string stop = currentMonth.ToString("yyyy-MM-ddTHH:mm:ssZ");

                Console.Out.WriteLine("Quering list of editors for " + previousMonth.ToString("MMMM yyyy") + "...");
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("list", "logevents");
                parameters.Add("letype", "review");
                parameters.Add("lestart", start);
                parameters.Add("leend", stop);
                parameters.Add("ledir", "newer");
                parameters.Add("lelimit", "max");

                Dictionary<string, User> users = new Dictionary<string, User>();
                XmlNode continueNode = null;
                while (true)
                {
                    XmlDocument doc;
                    try
                    {
                        doc = wiki.MakeRequest(Claymore.SharpMediaWiki.Action.Query, parameters);
                    }
                    catch (WikiException e)
                    {
                        Console.Out.WriteLine(e.Message);
                        return;
                    }

                    continueNode = doc.SelectSingleNode("//query-continue");
                    if (continueNode != null)
                    {
                        string name = continueNode.FirstChild.Attributes[0].Name;
                        string value = continueNode.FirstChild.Attributes[0].Value;
                        parameters.Set(name, value);
                    }

                    XmlNodeList entries = doc.SelectNodes("//item[@action!=\"approve-a\" and @action!=\"approve-ia\"]");

                    foreach (XmlNode entry in entries)
                    {
                        string username = renamedUsers.ContainsKey(entry.Attributes["user"].Value)
                            ? renamedUsers[entry.Attributes["user"].Value]
                            : entry.Attributes["user"].Value;
                        string ns = entry.Attributes["ns"].Value;
                        if (!users.ContainsKey(username))
                        {
                            User user = new User(username);
                            if (ns == "0")
                            {
                                user.ArticleActions = 1;
                            }
                            else if (ns == "14")
                            {
                                user.CategoryActions = 1;
                            }
                            else if (ns == "10")
                            {
                                user.TemplateActions = 1;
                            }
                            else if (ns == "6")
                            {
                                user.FileActions = 1;
                            }
                            users.Add(username, user);
                        }
                        else
                        {
                            User user = users[username];
                            if (ns == "0")
                            {
                                ++user.ArticleActions;
                            }
                            else if (ns == "14")
                            {
                                ++user.CategoryActions;
                            }
                            else if (ns == "10")
                            {
                                ++user.TemplateActions;
                            }
                            else if (ns == "6")
                            {
                                ++user.FileActions;
                            }
                            users[username] = user;
                        }
                    }

                    if (continueNode == null)
                    {
                        break;
                    }
                }

                Console.Out.WriteLine("Processing data...");

                List<User> userList = new List<User>(users.Select(s => s.Value));
                userList.Sort(CompareUsers);

                using (StreamWriter sw =
                            new StreamWriter("output" + previousMonth.ToString("yyyy-MM") + ".txt", false))
                {
                    int totalActions = 0;
                    int totalArticleActions = 0;
                    int totalCategoryActions = 0;
                    int totalTemplateActions = 0;
                    int totalFileActions = 0;
                    bool bots = false;
                    sw.WriteLine("== " + previousMonth.ToString("MMMM") + " ==");
                    sw.WriteLine("{| class=\"standard sortable\"");
                    sw.WriteLine("!№!!Участник!!всего!!статей!!категорий!!шаблонов!!файлов");
                    for (int i = 0, j = 1; i < userList.Count; ++i)
                    {
                        if (userList[i].Name.Contains("Lockalbot") ||
                            userList[i].Name.Contains("Secretary"))
                        {
                            bots = true;
                            continue;
                        }
                        sw.WriteLine("|-");
                        totalActions += userList[i].Actions;
                        totalArticleActions += userList[i].ArticleActions;
                        totalCategoryActions += userList[i].CategoryActions;
                        totalTemplateActions += userList[i].TemplateActions;
                        totalFileActions += userList[i].FileActions;
                        string line = string.Format("|{0}||[[User:{1}|]]||{2}||{3}||{4}||{5}||{6}",
                            j++,
                            userList[i].Name,
                            userList[i].Actions,
                            userList[i].ArticleActions,
                            userList[i].CategoryActions,
                            userList[i].TemplateActions,
                            userList[i].FileActions);
                        sw.WriteLine(line);
                    }
                    sw.WriteLine("|-");
                    sw.WriteLine("|||Итого||{0}||{1}||{2}||{3}||{4}",
                        totalActions,
                        totalArticleActions,
                        totalCategoryActions,
                        totalTemplateActions,
                        totalFileActions);
                    sw.WriteLine("|}");

                    if (bots)
                    {
                        sw.WriteLine("; Боты");
                        sw.WriteLine("{| class=\"standard sortable\"");
                        sw.WriteLine("!№!!Участник!!всего!!статей!!категорий!!шаблонов!!файлов");
                        for (int i = 0, j = 1; i < userList.Count; ++i)
                        {
                            if (userList[i].Name.Contains("Lockalbot") ||
                                userList[i].Name.Contains("Secretary"))
                            {
                                sw.WriteLine("|-");
                                string line = string.Format("|{0}||[[User:{1}|]]||{2}||{3}||{4}||{5}||{6}",
                                    j++,
                                    userList[i].Name,
                                    userList[i].Actions,
                                    userList[i].ArticleActions,
                                    userList[i].CategoryActions,
                                    userList[i].TemplateActions,
                                    userList[i].FileActions);
                                sw.WriteLine(line);
                            }
                        }
                        sw.WriteLine("|}");
                    }
                    sw.WriteLine("\n— ~~~~");
                }
                currentMonth = currentMonth.AddMonths(-1);
            }

            currentMonth = new DateTime(2008, 9, 1);
            Dictionary<string, List<MonthStat>> userStatistics = new Dictionary<string, List<MonthStat>>();
            while (currentMonth < now)
            {
                using (TextReader sr =
                        new StreamReader("output" + currentMonth.ToString("yyyy-MM") + ".txt"))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == "|-")
                        {
                            line = sr.ReadLine();
                            string[] fields = line.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                            string user = fields[1];
                            int actions = int.Parse(fields[2]);
                            if (user.StartsWith("[[User:") && !userStatistics.ContainsKey(user) && actions >= 1000)
                            {
                                List<MonthStat> stats = new List<MonthStat>();
                                userStatistics.Add(user, stats);
                            }
                        }
                    }
                }
                currentMonth = currentMonth.AddMonths(1);
            }

            currentMonth = new DateTime(2008, 9, 1);
            while (currentMonth < now)
            {
                using (TextReader sr =
                        new StreamReader("output" + currentMonth.ToString("yyyy-MM") + ".txt"))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == "|-")
                        {
                            line = sr.ReadLine();
                            string[] fields = line.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                            string user = fields[1];
                            int actions = int.Parse(fields[2]);
                            if (userStatistics.ContainsKey(user))
                            {
                                List<MonthStat> stats = userStatistics[user];
                                stats.Add(new MonthStat(currentMonth, actions));
                            }
                        }
                    }
                }
                currentMonth = currentMonth.AddMonths(1);
            }

            using (StreamWriter sw =
                           new StreamWriter("output.txt", false))
            {
                sw.WriteLine("== Статистика ==");

                for (int year = DateTime.Today.Year; year >= 2008; --year)
                {
                    DateTime currentYear = new DateTime(year, 1, 1);
                    List<UserStat> userStats = new List<UserStat>();
                    foreach (var user in userStatistics)
                    {
                        var stats = user.Value.Where(s => (s.Month >= currentYear && s.Month < currentYear.AddYears(1)));
                        UserStat userStat = new UserStat(user.Key, stats);
                        userStat.enterDate = user.Value.Where(s => s.Actions >= 1000).Min(s => s.Month);
                        userStat.ActionsBefore = user.Value.Where(s => s.Month < currentYear).Sum(s => s.Actions);
                        userStat.Max = user.Value.Max(s => s.Actions);
                        if (stats.Count() > 0 && userStat.enterDate.Year <= year)
                        {
                            userStats.Add(userStat);
                        }
                    }
                    userStats.Sort(CompareUserStat);

                    sw.WriteLine("\n=== {0} ===", currentYear.Year);
                    sw.WriteLine("{| class=\"wikitable sortable\"");
                    sw.Write("! № !! Участник");
                    currentMonth = currentYear;
                    while (currentMonth < currentYear.AddYears(1))
                    {
                        sw.Write(" !! " + currentMonth.ToString("MMM yy"));
                        currentMonth = currentMonth.AddMonths(1);
                    }
                    sw.Write(" !! За {0} !! На конец {0} ", currentYear.Year);
                    sw.WriteLine();

                    for (int index = 0; index < userStats.Count; ++index)
                    {
                        sw.WriteLine("|-");
                        sw.WriteLine(userStats[index].IsBot ? "! Бот" : string.Format("! {0}", index + 1));
                        sw.Write(string.Format("| {0}", userStats[index].Name));
                        int max = userStats[index].Stat.Max(s => s.Actions);
                        currentMonth = currentYear;
                        int totalActions = 0;
                        while (currentMonth < currentYear.AddYears(1))
                        {
                            int actions = 0;
                            foreach (var item in userStats[index].Stat)
                            {
                                if (item.Month == currentMonth)
                                {
                                    actions = item.Actions;
                                    break;
                                }
                            }
                            if (actions != userStats[index].Max)
                            {
                                sw.Write(" || " + actions);
                            }
                            else
                            {
                                sw.Write(string.Format(" || '''{0}'''", actions));
                            }
                            totalActions += actions;
                            currentMonth = currentMonth.AddMonths(1);
                        }
                        sw.Write(string.Format(" || {0}", totalActions));
                        sw.Write(string.Format(" || {0}", totalActions + userStats[index].ActionsBefore));
                        sw.WriteLine();
                    }
                    sw.WriteLine("|}");
                }
            }

            currentMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            Console.Out.WriteLine("Updating the wiki page...");
            using (TextReader sr =
                        new StreamReader("output" + currentMonth.ToString("yyyy-MM") + ".txt"))
            {
                DateTime previousMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                string text = sr.ReadToEnd();
                string period = previousMonth.ToString("MMMM yyyy");
                wiki.Save(previousMonth.ToString("Википедия:Проект:Патрулирование\\/Статистика\\/yyyy\\/MM"),
                    text,
                    "статистика патрулирования за " + period[0].ToString().ToLower() + period.Substring(1));
            }

            Console.Out.WriteLine("Updating Википедия:Проект:Патрулирование/Статистика/1k+...");
            using (TextReader sr =
                        new StreamReader("output.txt"))
            {
                string text = sr.ReadToEnd();
                wiki.SaveSection("Википедия:Проект:Патрулирование/Статистика/1k+",
                    "1",
                    text,
                    "обновление");
            }
            wiki.Save(currentMonth.ToString("Википедия:Проект:Патрулирование\\/Статистика\\/yyyy"),
                string.Format("#REDIRECT [[{0}]]", currentMonth.ToString("Википедия:Проект:Патрулирование\\/Статистика\\/yyyy\\/MM")),
                "обновление");
            Console.Out.WriteLine("Done.");
            wiki.Logout();
        }

        static int CompareUsers(User x, User y)
        {
            if (x.Actions == y.Actions)
            {
                return x.Name.CompareTo(y.Name);
            }
            return y.Actions.CompareTo(x.Actions);
        }

        static int CompareUserStat(UserStat x, UserStat y)
        {
            if (x.IsBot && !y.IsBot)
            {
                return 1;
            }
            if (!x.IsBot && y.IsBot)
            {
                return -1;
            }
            if (y.TotalActions == x.TotalActions)
            {
                return x.Name.CompareTo(y.Name);
            }
            return y.TotalActions.CompareTo(x.TotalActions);
        }

        struct MonthStat
        {
            public DateTime Month;
            public int Actions;

            public MonthStat(DateTime month, int actions)
            {
                Month = month;
                Actions = actions;
            }
        }

        class UserStat
        {
            public string Name;
            private List<MonthStat> _stat;
            public bool IsBot;
            public DateTime enterDate;
            public int ActionsBefore;
            public int Max;

            public UserStat(string name, IEnumerable<MonthStat> stat)
            {
                Name = name;
                _stat = new List<MonthStat>(stat);
                enterDate = DateTime.Today;

                IsBot = name.Contains("Lockalbot") || name.Contains("Secretary");
            }

            public IEnumerable<MonthStat> Stat
            {
                get { return _stat; }
            }

            public int TotalActions
            {
                get
                {
                    int sum = 0;
                    for (int i = 0; i < _stat.Count; ++i)
                    {
                        sum += _stat[i].Actions;
                    }
                    return sum + ActionsBefore;
                }
            }
        }

        struct User
        {
            public int CategoryActions;
            public int TemplateActions;
            public int ArticleActions;
            public int FileActions;
            public string Name;

            public User(string name)
            {
                Name = name;
                FileActions = 0;
                CategoryActions = 0;
                TemplateActions = 0;
                ArticleActions = 0;
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
