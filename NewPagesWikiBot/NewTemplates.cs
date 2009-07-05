using System;
using System.IO;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewTemplates : NewPages
    {
        public NewTemplates(string category, string page, int pageLimit, string format)
            : base(category, page, pageLimit, format, "Cache\\output-templates-" + category + ".txt",
                   "Cache\\input-templates-" + category + "-previous.txt")
        {
        }

        public override void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + Category);
            using (TextWriter streamWriter = new StreamWriter(Output))
            using (TextReader streamReader = new StreamReader("Cache\\input-" + Category + ".txt"))
            {
                int index = 0;
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
