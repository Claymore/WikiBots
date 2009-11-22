using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPages : IPortalModule
    {
        private List<string> _categories;
        private List<string> _categoriesToIgnore;
        private HashSet<string> _usersToIgnore;
        public string Page { get; private set; }
        public string Format { get; protected set; }
        public string Header { get; private set; }
        public string Footer { get; private set; }
        public int MaxItems { get; private set; }
        protected int Hours { get; private set; }
        public string Delimeter { get; private set; }
        public PortalModule Module { get; private set; }
        public int Depth { get; private set; }
        public int Namespace { get; private set; }
        public bool MarkEdits { get; private set; }

        public IList<string> Categories
        {
            get { return _categories; }
        }

        public IList<string> CategoriesToIgnore
        {
            get { return _categoriesToIgnore; }
        }

        public HashSet<string> UsersToIgnore
        {
            get { return _usersToIgnore; }
        }

        public NewPages(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        IEnumerable<string> usersToIgnore,
                        string page,
                        int ns,
                        int depth,
                        int hours,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer,
                        bool markEdits)
        {
            _categories = new List<string>(categories);
            _categoriesToIgnore = new List<string>(categoriesToIgnore);
            _usersToIgnore = new HashSet<string>(usersToIgnore);
            Page = page;
            MaxItems = maxItems;
            Format = format.Replace("%(название)", "{0}").Replace("%(автор)", "{1}").Replace("%(дата)", "{2}");
            Hours = hours;
            Module = module;
            Delimeter = delimeter;
            Depth = depth;
            Header = header;
            Footer = footer;
            Namespace = ns;
            MarkEdits = markEdits;
        }

        public virtual string GetData(Wiki wiki)
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

            try
            {
                return wiki.LoadText(Page);
            }
            catch (WikiPageNotFound)
            {
                return "";
            }
        }

        public virtual string ProcessCategory(Wiki wiki, string text)
        {
            var pageList = new List<Cache.PageInfo>();
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
                        if (groups[0] == Namespace.ToString())
                        {
                            string title = groups[1].Replace('_', ' ');
                            ignore.Add(title);
                        }
                    }
                }
            }

            string file = "Cache\\" + Module.Language + "\\NewPages\\" + Cache.EscapePath(_categories[0]) + ".txt";
            Console.Out.WriteLine("Processing data of " + _categories[0]);
            using (TextReader streamReader = new StreamReader(file))
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
                        string fullTitle = title;
                        if (Namespace != 0)
                        {
                            fullTitle = wiki.GetNamespace(Namespace) + ":" + title;
                        }
                        Cache.PageInfo page = Cache.LoadPageInformation(wiki, Module.Language, fullTitle);
                        if (page != null &&
                            !UsersToIgnore.Contains(page.Author) &&
                            !pages.Contains(page.Title))
                        {
                            pages.Add(page.Title);
                            pageList.Add(page);
                        }
                    }
                    if (pageList.Count == MaxItems)
                    {
                        break;
                    }
                }
            }

            List<string> result = new List<string>(pageList.Select(p => string.Format(Format,
                Namespace != 0 ? p.Title.Substring(wiki.GetNamespace(Namespace).Length + 1) : p.Title,
                p.Author,
                p.FirstEdit.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"))));

            if (pageList.Count < MaxItems)
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

        public virtual string ProcessData(Wiki wiki, string text)
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
                            if (page != null &&
                                !UsersToIgnore.Contains(page.Author) &&
                                !pages.Contains(page.Title))
                            {
                                pages.Add(page.Title);
                                pageList.Add(page);
                            }
                        }
                    }
                }
            }

            pageList.Sort(ComparePages);

            int count = pages.Count < MaxItems ? pages.Count : MaxItems;
            var subset = new List<string>();

            foreach (var el in pageList.Take(count))
            {
                subset.Add(string.Format(Format,
                    Namespace != 0 ? el.Title.Substring(wiki.GetNamespace(Namespace).Length + 1) : el.Title,
                    el.Author,
                    el.FirstEdit.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")));
            }

            if (subset.Count < MaxItems)
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
                for (int i = 0; i < items.Length && subset.Count < MaxItems; ++i)
                {
                    if (!subset.Exists(l => l == items[i]))
                    {
                        subset.Add(items[i]);
                    }
                }
            }

            return Header + string.Join(Delimeter, subset.ToArray()) + Footer;
        }

        public virtual void Update(Wiki wiki)
        {
            string text = GetData(wiki);
            string newText = "";
            if (_categories.Count == 1)
            {
                newText = ProcessCategory(wiki, text);
            }
            else if (_categories.Count > 1)
            {
                newText = ProcessData(wiki, text);
            }
            if (!string.IsNullOrEmpty(newText) && newText != text)
            {
                Console.Out.WriteLine("Updating " + Page);
                wiki.Save(Page, newText, Module.UpdateComment, !MarkEdits ? MinorFlags.NotMinor : MinorFlags.None, MarkEdits);
            }
        }

        private int ComparePages(Cache.PageInfo x, Cache.PageInfo y)
        {
            return y.FirstEdit.CompareTo(x.FirstEdit);
        }
    }
}
