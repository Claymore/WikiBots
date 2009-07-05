using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    class NewCategories : NewPages
    {
        public NewCategories(string category, string page, int pageLimit, string format)
            : base(category, page, pageLimit, format, "Cache\\output-categories-" + category + ".txt",
                   "Cache\\input-categories-" + category + "-previous.txt")
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
                    if (groups[0] == "14")
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
