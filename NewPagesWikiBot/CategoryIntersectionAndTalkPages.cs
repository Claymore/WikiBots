﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class CategoryIntersectionAndTalkPages : CategoryTemplateIntersection
    {
        public string Prefix { get; private set; }

        public CategoryIntersectionAndTalkPages(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string templates,
                        string prefix,
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
            : base(module,
                   categories,
                   categoriesToIgnore,
                   templates,
                   page,
                   ns,
                   depth,
                   hours,
                   maxItems,
                   format,
                   delimeter,
                   header,
                   footer,
                   markEdits)
        {
            Prefix = prefix;
        }

        public override string ProcessData(Wiki wiki)
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
                        if (groups.Count() > 2 && groups[2] == Namespace.ToString())
                        {
                            string title = groups[0].Replace('_', ' ');
                            ignore.Add(title);
                        }
                    }
                }
            }

            var pageList = new List<CategoryIntersectionElement>();
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
                        if (groups.Count() > 2 && groups[2] == Namespace.ToString())
                        {
                            string title = groups[0].Replace('_', ' ');
                            if (ignore.Contains(title))
                            {
                                continue;
                            }
                            if (!pages.Contains(title))
                            {
                                if (Namespace != 0)
                                {
                                    title = wiki.GetNamespace(Namespace) + ":" + title;
                                }

                                ParameterCollection parameters = new ParameterCollection();
                                parameters.Add("list", "backlinks");
                                parameters.Add("bltitle", title);
                                parameters.Add("blnamespace", "4");
                                parameters.Add("bllimit", "max");

                                List<DateTime> dates = new List<DateTime>();
                                XmlDocument xml = wiki.Enumerate(parameters, true);
                                foreach (XmlNode node in xml.SelectNodes("//bl"))
                                {
                                    if (node.Attributes["title"].Value.StartsWith(Prefix))
                                    {
                                        string page = node.Attributes["title"].Value;
                                        string dateString = page.Replace(Prefix, "");
                                        DateTime date;
                                        if (DateTime.TryParse(dateString, null,
                                            System.Globalization.DateTimeStyles.AssumeUniversal, out date))
                                        {
                                            dates.Add(date);
                                        }
                                    }
                                }
                                if (dates.Count > 0)
                                {
                                    DateTime talkDate = dates.Max();
                                    pageList.Add(new CategoryIntersectionElement(Namespace != 0
                                        ? title.Substring(wiki.GetNamespace(Namespace).Length + 1)
                                        : title,
                                        talkDate));
                                    pages.Add(title);
                                }
                            }
                        }
                    }
                }
            }

            if (pageList.Count == 0)
            {
                return "";
            }
            pageList.Sort(CompareCategoryIntersectionElements);
            var result = new List<string>();
            foreach (var item in pageList)
            {
                result.Add(string.Format(Format,
                    item.PageTitle,
                    "",
                    item.Date.ToUniversalTime().ToString("d MMMM yyyy")));
            }
            return Header + string.Join(Delimeter, result.ToArray()) + Footer;
        }

        static int CompareCategoryIntersectionElements(CategoryIntersectionElement x, CategoryIntersectionElement y)
        {
            if (y.Date == x.Date)
            {
                return x.PageTitle.CompareTo(y.PageTitle);
            }
            return y.Date.CompareTo(x.Date);
        }
    }

    struct CategoryIntersectionElement
    {
        public CategoryIntersectionElement(string title, DateTime date)
        {
            PageTitle = title;
            Date = date;
        }
        public string PageTitle;
        public DateTime Date;
    }
}
