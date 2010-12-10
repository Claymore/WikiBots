using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;
using RequestsForArbitrationListWikiBot.Properties;

namespace RequestsForArbitrationListWikiBot
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

            Dictionary<string, int> requestNumbers = new Dictionary<string, int>();
            string text = wiki.LoadText("Википедия:Заявки на арбитраж/Все страницы");
            StringReader reader = new StringReader(text);
            Regex re = new Regex(@" \[\[ВП:(\d+)\]\] — \[\[(.+?)\|");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Match m = re.Match(line);
                if (m.Success)
                {
                    int number = int.Parse(m.Groups[1].Value);
                    string page = m.Groups[2].Value;
                    requestNumbers.Add(page, number);
                }
            }

            var parameters = new ParameterCollection
            {
                {"list" , "categorymembers"},
                {"cmtitle", "Категория:Википедия:Заявки в Арбитражный комитет"},
                {"cmlimit", "max"},
            };

            List<ArbComEntry> entries = new List<ArbComEntry>();
            var doc = wiki.Enumerate(parameters, true);
            foreach (XmlNode page in doc.SelectNodes("//cm"))
            {
                parameters = new ParameterCollection
                {
                    {"list" , "backlinks"},
                    {"bltitle", page.Attributes["title"].Value},
                    {"bllimit", "max"},
                    {"blfilterredir", "redirects"},
                };

                int number = int.MinValue;
                if (requestNumbers.ContainsKey(page.Attributes["title"].Value))
                {
                    number = requestNumbers[page.Attributes["title"].Value];
                }
                else
                {
                    XmlDocument xml = wiki.Enumerate(parameters, true);
                    foreach (XmlNode link in xml.SelectNodes("//bl"))
                    {
                        if (link.Attributes["title"].Value.StartsWith("Википедия:"))
                        {
                            if (int.TryParse(link.Attributes["title"].Value.Replace("Википедия:", ""), out number))
                            {
                                break;
                            }
                            else
                            {
                                number = int.MinValue;
                            }
                        }
                    }
                }
                ArbComEntry entry = new ArbComEntry();
                entry.Number = number;
                entry.Page = page.Attributes["title"].Value;
                entries.Add(entry);
            }

            string prefix = "Википедия:Заявки на арбитраж/";
            StringBuilder sb = new StringBuilder("== Заявки ==\n");
            entries.Sort(CompareEntries);
            foreach (ArbComEntry entry in entries)
            {
                string requestNumber = "";
                if (entry.Number != int.MinValue)
                {
                    requestNumber = string.Format("[[ВП:{0}]] — ", entry.Number);
                }
                string title = entry.Page.Substring(prefix.Length);
                sb.AppendFormat("* {2}[[{1}{0}|{0}]] [[Обсуждение Википедии:Заявки на арбитраж/{0}|±]] ([[{1}{0}/Дискуссия арбитров|д]])\n", title, prefix, requestNumber);
            }
            text = sb.ToString();
            wiki.SaveSection("Википедия:Заявки на арбитраж/Все страницы", "1", text, "/* Заявки */ обновление");
        }

        static int CompareEntries(ArbComEntry x, ArbComEntry y)
        {
            if (y.Number == x.Number)
            {
                return x.Page.CompareTo(y.Page);
            }
            return y.Number.CompareTo(x.Number);
        }
    }

    struct ArbComEntry
    {
        public int Number;
        public string Page;
    }
}
