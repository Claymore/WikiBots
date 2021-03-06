﻿using System;
using System.IO;
using System.Net;
using System.Text;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class WatchList : IPortalModule
    {
        public string Page { get; private set; }
        public string Category { get; private set; }
        public string Format { get; private set; }
        public PortalModule Module { get; private set; }
        public int Depth { get; private set; }
        public int Namespace { get; private set; }

        public WatchList(PortalModule module, string category, string page, int ns, string format, int depth)
        {
            Category = category;
            Format = format.Replace("{", "{{").Replace("}", "}}").Replace("%(название)", "{0}");
            Page = page;
            Depth = depth;
            Module = module;
            Namespace = ns;
        }

        public void Update(Wiki wiki)
        {
            WebClient client = new WebClient();
            Cache.LoadPageList(client, Module.Language, Category, Depth);
            string text = ProcessData(wiki);
            if (!string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Updating " + Page);
                wiki.Save(Page, text, Module.UpdateComment);
            }
        }

        private string ProcessData(Wiki wiki)
        {
            StringBuilder result = new StringBuilder();
            Console.Out.WriteLine("Processing data of " + Category);
            string fileName = "Cache\\" + Module.Language + "\\PagesInCategory\\" + Cache.EscapePath(Category) + ".txt";
            using (TextReader streamReader = new StreamReader(fileName))
            {
                streamReader.ReadLine();
                streamReader.ReadLine();
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] groups = line.Split(new char[] { '\t' });
                    if (groups.Length > 2 && groups[2] == Namespace.ToString())
                    {
                        string title = groups[0].Replace('_', ' ');
                        result.AppendLine(string.Format(Format, title));
                    }
                }
            }
            return result.ToString();
        }
    }
}
