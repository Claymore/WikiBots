using System.Collections.Generic;
using System.IO;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithProcessing : NewPages
    {
        public string Template { get; private set; }
        public NewPagesWithProcessing(string category, string page, int pageLimit, string format, string template, bool skip)
            : base(category, page, pageLimit, format, skip)
        {
            Template = template;
        }

        public override bool UpdatePage(Wiki wiki)
        {
            bool result = base.UpdatePage(wiki);
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("list", "embeddedin");
            parameters.Add("einamespace", "1");
            parameters.Add("eilimit", "max");
            parameters.Add("eititle", "Шаблон:" + Template);
            XmlDocument xml = wiki.Enumerate(parameters, true);

            List<string> titles = new List<string>();
            using (TextReader streamReader = new StreamReader("Cache\\input-" + Category + ".txt"))
            {
                int index = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups[0] == "0")
                    {
                        string title = groups[1].Replace('_', ' ');
                        titles.Add(title);
                        ++index;
                    }
                    if (index >= PageLimit)
                    {
                        break;
                    }
                }
            }

            parameters.Clear();
            parameters.Add("redirects");
            XmlDocument doc = wiki.Query(QueryBy.Titles, parameters, titles);
            foreach (XmlNode node in doc.SelectNodes("//page"))
            {
                string title = "Обсуждение:" + node.Attributes["title"].Value;
                if (xml.SelectSingleNode("//ei[@title='" + title + "']") == null)
                {
                    wiki.SavePage(title,
                                  "",
                                  "{{" + Template + "}}\n",
                                  "добавление шаблона",
                                  MinorFlags.Minor,
                                  CreateFlags.None,
                                  WatchFlags.None,
                                  SaveFlags.Prepend);
                }
            }
            return result;
        }
    }
}
