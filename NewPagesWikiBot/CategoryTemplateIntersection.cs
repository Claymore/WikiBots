using System;
using System.IO;
using Claymore.SharpMediaWiki;
using System.Net;

namespace Claymore.NewPagesWikiBot
{
    internal class CategoryTemplateIntersection : IPortalModule
    {
        public string Page { get; private set; }
        public string MainCategory { get; private set; }
        public string Template { get; private set; }
        public string Format { get; private set; }
        protected string _directory;

        public CategoryTemplateIntersection(string mainCategory,
            string template,
            string directory,
            string page,
            string format)
        {
            MainCategory = mainCategory;
            Format = format;
            Page = page;
            Template = template;
            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public void GetData(Wiki wiki)
        {
            Console.Out.WriteLine("Downloading data for " + MainCategory);

            string query = string.Format("language={0}&depth=15&categories={1}&templates_yes={2}&sortby=title&format=tsv&doit=submit",
                "ru",
                Uri.EscapeDataString(MainCategory),
                Uri.EscapeDataString(Template));

            UriBuilder ub = new UriBuilder("http://toolserver.org");
            ub.Path = "/~magnus/catscan_rewrite.php";
            ub.Query = query;

            WebClient client = new WebClient();
            client.DownloadFile(ub.Uri, _directory + "\\input-" + MainCategory + ".txt");
        }

        public virtual void ProcessData(Wiki wiki)
        {
            Console.Out.WriteLine("Processing data of " + MainCategory);
            using (TextWriter streamWriter = new StreamWriter(_directory + "\\output-" + MainCategory + ".txt"))
            using (TextReader streamReader = new StreamReader(_directory + "\\input-" + MainCategory + ".txt"))
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

        public void UpdatePage(Wiki wiki)
        {
            using (TextReader sr = new StreamReader(_directory + "\\output-" + MainCategory + ".txt"))
            {
                string text = sr.ReadToEnd();
                if (string.IsNullOrEmpty(text))
                {
                    Console.Out.WriteLine("Skipping " + Page);
                    return;
                }
                Console.Out.WriteLine("Updating " + Page);
                wiki.SavePage(Page, text, "обновление");
            }
        }
    }
}
