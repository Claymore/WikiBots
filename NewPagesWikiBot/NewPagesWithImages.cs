using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithImages : NewPages
    {
        private Regex _regex;
        
        public NewPagesWithImages(string category, string page, int pageLimit, string format, string top, string bottom)
            : base(category, page, pageLimit, format)
        {
            Head = top;
            Bottom = bottom;
            _regex = new Regex(@"\| *image file *= *(?'fileName'.+?) *\n");
        }

        public NewPagesWithImages(string category, string page, int pageLimit, string format, string top, string bottom, Regex regex)
            : base(category, page, pageLimit, format)
        {
            Head = top;
            Bottom = bottom;
            _regex = regex;
        }

        public override void ProcessData(Wiki wiki)
        {
            int index = 0;
            using (TextWriter streamWriter = new StreamWriter(Output))
            {
                foreach (var category in Categories)
                {
                    Console.Out.WriteLine("Processing data of " + category);

                    using (TextReader streamReader = new StreamReader("Cache\\input-" + category + ".txt"))
                    {
                        ParameterCollection parameters = new ParameterCollection();
                        parameters.Add("redirects");
                        parameters.Add("prop", "revisions");
                        parameters.Add("rvprop", "content");
                        parameters.Add("rvsection", "0");

                        Console.Out.WriteLine("Quering author information for " + category);

                        streamWriter.WriteLine(Head);

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
                                    string content = node.FirstChild == null ? "" : node.FirstChild.Value;
                                    Match m = _regex.Match(content);
                                    if (m.Success)
                                    {
                                        string file = m.Groups["fileName"].Value.Trim();
                                        if (string.IsNullOrEmpty(file))
                                        {
                                            continue;
                                        }
                                        streamWriter.WriteLine(string.Format(Format,
                                            title, file));
                                        ++index;
                                    }
                                }
                            }
                            if (index >= PageLimit)
                            {
                                break;
                            }
                        }
                        streamWriter.WriteLine(Bottom);
                    }
                }
            }
        }
    }
}
