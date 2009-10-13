using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithWeeks : NewPages
    {
        public NewPagesWithWeeks(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string page,
                        int ns,
                        int depth,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer)
            : base(module,
                   categories,
                   categoriesToIgnore,
                   page,
                   ns,
                   depth,
                   192,
                   maxItems,
                   format,
                   delimeter,
                   header,
                   footer)
        {
        }

        public Dictionary<string, string> Process(Wiki wiki)
        {
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
                        if (groups[0] == Namespace.ToString())
                        {
                            string title = groups[1].Replace('_', ' ');
                            ignore.Add(title);
                        }
                    }
                }
            }

            var pageList = new List<Cache.PageInfo>();
            var pages = new HashSet<string>();
            foreach (var category in Categories)
            {
                string fileName = "Cache\\" + Module.Language + "\\NewPages\\" + Cache.EscapePath(category) + ".txt";
                Console.Out.WriteLine("Processing data of " + category);
                using (TextReader streamReader = new StreamReader(fileName))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] groups = line.Split(new char[] { '\t' });
                        if (groups[0] == Namespace.ToString())
                        {
                            string title = groups[1].Replace('_', ' ');
                            if (ignore.Contains(title))
                            {
                                continue;
                            }
                            if (Namespace != 0)
                            {
                                title = wiki.GetNamespace(Namespace) + ":" + title;
                            }
                            Cache.PageInfo page = Cache.LoadPageInformation(wiki, Module.Language, title);
                            if (page != null && !pages.Contains(page.Title))
                            {
                                pages.Add(page.Title);
                                pageList.Add(page);
                            }
                        }
                    }
                }
            }

            pageList.Sort(CompareTime);

            Dictionary<string, string> wikiPages = new Dictionary<string, string>();
            for (int i = 0; i < 7; ++i)
            {
                DateTime end = DateTime.Today.AddDays(1 - i);
                DateTime start = DateTime.Today.AddDays(-i);

                var subset = new List<Cache.PageInfo>(pageList.Where(p =>
                    p.FirstEdit.ToUniversalTime() >= start &&
                    p.FirstEdit.ToUniversalTime() < end));

                var result = new List<string>();
                foreach (var el in subset)
                {
                    result.Add(string.Format(Format,
                        Namespace != 0 ? el.Title.Substring(wiki.GetNamespace(Namespace).Length + 1) : el.Title,
                        el.Author,
                        el.FirstEdit.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")));
                }
                if (result.Count != 0)
                {
                    string pageName = Page + "/" + start.ToString("d MMMM yyyy");
                    string resultText = Header + string.Join(Delimeter, result.ToArray()) + Footer;
                    wikiPages.Add(pageName, resultText);
                }
            }

            return wikiPages; 
        }

        public override void Update(Wiki wiki)
        {
            WebClient client = new WebClient();
            foreach (var category in Categories)
            {
                Cache.LoadPageList(client, category, Module.Language, Depth, Hours);
            }

            foreach (var category in CategoriesToIgnore)
            {
                Cache.LoadPageList(client, category, Module.Language, Depth, Hours);
            }

            Dictionary<string, string> pages = Process(wiki);
            foreach (var page in pages)
            {
                Console.Out.WriteLine("Updating " + page.Key);
                wiki.Save(page.Key, page.Value, Module.UpdateComment);
            }
        }

        private static int CompareTime(Cache.PageInfo x, Cache.PageInfo y)
        {
            return x.FirstEdit.CompareTo(y.FirstEdit);
        }
    }
}
