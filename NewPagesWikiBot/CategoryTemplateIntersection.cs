using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class CategoryTemplateIntersection : NewPages
    {
        public string Templates { get; private set; }
        public string OnEmpty { get; private set; }

        public CategoryTemplateIntersection(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string templates,
                        string page,
                        int depth,
                        int hours,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer,
                        string onEmpty)
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
            Templates = templates;
            OnEmpty = onEmpty;
        }

        public void GetData(WebClient client)
        {
            foreach (var category in Categories)
            {
                Cache.LoadPageList(client, category, Templates, Module.Language, Depth);
            }

            foreach (var category in CategoriesToIgnore)
            {
                Cache.LoadPageList(client, category, Templates, Module.Language, Depth);
            }
        }

        public virtual string ProcessData(Wiki wiki)
        {
            HashSet<string> ignore = new HashSet<string>();
            foreach (var category in CategoriesToIgnore)
            {
                string fileName = "Cache\\" + Module.Language + "\\PagesInCategoryWithTemplates\\" + Cache.EscapePath(category + "-" + Templates) + ".txt";
                using (TextReader streamReader = new StreamReader(fileName))
                {
                    streamReader.ReadLine();
                    streamReader.ReadLine();
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] groups = line.Split(new char[] { '\t' });
                        string title = groups[0].Replace('_', ' ');
                        ignore.Add(title);
                    }
                }
            }

            var pageList = new List<string>();
            var pages = new HashSet<string>();
            foreach (var category in Categories)
            {
                string fileName = "Cache\\" + Module.Language + "\\PagesInCategoryWithTemplates\\" + Cache.EscapePath(category + "-" + Templates) + ".txt";
                Console.Out.WriteLine("Processing data of " + category);
                using (TextReader streamReader = new StreamReader(fileName))
                {
                    streamReader.ReadLine();
                    streamReader.ReadLine();
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] groups = line.Split(new char[] { '\t' });
                        string title = groups[0].Replace('_', ' ');
                        if (ignore.Contains(title))
                        {
                            continue;
                        }
                        if (!pages.Contains(title))
                        {
                            pages.Add(title);
                            pageList.Add(title);
                        }
                    }
                }
            }

            var result = new List<string>();

            foreach (var el in pageList)
            {
                result.Add(string.Format(Format, el));
            }
            if (result.Count == 0)
            {
                return OnEmpty;
            }
            return Header + string.Join(Delimeter, result.ToArray()) + Footer;
        }

        public override void Update(Wiki wiki)
        {
            WebClient client = new WebClient();
            GetData(client);
            string newText = ProcessData(wiki);
            Console.Out.WriteLine("Updating " + Page);
            wiki.SavePage(Page,
                "",
                newText,
                Module.UpdateComment,
                MinorFlags.Minor,
                CreateFlags.None,
                WatchFlags.None,
                SaveFlags.Replace);
        }
    }
}
