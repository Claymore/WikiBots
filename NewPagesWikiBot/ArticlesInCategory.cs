using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Claymore.SharpMediaWiki;
using System.Net;
using System.IO;

namespace Claymore.NewPagesWikiBot
{
    class ArticlesInCategory : IPortalModule
    {
        public string Page { get; private set; }
        public string Category { get; private set; }
        public string Format { get; private set; }
        protected string _directory;

        public ArticlesInCategory(string category, string page, string format)
        {
            Category = category;
            Format = format;
            Page = page;
            _directory = "Cache\\ArticlesInCategory";
            Directory.CreateDirectory(_directory);
        }

        #region IPortalModule Members

        public void GetData(Wiki wiki)
        {
            Console.Out.WriteLine("Downloading data for " + Category);

            string query = string.Format("language={0}&depth=3&categories={1}&sortby=title&format=tsv&doit=submit",
                "ru",
                Uri.EscapeDataString(Category));

            UriBuilder ub = new UriBuilder("http://toolserver.org");
            ub.Path = "/~magnus/catscan_rewrite.php";
            ub.Query = query;

            WebClient client = new WebClient();
            client.DownloadFile(ub.Uri, _directory + "\\input-" + Category + ".txt");
        }

        public bool UpdatePage(Wiki wiki)
        {
            using (TextReader sr = new StreamReader(_directory + "\\output-" + Category + ".txt"))
            {
                string text = sr.ReadToEnd();
                if (string.IsNullOrEmpty(text))
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

        public void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + Category);
            using (TextWriter streamWriter = new StreamWriter(_directory + "\\output-" + Category + ".txt"))
            using (TextReader streamReader = new StreamReader(_directory + "\\input-" + Category + ".txt"))
            {
                streamReader.ReadLine();
                streamReader.ReadLine();
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    string title = groups[0].Replace('_', ' ');
                    streamWriter.WriteLine(string.Format(Format, title));
                }
            }
        }

        #endregion
    }
}
