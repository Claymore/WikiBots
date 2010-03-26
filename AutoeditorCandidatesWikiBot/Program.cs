using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Claymore.AutoeditorCandidatesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.AutoeditorCandidatesWikiBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            try
            {
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + wiki.User + ".");

            var parameters = new ParameterCollection
            {
                {"list", "recentchanges"},
                {"rcnamespace", "0"},
                {"rcprop", "user"},
                {"rcshow", "patrolled|!bot|!anon|!redirect"},
                {"rclimit", "2500"},
                {"rctype", "edit|new"},
            };

            if (!File.Exists("DoNotCheck.txt"))
            {
                FileStream fs = File.Create("DoNotCheck.txt");
                fs.Close();
            }

            HashSet<string> checkedUsers = new HashSet<string>();
            using (StreamReader sr = new StreamReader("DoNotCheck.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    checkedUsers.Add(line);
                }
            }

            HashSet<string> users = new HashSet<string>();
            XmlDocument doc = wiki.Enumerate(parameters, false);
            foreach (XmlNode edit in doc.SelectNodes("//rc"))
            {
                string userName = edit.Attributes["user"].Value;
                if (!checkedUsers.Contains(userName) && !users.Contains(userName))
                {
                    users.Add(userName);
                }
            }

            parameters = new ParameterCollection
            {
                {"list", "users"},
                {"usprop", "groups|editcount|registration"},
            };

            Dictionary<string, UserInfo> candidates = new Dictionary<string, UserInfo>();
            int limit = 500;
            StringBuilder usersString = new StringBuilder();
            int index = 0;
            foreach (string user in users)
            {
                if (index < limit)
                {
                    usersString.Append("|" + user);
                    ++index;
                }
                else
                {
                    usersString.Remove(0, 1);
                    ParameterCollection localParameters = new ParameterCollection(parameters);
                    localParameters.Add("ususers", usersString.ToString());
                    doc = wiki.Enumerate(localParameters, true);

                    using (StreamWriter sw = new StreamWriter("DoNotCheck.txt", true))
                    {
                        FillInCandidates(candidates, doc.SelectNodes("//user"), sw);
                    }

                    index = 1;
                    usersString = new StringBuilder("|" + user);
                }
            }
            if (index > 0)
            {
                usersString.Remove(0, 1);
                ParameterCollection localParameters = new ParameterCollection(parameters);
                localParameters.Add("ususers", usersString.ToString());
                doc = wiki.Enumerate(localParameters, true);

                using (StreamWriter sw = new StreamWriter("DoNotCheck.txt", true))
                {
                    FillInCandidates(candidates, doc.SelectNodes("//user"), sw);
                }
            }

            StringBuilder sb = new StringBuilder("== Список ==\n");
            using (StreamWriter sw = new StreamWriter("DoNotCheck.txt", true))
            {
                foreach (var candidate in candidates)
                {
                    parameters = new ParameterCollection
                    {
                        {"list", "logevents"},
                        {"leprop", "timestamp"},
                        {"letype", "block"},
                        {"letitle", "User:" + candidate.Key},
                    };
                    doc = wiki.Enumerate(parameters, true);
                    bool wasBlocked = doc.SelectSingleNode("//item") != null;
                    if (wasBlocked)
                    {
                        sw.WriteLine(candidate.Key);
                        continue;
                    }

                    parameters = new ParameterCollection
                    {
                        {"list", "usercontribs"},
                        {"uclimit", "500"},
                        {"ucnamespace", "0"},
                        {"ucprop", "patrolled"},
                        {"ucuser", candidate.Key},
                    };
                    doc = wiki.Enumerate(parameters, false);
                    int totalEdits = doc.SelectNodes("//item").Count;
                    int patrolledEdits = doc.SelectNodes("//item[@patrolled]").Count;

                    Console.Out.WriteLine("{0}: {1} from {2} (was blocked? {3})",
                        candidate.Key,
                        patrolledEdits,
                        totalEdits,
                        wasBlocked ? "Yes" : "No");

                    if (patrolledEdits > 5 && totalEdits == 500)
                    {
                        sb.AppendFormat("* {{{{userlinks|{0}}}}}, всего правок: {1}, дата регистрации — {2}, отпатрулировано правок: {3}\n",
                            candidate.Key,
                            candidate.Value.Edits,
                            candidate.Value.Registration == DateTime.MinValue
                              ? "— дата неизвестна"
                              : candidate.Value.Registration.ToUniversalTime().ToLongDateString(),
                            patrolledEdits);
                    }
                }
            }
            Console.Out.WriteLine("Википедия:Заявки на статус автопатрулируемого/Кандидаты...");
            wiki.SaveSection("Википедия:Заявки на статус автопатрулируемого/Кандидаты", "1", sb.ToString(), "обновление");
        }

        private static void FillInCandidates(Dictionary<string, UserInfo> candidates, XmlNodeList nodes, StreamWriter writer)
        {
            foreach (XmlNode userNode in nodes)
            {
                string userName = userNode.Attributes["name"].Value;
                int edits = int.Parse(userNode.Attributes["editcount"].Value);

                XmlNode editorNode = userNode.SelectSingleNode("//user[@name=" + EscapeXPathQuery(userName) + "]/groups[g='editor' or g='autoeditor' or g='sysop']/g");
                if (editorNode != null)
                {
                    writer.WriteLine(userName);
                }
                else if (edits > 500 && !candidates.ContainsKey(userName))
                {
                    UserInfo userInfo = new UserInfo();
                    userInfo.User = userName;
                    userInfo.Edits = edits;
                    if (!string.IsNullOrEmpty(userNode.Attributes["registration"].Value))
                    {
                        userInfo.Registration = DateTime.Parse(userNode.Attributes["registration"].Value,
                            null,
                            System.Globalization.DateTimeStyles.AssumeUniversal);
                    }
                    candidates.Add(userName, userInfo);
                }
            }
        }

        private static string EscapeXPathQuery(string query)
        {
            char[] quoteChars = new char[] { '\'', '"' };
            int index = query.IndexOfAny(quoteChars);
            if (index == -1)
            {
                return "'" + query + "'";
            }
            else
            {
                string searchString = query;
                StringBuilder result = new StringBuilder("concat(");
                while (index != -1)
                {
                    result.Append("'" + searchString.Substring(0, index) + "', ");
                    if (searchString[index] == '\'')
                    {
                        result.Append("\"'\", ");
                    }
                    else
                    {
                        result.Append("'\"', ");
                    }
                    searchString = searchString.Substring(index + 1);
                    index = searchString.IndexOfAny(quoteChars);
                }
                if (string.IsNullOrEmpty(searchString))
                {
                    result.Remove(result.Length - 2, 2);
                    return result.ToString() + ")";
                }
                else
                {
                    result.Append("'" + searchString + "')");
                    return result.ToString();
                }
            }
        }
    }

    struct UserInfo
    {
        public string User;
        public int Edits;
        public DateTime Registration;
    }
}
