using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPages : IPortalModule
    {
        private List<string> _categories;
        private List<string> _categoriesToIgnore;
        public string Page { get; private set; }
        public string Format { get; private set; }
        public string Head { get; set; }
        public string Bottom { get; set; }
        public int PageLimit { get; private set; }
        protected int Hours { get; set; }
        protected string Output { get; set; }
        protected string Previous { get; set; }
        public bool Skip { get; set; }
        public string Delimeter { get; private set; }

        public IEnumerable<string> Categories
        {
            get { return _categories; }
        }

        public IEnumerable<string> CategoriesToIgnore
        {
            get { return _categoriesToIgnore; }
        }

        public NewPages(string category, string page)
            : this(category, page, 20, "* [[{0}]]")
        {
        }

        public NewPages(string category, string page, int pageLimit, string format, bool skip)
            : this(category, page, pageLimit, format, "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt", skip)
        {
        }

        public NewPages(string category, string page, int pageLimit, string format)
            : this(category, page, pageLimit, format, "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt", true)
        {
        }

        public NewPages(string category, string page, int pageLimit, string format, string output, string previous, bool skip)
        {
            _categories = new List<string> { category };
            _categoriesToIgnore = new List<string>();
            Page = page;
            PageLimit = pageLimit;
            Format = format;
            Hours = 720;
            Output = output;
            Previous = previous;
            Skip = skip;
            Delimeter = "\n";
        }

        public NewPages(IEnumerable<string> categories,
                        string page,
                        int pageLimit,
                        string format,
                        string output,
                        string previous,
                        bool skip)
        {
            _categories = new List<string>(categories);
            _categoriesToIgnore = new List<string>();
            Page = page;
            PageLimit = pageLimit;
            Format = format;
            Hours = 720;
            Output = output;
            Previous = previous;
            Skip = skip;
            Delimeter = "\n";
        }

        public NewPages(IEnumerable<string> categories,
                        string page,
                        int pageLimit,
                        string format,
                        string output,
                        string previous,
                        string delimeter,
                        bool skip)
        {
            _categories = new List<string>(categories);
            _categoriesToIgnore = new List<string>();
            Page = page;
            PageLimit = pageLimit;
            Format = format;
            Hours = 720;
            Output = output;
            Previous = previous;
            Skip = skip;
            Delimeter = delimeter;
        }

        public NewPages(IEnumerable<string> categories,
                        string page,
                        int pageLimit,
                        string format,
                        string output,
                        string previous,
                        string delimeter,
                        bool skip,
                        IEnumerable<string> categoriesToIgnore)
        {
            _categories = new List<string>(categories);
            _categoriesToIgnore = new List<string>(categoriesToIgnore);
            Page = page;
            PageLimit = pageLimit;
            Format = format;
            Hours = 720;
            Output = output;
            Previous = previous;
            Skip = skip;
            Delimeter = delimeter;
        }

        public virtual void GetData(Wiki wiki)
        {
            foreach (var category in Categories)
            {
                Console.Out.WriteLine("Downloading data for " + category);
                string url = string.Format("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang=ru&wikifam=.wikipedia.org&basecat={0}&basedeep=7&mode=rc&hours={1}&onlynew=on&go=Сканировать&format=csv&userlang=ru",
                    Uri.EscapeDataString(category), Hours);
                WebClient client = new WebClient();
                client.DownloadFile(url, "Cache\\input-" + category + ".txt");
                using (TextWriter streamWriter = new StreamWriter(Previous))
                {
                    try
                    {
                        string text = wiki.LoadPage(Page);
                        streamWriter.Write(text);
                    }
                    catch (WikiPageNotFound)
                    {
                    }
                }
            }

            foreach (var category in CategoriesToIgnore)
            {
                Console.Out.WriteLine("Downloading data for " + category);
                string url = string.Format("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang=ru&wikifam=.wikipedia.org&basecat={0}&basedeep=7&mode=rc&hours={1}&onlynew=on&go=Сканировать&format=csv&userlang=ru",
                    Uri.EscapeDataString(category), Hours);
                WebClient client = new WebClient();
                client.DownloadFile(url, "Cache\\input-" + category + ".txt");
                using (TextWriter streamWriter = new StreamWriter(Previous))
                {
                    try
                    {
                        string text = wiki.LoadPage(Page);
                        streamWriter.Write(text);
                    }
                    catch (WikiPageNotFound)
                    {
                    }
                }
            }
        }

        public virtual void ProcessData(Wiki wiki)
        {
            List<string> pages = new List<string>();
            int index = 0;

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
                            if (!ignore.Contains(title))
                            {
                                pages.Add(string.Format(Format,
                                    title));
                                ++index;
                            }
                        }
                        if (index >= PageLimit)
                        {
                            break;
                        }
                    }
                }
            }

            using (TextWriter streamWriter = new StreamWriter(Output))
            {
                streamWriter.Write(string.Join(Delimeter, pages.ToArray()));
            }
        }

        public virtual bool UpdatePage(Wiki wiki)
        {
            if (!File.Exists(Previous))
            {
                using (TextWriter streamWriter = new StreamWriter(Previous))
                {
                }
            }
            using (TextReader oldSr = new StreamReader(Previous))
            using (TextReader sr = new StreamReader(Output))
            {
                string oldText = oldSr.ReadToEnd();
                string[] oldLines = oldText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string text = sr.ReadToEnd();
                string[] lines = text.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (string.IsNullOrEmpty(text) || (Skip && lines.Length < oldLines.Length))
                {
                    Console.Out.WriteLine("Skipping " + Page);
                    return false;
                }
                Console.Out.WriteLine("Updating " + Page);
                wiki.SavePage(Page,
                    "",
                    text,
                    "обновление",
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Replace);
                return true;
            }
        }
    }
}
