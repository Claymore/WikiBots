using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using AutoeditorsWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.AutoeditorsWikiBot
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
            Regex sysopRE = new Regex(@"\[\[(User|Участник|user|участник):(.+?)\|.+?]\]");
            Regex userRE = new Regex(@"{{user\|(.+)}}");
            Regex timeRE = new Regex(@"(\d{1,2}:\d{2}\, \d\d? [а-я]+ \d{4})( \(UTC\))?");
            string text;
            try
            {
                text = wiki.LoadText("Википедия:Присвоение флага автопатрулируемого");
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            string[] lines = text.Split(new char[] { '\n' });
            List<LogEvent> entries = new List<LogEvent>();
            for (int i = 0; i < lines.Length; ++i)
            {
                if (lines[i].StartsWith("|-"))
                {
                    LogEvent logEntry = new LogEvent();
                    Match m = userRE.Match(lines[i + 2]);
                    logEntry.User = m.Groups[1].Value;
                    string line = lines[i + 3];
                    m = sysopRE.Match(lines[i + 3]);
                    if (!m.Success)
                    {
                    }
                    logEntry.Sysop = m.Groups[2].Value;
                    
                    m = timeRE.Match(lines[i + 3]);
                    logEntry.Timestamp = DateTime.Parse(m.Groups[1].Value,
                        CultureInfo.CreateSpecificCulture("ru-RU"),
                        DateTimeStyles.AssumeUniversal);
                    logEntry.Comment = lines[i + 4].Substring(1).Trim();
                    logEntry.Grayed = lines[i].Contains("color:grey");
                    entries.Add(logEntry);
                    i = i + 4;
                }
            }

            ParameterCollection parameters = new ParameterCollection
            {
                { "list", "logevents" },
                { "letype", "rights" },
                { "lelimit", "max" },
                { "lestart", "2008-09-06T00:00:00Z" },
                { "ledir", "newer" }
            };
            XmlDocument doc;
            try
            {
                doc = wiki.Enumerate(parameters, true);
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            XmlNodeList autoeditors = doc.SelectNodes("//rights[@new=\"autoeditor\"]");
            XmlNodeList editors = doc.SelectNodes("//rights[@new=\"editor, rollbacker\"]");

            foreach (XmlNode user in autoeditors)
            {
                string name = user.ParentNode.Attributes["title"].Value.Substring("Участник:".Length);
                string sysop = user.ParentNode.Attributes["user"].Value;
                string comment = user.ParentNode.Attributes["comment"].Value;
                DateTime timestamp = DateTime.Parse(user.ParentNode.Attributes["timestamp"].Value,
                    null, DateTimeStyles.AssumeUniversal);
                if (entries.Count(e => e.User.ToLower() == name.ToLower()) == 0)
                {
                    LogEvent logEntry = new LogEvent();
                    logEntry.User = name;
                    logEntry.Sysop = sysop;
                    logEntry.Timestamp = timestamp;
                    logEntry.Comment = comment;
                    logEntry.Grayed = false;
                    entries.Add(logEntry);
                }
            }

            foreach (XmlNode user in editors)
            {
                string name = user.ParentNode.Attributes["title"].Value.Substring("Участник:".Length);
                LogEvent entry = entries.Find(e => e.User.ToLower() == name.ToLower());
                if (entry != null)
                {
                    entry.Grayed = true;
                }
            }

            entries.Sort(CompareLogEvents);

            using (StreamWriter sw =
                        new StreamWriter("output.txt"))
            {
                sw.WriteLine("== Журнал ==");
                sw.WriteLine("{|class=\"wikitable\" style=\"width: 100%\"");
                sw.WriteLine("! &nbsp;");
                sw.WriteLine("! Участник");
                sw.WriteLine("! Присвоивший&nbsp;администратор");
                sw.WriteLine("! Комментарий");
                int index = 1;
                foreach (LogEvent entry in entries)
                {
                    string time = entry.Timestamp.ToUniversalTime().ToString("HH:mm, d MMMM yyyy (UTC)");
                    sw.WriteLine(entry.Grayed ? "|-style=\"color:grey;background:#eee\"" : "|-");
                    sw.WriteLine("| " + index.ToString());
                    sw.WriteLine("| {{user|" + (entry.User.Contains("=") ? "1=" : "") + entry.User + "}}");
                    sw.WriteLine("| [[User:" + entry.Sysop + "|]]&nbsp;" + time);
                    sw.WriteLine("| " + entry.Comment);
                    ++index;
                }
                sw.WriteLine("|}\n");
                sw.WriteLine("[[Категория:Википедия:Патрулирование]]");
            }

            using (TextReader sr = new StreamReader("output.txt"))
            {
                text = sr.ReadToEnd();
                try
                {
                    wiki.SaveSection("Википедия:Присвоение флага автопатрулируемого", "1", text, "обновление");
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine(e.Message);
                    return;
                }
            }

            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        static int CompareLogEvents(LogEvent x, LogEvent y)
        {
            return x.Timestamp.CompareTo(y.Timestamp);
        }
    }

    class LogEvent
    {
        public string User;
        public string Sysop;
        public string Comment;
        public DateTime Timestamp;
        public bool Grayed;

        public override string ToString()
        {
            return User;
        }
    }
}
