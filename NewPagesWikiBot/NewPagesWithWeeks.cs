using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithWeeks : NewPages
    {
        public string TimeFormat { get; private set; }

        public NewPagesWithWeeks(string category, string page, string format, string timeFormat, string top, string bottom)
            : base(category, page, 0, format)
        {
            TimeFormat = timeFormat;
            Hours = 192;
            Head = top;
            Bottom = bottom;
        }

        public override void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + Category);
            List<NewPage> newPages = new List<NewPage>();
            using (TextReader streamReader = new StreamReader("Cache\\input-" + Category + ".txt"))
            {
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("prop", "revisions");
                parameters.Add("rvprop", "timestamp|user");
                parameters.Add("rvdir", "newer");
                parameters.Add("rvlimit", "1");
                parameters.Add("redirects");

                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == "0")
                    {
                        string title = groups[1].Replace('_', ' ');

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
                            newPages.Add(new NewPage(title, time, user));
                        }
                    }
                }
            }

            Directory.CreateDirectory("Cache\\" + Category);
            for (int i = 0; i < 7; ++i)
            {
                DateTime end = DateTime.Today.AddDays(1 - i);
                DateTime start = DateTime.Today.AddDays(-i);
                string filename = string.Format("{0}.txt", start.ToString("d MMMM yyyy"));
                using (TextWriter streamWriter = new StreamWriter("Cache\\" + Category + "\\" + filename))
                {
                    streamWriter.WriteLine("<noinclude>" + Head + "</noinclude>");
                    List<NewPage> pages = new List<NewPage>(newPages.Where(p => p.Time.ToUniversalTime() >= start && p.Time.ToUniversalTime() < end));
                    pages.Sort(CompareTime);
                    foreach (NewPage page in pages)
                    {
                        streamWriter.WriteLine(string.Format(Format,
                                page.Name, page.Author, page.Time.ToUniversalTime().ToString(TimeFormat)));
                    }
                    streamWriter.WriteLine("<noinclude>" + Bottom + "</noinclude>");
                }

                using (TextReader sr = new StreamReader("Cache\\" + Category + "\\" + filename))
                {
                    string text = sr.ReadToEnd();
                    if (string.IsNullOrEmpty(text))
                    {
                        Console.Out.WriteLine("Skipping " + Category + "/" + filename);
                        return;
                    }
                    Console.Out.WriteLine("Updating " + Category + "/" + filename);
                    wiki.SavePage(Page + "/" + start.ToString("d MMMM yyyy"),
                        "0",
                        text,
                        "обновление",
                        MinorFlags.Minor,
                        CreateFlags.None,
                        WatchFlags.None,
                        SaveFlags.Replace);
                }
            }
        }

        public override void UpdatePage(Wiki wiki)
        {
        }

        private static int CompareTime(NewPage x, NewPage y)
        {
            return x.Time.CompareTo(y.Time);
        }
    }
}
