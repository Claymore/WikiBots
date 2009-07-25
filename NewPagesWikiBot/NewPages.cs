using System;
using System.IO;
using System.Net;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPages : IPortalModule
    {
        public string Page { get; private set; }
        public string Category { get; private set; }
        public string Format { get; private set; }
        public string Head { get; set; }
        public string Bottom { get; set; }
        public int PageLimit { get; private set; }
        protected int Hours { get; set; }
        protected string Output { get; set; }
        protected string Previous { get; set; }

        public NewPages(string category, string page)
            : this(category, page, 20, "* [[{0}]]")
        {
        }

        public NewPages(string category, string page, int pageLimit, string format)
            : this(category, page, pageLimit, format, "Cache\\output-" + category + ".txt",
                   "Cache\\input-" + category + "-previous.txt")
        {
        }

        public NewPages(string category, string page, int pageLimit, string format, string output, string previous)
        {
            Page = page;
            Category = category;
            PageLimit = pageLimit;
            Format = format;
            Hours = 720;
            Output = output;
            Previous = previous;
        }

        public virtual void GetData(Wiki wiki)
        {
            Console.Out.WriteLine("Downloading data for " + Category);
            string url = string.Format("http://toolserver.org/~daniel/WikiSense/CategoryIntersect.php?wikilang=ru&wikifam=.wikipedia.org&basecat={0}&basedeep=7&mode=rc&hours={1}&onlynew=on&go=Сканировать&format=csv&userlang=ru",
                Uri.EscapeDataString(Category), Hours);
            WebClient client = new WebClient();
            client.DownloadFile(url, "Cache\\input-" + Category + ".txt");
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

        public virtual void ProcessData(Wiki wiki)
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
                    if (groups[0] == "0")
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
                if (string.IsNullOrEmpty(text) || lines.Length < oldLines.Length)
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
