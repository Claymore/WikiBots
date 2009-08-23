using System;
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
            : base(category, page, pageLimit, format, false)
        {
            TimeFormat = timeFormat;
        }

        public override void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + Category);
            using (TextWriter streamWriter = new StreamWriter("Cache\\output-" + Category + ".txt"))
            using (TextReader streamReader = new StreamReader("Cache\\input-" + Category + ".txt"))
            {
                ParameterCollection parameters = new ParameterCollection();
                parameters.Add("prop", "revisions");
                parameters.Add("rvprop", "timestamp|user");
                parameters.Add("rvdir", "newer");
                parameters.Add("rvlimit", "1");
                parameters.Add("redirects");

                int index = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null && index < PageLimit)
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
                            streamWriter.WriteLine(string.Format(Format,
                                title, user, time.ToUniversalTime().ToString(TimeFormat)));
                            ++index;
                        }
                    }
                }
            }
        }
    }
}
