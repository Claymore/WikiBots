using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using Claymore.NewPagesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }

            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + "...");
            try
            {
                if (!wiki.LoadCookies())
                {
                    wiki.Login(Settings.Default.Login, Settings.Default.Password);
                    wiki.CacheCookies();
                }
                else
                {
                    wiki.Login();
                    if (!wiki.IsBot)
                    {
                        wiki.Logout();
                        wiki.Login(Settings.Default.Login, Settings.Default.Password);
                        wiki.CacheCookies();
                    }
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            List<Portal> portals = new List<Portal>
            {
                new Portal("Религия", new IPortalModule[] { new NewPages("Религия", "Портал:Религия/Новые статьи", 35, "* [[{0}]]") }),
                new Portal("Италия", new IPortalModule[] { new NewPages("Италия", "Портал:Италия/Новые статьи", 35, "* [[{0}]]") }),
                new Portal("Христианство", new IPortalModule[] { new NewPages("Христианство", "Портал:Христианство/Новые статьи", 25, "* [[{0}]]") }),
                new Portal("Католицизм", new IPortalModule[] { new NewPages("Католицизм", "Портал:Католицизм/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Перу", "Портал:Перу/Новые статьи", 20, "# [[{0}]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Шотландия", "Портал:Шотландия/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Уэльс", "Портал:Уэльс/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Эротика", "Портал:Эротика и порнография/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Греция", "Портал:Греция/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Награды", "Портал:Награды/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Лютеранство", "Портал:Лютеранство/Новые статьи", 15, "* [[{0}]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Мексика", "Портал:Мексика/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Средние века", "Портал:Средневековье/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Одесса", "Портал:Одесса/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Психоактивные вещества", "Портал:Психоактивные субстанции/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Шотландия", "Портал:Шотландия/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Южная Америка", "Портал:Южная Америка/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Испания", "Портал:Испания/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Турция", "Портал:Турция/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Великобритания", "Портал:Великобритания/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("США", "Портал:США/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Эстония", "Портал:Эстония/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Генетика", "Портал:Генетика/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Франция", "Портал:Франция/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Каталония", "Портал:Каталония/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Камбоджа", "Портал:Камбоджа/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Таиланд", "Портал:Таиланд/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Православие", "Портал:Православие/Новые статьи", 25, "# [[{0}]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Тула", "Портал:Тула/Новые статьи", 20, "# [[{0}]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Белоруссия", "Портал:Белоруссия/Новые статьи", 15, "* [[{0}]]") }),
                new Portal("Литература", new IPortalModule[] { new NewPages("Литература", "Портал:Литература/Новые статьи", 25, "* [[{0}]]") }),

                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Древний Восток", "Портал:Древний Восток/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Индеанистика", "Портал:Индейцы/Новые статьи", 20, "* [[{0}]] <small>— [[User:{1}|]] {2}</small>", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Доисторическая Европа", "Портал:Доисторическая Европа/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Лингвистика", "Портал:Лингвистика/Новые статьи", 30, "# [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Ирландия", "Портал:Ирландия/Новые статьи", 20, "* '''{2}''' — [[{0}]]", "[[d MMMM]] [[yyyy]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Монголия", "Портал:Монголия/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Индия", "Портал:Индия/Новые статьи", 10, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Германия", "Портал:Германия/Новые статьи", 20, "* [[{0}]] <small>— {2}</small>", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Этнология", "Портал:Этнология/Новые статьи", 20, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Канада", "Портал:Канада/Новые статьи", 15, "# [[{0}]] <small>— {2} [[User:{1}|]]</small>", "dd.MM.yy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Сельское хозяйство", "Портал:Сельское хозяйство/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Финляндия", "Портал:Финляндия/Новые статьи", 35, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Швеция", "Портал:Швеция/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Ужасы", "Портал:Хоррор/Новые статьи", 20, "# [[{0}]] — {2}", "d MMMM") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Телевидение", "Портал:Телевидение/Новые статьи", 20, "* [[{0}]] — {2}", "d MMMM") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Крым", "Портал:Крым/Новые статьи", 20, "* [[{0}]] — {2}", "d MMMM") }),

                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Энтомология", "Портал:Энтомология/Новые статьи", 9, "Файл:{1}|[[{0}]]",
                    "<gallery perrow=\"3\" widths=\"110px\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Энтомология]].") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Микология", "Портал:Микология/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=center><gallery perrow=\"1\" widths=\"80px\">",
                    "</gallery></div>") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Ботаника", "Портал:Ботаника/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=center>\n<gallery perrow=\"2\" widths=\"125px\" heights=\"125px\" caption=\"Ботаника\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Ботаника]].\n</div>") }),

                new Portal("Музыка", new IPortalModule[] { new NewPagesWithWeeks("Музыка", "Википедия:Проект:Музыка/Статьи", "* {2} — [[{0}]] &nbsp; <small>{{{{u|{1}}}}}</small>", "HH:mm",
                    "{{МСВС}}", "{{МСВС-предупреждение}}") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithWeeks("Футбол", "Википедия:Проект:Футбол/Статьи", "* {2} — [[{0}]] &nbsp; <small>{{{{u|{1}}}}}</small>", "HH:mm",
                    "{{ФСВС}}", "{{ФСВС-предупреждение}}") }),
                new Portal("Япония", new IPortalModule[]
                {
                    new NewPagesWithWeeks("Япония", "Википедия:Проект:Япония/Статьи", "* {2} — [[{0}]] <small>({{{{u|{1}}}}})</small>", "HH:mm",
                                        "{{Википедия:Проект:Япония/НС}}", "{{Википедия:Проект:Япония/Предупреждение}}"),
                    new PagesForDeletion("Япония", "Портал:Япония/Статьи к удалению", "* [[{0}]] ([[{1}#{0}|обсуждение]])"),
                    new PagesForCleanup("Япония", "Портал:Япония/Статьи к улучшению", "* [[{0}]] ([[{1}#{0}|обсуждение]])"),
                    new PagesForMoving("Япония", "Портал:Япония/Статьи к переименованию", "* [[{0}]] ([[{1}#{0}|обсуждение]])"),
                    new FeaturedArticles("Япония", "Портал:Япония/Избранные статьи", "* [[{0}]]"),
                    new GoodArticles("Япония", "Портал:Япония/Хорошие статьи", "* [[{0}]]"),
                    new FeaturedLists("Япония", "Портал:Япония/Избранные списки", "* [[{0}]]"),
                }),
            };

            for (int i = 0; i < portals.Count; ++i)
            {
                try
                {
                    portals[i].GetData(wiki);
                    portals[i].ProcessData(wiki);
                    portals[i].UpdatePages(wiki);
                }
                catch (WebException e)
                {
                    Console.Out.WriteLine("Failed to update " + portals[i].Name + ": " + e.Message);
                }
            }
        }
    }

    internal interface IPortalModule
    {
        void GetData(Wiki wiki);
        void UpdatePage(Wiki wiki);
        void ProcessData(Wiki wiki);
    }

    internal class NewPage
    {
        public string Name { get; private set; }
        public DateTime Time { get; private set; }
        public string Author { get; private set; }

        public NewPage(string name, DateTime time, string author)
        {
            Name = name;
            Time = time;
            Author = author;
        }
    }

    internal class FeaturedArticles : CategoryTemplateIntersection
    {
        public FeaturedArticles(string category, string page, string format)
            : base(category,
                   "Избранная статья",
                    "Cache\\FeaturedArticles",
                    page,
                    format)
        {
        }
    }

    internal class GoodArticles : CategoryTemplateIntersection
    {
        public GoodArticles(string category, string page, string format)
            : base(category,
                   "Хорошая статья",
                   "Cache\\GoodArticles",
                   page,
                   format)
        {
        }
    }

    internal class FeaturedLists : CategoryTemplateIntersection
    {
        public FeaturedLists(string category, string page, string format)
            : base(category,
                   "Избранный список или портал",
                    "Cache\\FeaturedLists",
                    page,
                    format)
        {
        }
    }

    internal class CategoryIntersectionAndTalkPages : CategoryTemplateIntersection
    {
        public string Prefix { get; private set; }

        public CategoryIntersectionAndTalkPages(string mainCategory,
            string category,
            string prefix,
            string directory,
            string page,
            string format)
            : base(mainCategory, category, directory, page, format)
        {
            Prefix = prefix;
        }

        public override void ProcessData(Wiki wiki)
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

                    ParameterCollection parameters = new ParameterCollection();
                    parameters.Add("list", "backlinks");
                    parameters.Add("bltitle", title);
                    parameters.Add("blnamespace", "4");
                    parameters.Add("bllimit", "max");

                    XmlDocument xml = wiki.Enumerate(parameters, true);
                    foreach (XmlNode node in xml.SelectNodes("//bl"))
                    {
                        if (node.Attributes["title"].Value.StartsWith(Prefix))
                        {
                            string page = node.Attributes["title"].Value;
                            streamWriter.WriteLine(string.Format(Format, title, page));
                            break;
                        }
                    }
                }
            }
        }
    }

    internal class PagesForDeletion : CategoryIntersectionAndTalkPages
    {
        public PagesForDeletion(string category, string page, string format)
            : base(category,
                   "К удалению",
                   "Википедия:К удалению/",
                    "Cache\\PagesForDeletion",
                    page,
                    format)
        {
        }
    }

    internal class PagesForCleanup : CategoryIntersectionAndTalkPages
    {
        public PagesForCleanup(string category, string page, string format)
            : base(category,
                   "К улучшению",
                   "Википедия:К улучшению/",
                    "Cache\\PagesForCleanup",
                    page,
                    format)
        {
        }
    }

    internal class PagesForMoving : CategoryIntersectionAndTalkPages
    {
        public PagesForMoving(string category, string page, string format)
            : base(category,
                   "К переименованию",
                   "Википедия:К переименованию/",
                    "Cache\\PagesForMoving",
                    page,
                    format)
        {
        }
    }
}
