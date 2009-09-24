using System;
using System.Collections.Generic;
using System.IO;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithArchive : NewPagesWithAuthors
    {
        public string ArchivePage { get; private set; }

        public NewPagesWithArchive(string category, string page, string archive, int pageLimit, string format, string timeFormat)
            : base(category,
                   page,
                   pageLimit,
                   format,
                   timeFormat)
        {
            ArchivePage = archive;
        }

        public NewPagesWithArchive(IEnumerable<string> categories, string page, string archive, string output, int pageLimit, string format, string timeFormat)
            : base(categories,
                   page,
                   output,
                   pageLimit,
                   format,
                   timeFormat)
        {
            ArchivePage = archive;
        }

        public NewPagesWithArchive(IEnumerable<string> categories,
                                   IEnumerable<string> categoriesToIgnore,
                                   string page, string archive, string output, int pageLimit, string format, string timeFormat)
            : base(categories,
                   categoriesToIgnore,
                   page,
                   output,
                   pageLimit,
                   format,
                   timeFormat,
                   "\n")
        {
            ArchivePage = archive;
        }

        public override void ProcessData(Wiki wiki)
        {
            base.ProcessData(wiki);
            using (TextWriter streamWriter = new StreamWriter(Output + ".archive"))
            using (TextReader previousStream = new StreamReader(Previous))
            using (TextReader streamReader = new StreamReader(Output))
            {
                string text = streamReader.ReadToEnd();
                HashSet<string> currentPages = new HashSet<string>(text.Split(new string[] { "\n" },
                    System.StringSplitOptions.RemoveEmptyEntries));
                string line;
                while ((line = previousStream.ReadLine()) != null)
                {
                    if (!currentPages.Contains(line))
                    {
                        streamWriter.WriteLine(line);
                    }
                }
            }
        }

        public override bool UpdatePage(Wiki wiki)
        {
            if (!base.UpdatePage(wiki))
            {
                return false;
            }
            using (TextReader sr = new StreamReader(Output + ".archive"))
            {
                string text = sr.ReadToEnd();
                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Skipping " + ArchivePage);
                    return false;
                }
                Console.Out.WriteLine("Updating " + ArchivePage);
                wiki.SavePage(ArchivePage,
                    "",
                    text,
                    "обновление",
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Prepend);
                return true;
            }
        }
    }
}
