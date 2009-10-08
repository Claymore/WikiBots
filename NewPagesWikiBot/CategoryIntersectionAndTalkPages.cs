using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class CategoryIntersectionAndTalkPages : CategoryTemplateIntersection
    {
        public string Prefix { get; private set; }

        public CategoryIntersectionAndTalkPages(string mainCategory,
            string category,
            string prefix,
            string directory,
            string page,
            string format)
            : base(mainCategory, category, directory, page, format)
        {
            Prefix = prefix;
        }

        public override void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + MainCategory);
            using (TextWriter streamWriter = new StreamWriter(_directory + "\\output-" + MainCategory + ".txt"))
            using (TextReader streamReader = new StreamReader(_directory + "\\input-" + MainCategory + ".txt"))
            {
                streamReader.ReadLine();
                streamReader.ReadLine();
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    string title = groups[0].Replace('_', ' ');

                    ParameterCollection parameters = new ParameterCollection();
                    parameters.Add("list", "backlinks");
                    parameters.Add("bltitle", title);
                    parameters.Add("blnamespace", "4");
                    parameters.Add("bllimit", "max");

                    List<DateTime> dates = new List<DateTime>();
                    XmlDocument xml = wiki.Enumerate(parameters, true);
                    foreach (XmlNode node in xml.SelectNodes("//bl"))
                    {
                        if (node.Attributes["title"].Value.StartsWith(Prefix))
                        {

                            string page = node.Attributes["title"].Value;
                            string dateString = page.Replace(Prefix, "");
                            DateTime date;
                            if (DateTime.TryParse(dateString, null,
                                System.Globalization.DateTimeStyles.AssumeUniversal, out date))
                            {
                                dates.Add(date);
                            }
                        }
                    }
                    if (dates.Count > 0)
                    {
                        DateTime talkDate = dates.Max();
                        string page = string.Format("{0}{1}", Prefix, talkDate.ToString("d MMMM yyyy"));
                        streamWriter.WriteLine(string.Format(Format, title, page, talkDate.ToString("d MMMM yyyy")));
                    }
                }
            }
        }
    }

    internal class PagesForDeletion : CategoryIntersectionAndTalkPages
    {
        public PagesForDeletion(string category, string page, string format)
            : base(category,
                   "К удалению",
                   "Википедия:К удалению/",
                    "Cache\\PagesForDeletion",
                    page,
                    format)
        {
        }
    }

    internal class PagesForCleanup : CategoryIntersectionAndTalkPages
    {
        public PagesForCleanup(string category, string page, string format)
            : base(category,
                   "К улучшению",
                   "Википедия:К улучшению/",
                    "Cache\\PagesForCleanup",
                    page,
                    format)
        {
        }
    }

    internal class PagesForMoving : CategoryIntersectionAndTalkPages
    {
        public PagesForMoving(string category, string page, string format)
            : base(category,
                   "К переименованию",
                   "Википедия:К переименованию/",
                    "Cache\\PagesForMoving",
                    page,
                    format)
        {
        }
    }

    internal class PagesForMerging : CategoryIntersectionAndTalkPages
    {
        public PagesForMerging(string category, string page, string format)
            : base(category,
                   "К объединению\namerge",
                   "Википедия:К объединению/",
                    "Cache\\PagesForMerging",
                    page,
                    format)
        {
        }
    }
}
