using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithImages : NewPages
    {
        private Regex _regex;

        public NewPagesWithImages(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string page,
                        int depth,
                        int hours,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer)
            : base(module,
                   categories,
                   categoriesToIgnore,
                   page,
                   depth,
                   hours,
                   maxItems,
                   format,
                   delimeter,
                   header,
                   footer)
        {
            Format = Format.Replace("%(имя файла)", "{1}");
            _regex = new Regex(@"\| *image file *= *(?'fileName'.+?) *\n");
        }

        public NewPagesWithImages(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string page,
                        int depth,
                        int hours,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer,
                        Regex regex)
            : base(module,
                   categories,
                   categoriesToIgnore,
                   page,
                   depth,
                   hours,
                   maxItems,
                   format,
                   delimeter,
                   header,
                   footer)
        {
            Format = Format.Replace("%(имя файла)", "{1}");
            _regex = regex;
        }

        public override string ProcessCategory(Wiki wiki, string text)
        {
            var result = new List<string>();
            var pages = new HashSet<string>();

            HashSet<string> ignore = new HashSet<string>();
            foreach (var category in CategoriesToIgnore)
            {
                string fileName = "Cache\\" + Module.Language + "\\NewPages\\" + Cache.EscapePath(category) + ".txt";
                using (TextReader streamReader = new StreamReader(fileName))
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

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("redirects");
            parameters.Add("prop", "revisions");
            parameters.Add("rvprop", "content");
            parameters.Add("rvsection", "0");

            string file = "Cache\\" + Module.Language + "\\NewPages\\" + Cache.EscapePath(Categories[0]) + ".txt";
            Console.Out.WriteLine("Processing data of " + Categories[0]);
            using (TextReader streamReader = new StreamReader(file))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
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
                            string content = node.FirstChild == null ? "" : node.FirstChild.Value;
                            Match m = _regex.Match(content);
                            if (m.Success)
                            {
                                string fileName = m.Groups["fileName"].Value.Trim();
                                if (string.IsNullOrEmpty(fileName))
                                {
                                    continue;
                                }
                                result.Add(string.Format(Format,
                                    title, fileName));
                            }
                        }
                    }
                    if (result.Count == MaxItems)
                    {
                        break;
                    }
                }
            }

            if (result.Count < MaxItems)
            {
                string oldText = text;
                if (!string.IsNullOrEmpty(Header) && text.StartsWith(Header))
                {
                    oldText = oldText.Substring(Header.Length);
                }
                if (!string.IsNullOrEmpty(Footer) && oldText.EndsWith(Footer))
                {
                    oldText = oldText.Substring(0, oldText.Length - Footer.Length);
                }
                string[] items = oldText.Split(new string[] { Delimeter },
                       StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < items.Length && result.Count < MaxItems; ++i)
                {
                    if (!result.Exists(l => l == items[i]))
                    {
                        result.Add(items[i]);
                    }
                }
            }

            return Header + string.Join(Delimeter, result.ToArray()) + Footer;
        }
    }
}
