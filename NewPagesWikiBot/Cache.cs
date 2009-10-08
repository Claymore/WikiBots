using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using Claymore.SharpMediaWiki;
using System.Xml;

namespace Claymore.NewPagesWikiBot
{
    internal class Cache
    {
        internal class PageInfo
        {
            public string Title;
            public string Author;
            public DateTime FirstEdit;

            public PageInfo(string title, string author, DateTime firstEdit)
            {
                Title = title;
                Author = author;
                FirstEdit = firstEdit;
            }
        }

        public static PageInfo LoadPageInformation(Wiki wiki, string title)
        {
            if (!File.Exists(@"Cache\pages.txt"))
            {
                FileStream stream = File.Create(@"Cache\pages.txt");
                stream.Close();
            }

            using (TextReader streamReader = new StreamReader(@"Cache\pages.txt"))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == title)
                    {
                        DateTime time = DateTime.Parse(groups[2],
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                        return new PageInfo(groups[0], groups[1], time);
                    }
                }
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "revisions");
            parameters.Add("rvprop", "timestamp|user");
            parameters.Add("rvdir", "newer");
            parameters.Add("rvlimit", "1");
            parameters.Add("redirects");

            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
            XmlNode node = xml.SelectSingleNode("//rev");
            if (node != null)
            {
                string pageName = xml.SelectSingleNode("//page").Attributes["title"].Value;
                string user = node.Attributes["user"].Value;
                string timestamp = node.Attributes["timestamp"].Value;
                DateTime time = DateTime.Parse(timestamp,
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                using (TextWriter streamWriter = new StreamWriter(@"Cache\pages.txt", true))
                {
                    streamWriter.WriteLine("{0}\t{1}\t{2}",
                        pageName,
                        user,
                        timestamp);
                }
                return new PageInfo(pageName, user, time);
            }
            return null;
        }
    }
}
