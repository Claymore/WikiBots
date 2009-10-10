using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Claymore.SharpMediaWiki;
using System.IO;
using System.Net;
using System.Xml;

namespace Claymore.NewPagesWikiBot
{
    internal class EncyShell : IPortalModule
    {
        public string Page { get; private set; }
        public string LongColor { get; private set; }
        public string NormalColor { get; private set; }
        public string ShortColor { get; private set; }
        public PortalModule Module { get; private set; }
        public int Normal { get; private set; }
        public int Short { get; private set; }

        public EncyShell(PortalModule module, string page, int s, int n, string sc, string nc, string lc)
        {
            Page = page;
            Module = module;
            LongColor = lc;
            NormalColor = nc;
            ShortColor = sc;
            Normal = n;
            Short = s;
        }

        public string GetData(Wiki wiki)
        {
            Console.Out.WriteLine("Downloading data " + Page + "...");
            string text = wiki.LoadPage(Page);
            HashSet<string> names = new HashSet<string>();
            StringReader reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("| "))
                {
                    string[] properties = line.Split(new string[] { "||" }, StringSplitOptions.None);
                    string name = properties[2].Trim();
                    name = name.Replace("[[", "").Replace("]]", "");
                    if (!names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }

            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info");
            parameters.Add("redirects");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, names);

            List<Item> items = new List<Item>();
            foreach (XmlNode node in xml.SelectNodes("//page"))
            {
                int size = int.Parse(node.Attributes["length"].Value);
                string title = node.Attributes["title"].Value;
                items.Add(new Item(title, size, Status.None));
            }

            items.Sort(CompareItems);

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Избранная статья");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.FeaturedArticle;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Хорошая статья");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.GoodArticle;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Кандидат в хорошие статьи");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.GoodArticleCandidate;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Кандидат в избранные статьи");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.FeaturedArticleCandidate;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:Хорошая статья и кандидат в избранные");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.GoodArticleFeaturedCandidate;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:К лишению статуса хорошей");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.DisputedGoodArticle;
                }
            }

            parameters.Clear();
            parameters.Add("list", "embeddedin");
            parameters.Add("eititle", "Шаблон:К лишению статуса избранной");
            parameters.Add("einamespace", "0");
            parameters.Add("eilimit", "max");
            xml = wiki.Enumerate(parameters, true);
            foreach (Item item in items)
            {
                XmlNode node = xml.SelectSingleNode("//ei[@title='" + item.Name + "']");
                if (node != null)
                {
                    item.Status = Status.DisputedFeaturedArticle;
                }
            }

            StringBuilder result = new StringBuilder();

            result.AppendLine("{{/Шапка}}");
            result.AppendLine("{| class=\"wikitable sortable\" |");
            result.AppendLine("! № !! !! Название !! Размер");

            for (int i = 0; i < items.Count; ++i)
            {
                string style;
                Item item = items[i];
                if (item.Size < Short)
                {
                    style = " bgcolor=" + ShortColor;
                }
                else if (item.Size < Normal)
                {
                    style = " bgcolor=" + NormalColor;
                }
                else
                {
                    style = " bgcolor=" + LongColor;
                }
                result.AppendLine("|-" + style);
                result.AppendLine(string.Format("| {3} || {2} || [[{0}]] || {1}",
                    item.Name,
                    item.Size,
                    item.GetTemplate(),
                    i + 1,
                    style));
            }
            result.AppendLine("|}");
            return result.ToString();
        }

        private static int CompareItems(Item x, Item y)
        {
            return x.Size.CompareTo(y.Size);
        }

        public void Update(Wiki wiki)
        {
            string text = GetData(wiki);
            if (!string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Updating " + Page);
                wiki.SavePage(Page,
                    "",
                    text,
                    Module.UpdateComment,
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Replace);
            }
        }

        private class Item
        {
            public string Name { get; set; }
            public int Size { get; set; }
            public Status Status { get; set; }

            public Item(string name)
            {
                Name = name;
                Status = Status.None;
            }

            public Item(string name, int size, Status status)
            {
                Name = name;
                Size = size;
                Status = status;
            }

            public override string ToString()
            {
                return Name;
            }

            public string GetTemplate()
            {
                switch (Status)
                {
                    case Status.GoodArticle:
                        return "{{Icon GA}}";
                    case Status.FeaturedArticle:
                        return "{{Icon FA}}";
                    case Status.FeaturedArticleCandidate:
                        return "{{Icon +FA}}";
                    case Status.GoodArticleFeaturedCandidate:
                        return "{{Icon +FA}}";
                    case Status.GoodArticleCandidate:
                        return "{{Icon +GA}}";
                    case Status.DisputedGoodArticle:
                        return "{{Icon -GA}}";
                    case Status.DisputedFeaturedArticle:
                        return "{{Icon -FA}}";
                    default:
                        return "";
                }
            }
        }

        private class ItemEqualityComparer : IEqualityComparer<Item>
        {
            #region IEqualityComparer<Item> Members

            public bool Equals(Item x, Item y)
            {
                return x.Name == y.Name;
            }

            public int GetHashCode(Item obj)
            {
                return base.GetHashCode();
            }

            #endregion
        }

        enum Status
        {
            None,
            GoodArticleCandidate,
            GoodArticle,
            GoodArticleFeaturedCandidate,
            FeaturedArticleCandidate,
            FeaturedArticle,
            DisputedGoodArticle,
            DisputedFeaturedArticle
        }
    }
}
