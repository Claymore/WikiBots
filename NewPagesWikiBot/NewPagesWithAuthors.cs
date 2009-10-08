using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Linq;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithAuthors : NewPages
    {
        public string TimeFormat { get; private set; }

        public NewPagesWithAuthors(string category, string page, int pageLimit, string format, string timeFormat)
            : base(new string[] { category },
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt",
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, string page, string output, int pageLimit, string format, string timeFormat)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, string page, string output, int pageLimit, string format, string timeFormat, string delimeter)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   delimeter,
                   false)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(IEnumerable<string> categories, IEnumerable<string> categoriesToIgnore,
            string page, string output, int pageLimit, string format, string timeFormat, string delimeter)
            : base(categories,
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + output + ".txt",
                   "Cache\\input-" + output + "-previous.txt",
                   delimeter,
                   false,
                   categoriesToIgnore)
        {
            TimeFormat = timeFormat;
        }

        public NewPagesWithAuthors(string category, string page, int pageLimit, string format, string timeFormat, string delimeter)
            : base(new string[] { category },
                   page,
                   pageLimit,
                   format,
                   "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt",
                   delimeter,
                   false)
        {
            TimeFormat = timeFormat;
        }


        public override void ProcessData(Wiki wiki)
        {
            HashSet<string> ignore = new HashSet<string>();
            foreach (var category in CategoriesToIgnore)
            {
                using (TextReader streamReader = new StreamReader("Cache\\input-" + category + ".txt"))
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

            var pages = new List<Cache.PageInfo>();
            HashSet<string> currentPages = new HashSet<string>();
            foreach (var category in Categories)
            {
                Console.Out.WriteLine("Processing data of " + category);

                using (TextReader streamReader = new StreamReader("Cache\\input-" + category + ".txt"))
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
                            Cache.PageInfo page = Cache.LoadPageInformation(wiki, title);
                            if (page != null && !currentPages.Contains(page.Title))
                            {
                                currentPages.Add(page.Title);
                                pages.Add(page);
                            }
                        }
                    }
                }
            }

            pages.Sort(ComparePages);

            int count = pages.Count < PageLimit ? pages.Count : PageLimit;
            string[] subset = new string[count];

            int index = 0;
            foreach (var el in pages.Take(count))
            {
                string result = string.Format(Format,
                                    el.Title,
                                    el.Author,
                                    el.FirstEdit.ToUniversalTime().ToString(TimeFormat));
                subset[index++] = result;
            }

            using (TextWriter streamWriter = new StreamWriter(Output))
            {
                streamWriter.Write(string.Join(Delimeter, subset));
            }
        }

        private int ComparePages(Cache.PageInfo x, Cache.PageInfo y)
        {
            return y.FirstEdit.CompareTo(x.FirstEdit);
        }
    }
}
