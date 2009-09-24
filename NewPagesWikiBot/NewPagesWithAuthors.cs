using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithAuthors : NewPages
    {
        public string TimeFormat { get; private set; }

        public NewPagesWithAuthors(string category, string page, int pageLimit, string format, string timeFormat)
            : base(new string[] { category },
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt",
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, string page, string output, int pageLimit, string format, string timeFormat)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, string page, string output, int pageLimit, string format, string timeFormat, string delimeter)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   delimeter,
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, IEnumerable<string> categoriesToIgnore,
            string page, string output, int pageLimit, string format, string timeFormat, string delimeter)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   delimeter,
                   false,
                   categoriesToIgnore)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(string category, string page, int pageLimit, string format, string timeFormat, string delimeter)
            : base(new string[] { category },
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt",
                   delimeter,
                   false)
        {
            TimeFormat = timeFormat;
        }


        public override void ProcessData(Wiki wiki)
        {
            HashSet<string> ignore = new HashSet<string>();
            foreach (var category in CategoriesToIgnore)
            {
                using (TextReader streamReader = new StreamReader("Cache\\input-" + category + ".txt"))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] groups = line.Split(new char[] { '\t' });
                        if (groups[0] == "0")
                        {
                            string title = groups[1].Replace('_', ' ');
                            ignore.Add(title);
                        }
                    }
                }
            }

            List<string> pages = new List<string>();
            int index = 0;
            
            HashSet<string> currentPages = new HashSet<string>();
            foreach (var category in Categories)
            {
                Console.Out.WriteLine("Processing data of " + category);

                using (TextReader streamReader = new StreamReader("Cache\\input-" + category + ".txt"))
                {
                    ParameterCollection parameters = new ParameterCollection();
                    parameters.Add("prop", "revisions");
                    parameters.Add("rvprop", "timestamp|user");
                    parameters.Add("rvdir", "newer");
                    parameters.Add("rvlimit", "1");
                    parameters.Add("redirects");

                    string line;
                    while ((line = streamReader.ReadLine()) != null && index < PageLimit)
                    {
                        string[] groups = line.Split(new char[] { '\t' });
                        if (groups[0] == "0")
                        {
                            string title = groups[1].Replace('_', ' ');
                            if (ignore.Contains(title))
                            {
                                continue;
                            }
                            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
                            XmlNode node = xml.SelectSingleNode("//rev");
                            if (node != null)
                            {
                                title = xml.SelectSingleNode("//page").Attributes["title"].Value;
                                string user = node.Attributes["user"].Value;
                                string timestamp = node.Attributes["timestamp"].Value;
                                DateTime time = DateTime.Parse(timestamp,
                                    null,
                                    DateTimeStyles.AssumeUniversal);
                                string result = string.Format(Format,
                                    title, user, time.ToUniversalTime().ToString(TimeFormat));
                                if (!currentPages.Contains(result))
                                {
                                    currentPages.Add(result);
                                    pages.Add(result);
                                    ++index;
                                }
                            }
                        }
                    }
                }
            }

            using (TextWriter streamWriter = new StreamWriter(Output))
            {
                streamWriter.Write(string.Join(Delimeter, pages.ToArray()));
            }
        }
    }
}
