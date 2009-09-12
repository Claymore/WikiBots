using System;
using System.IO;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewTemplates : NewPages
    {
        public NewTemplates(string category, string page, int pageLimit, string format)
            : base(category, page, pageLimit, format, "Cache\\output-templates-" + category + ".txt",
                   "Cache\\input-templates-" + category + "-previous.txt", true)
        {
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
                        
                        string line;
                        while ((line = streamReader.ReadLine()) != null)
                        {
                            string[] groups = line.Split(new char[] { '\t' });
                            if (groups[0] == "10")
                            {
                                string title = groups[1].Replace('_', ' ');
                                streamWriter.WriteLine(string.Format(Format,
                                    title));
                                ++index;
                            }
                            if (index >= PageLimit)
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
