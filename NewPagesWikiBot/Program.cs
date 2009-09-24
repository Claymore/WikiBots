using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Claymore.NewPagesWikiBot.Properties;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return 0;
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
                return 0;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            List<Portal> portals = new List<Portal>
            {
                new Portal("География", new IPortalModule[] { new NewPages("География", "Портал:География/Новые статьи", 10, "* [[{0}]]") }),
                new Portal("Искусство", new IPortalModule[] { new NewPages("Искусство", "Портал:Искусство/Новые статьи", 10, "* [[{0}]]") }),
                new Portal("Математика", new IPortalModule[] { new ArticlesInCategory("Динамические системы", "Википедия:Проект:Математика/Динамические системы/watchlist", "* [[{0}]] ([[Обсуждение:{0}|обсуждение]])") }),
                new Portal("Ленинградская область", new IPortalModule[] { new NewPages("Ленинградская область", "Портал:Ленинградская область/Новые статьи") }),
                new Portal("Санкт-Петербург", new IPortalModule[] { new NewPages("Санкт-Петербург", "Портал:Санкт-Петербург/Новые статьи", 45, "* [[{0}]]", false) }),
                new Portal("Биология", new IPortalModule[] { new NewPages("Биология", "Портал:Биология/Новые статьи", 30, "* [[{0}]]") }),
                new Portal("Криптография", new IPortalModule[] { new NewPages("Криптография", "Портал:Криптография/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Религия", "Портал:Религия/Новые статьи", 35, "* [[{0}]]") }),
                new Portal("Италия", new IPortalModule[] { new NewPages("Италия", "Портал:Италия/Новые статьи", 35, "* [[{0}]]") }),
                new Portal("Христианство", new IPortalModule[] { new NewPages("Христианство", "Портал:Христианство/Новые статьи", 25, "* [[{0}]]", false) }),
                new Portal("Католицизм", new IPortalModule[] { new NewPages("Католицизм", "Портал:Католицизм/Новые статьи", 20, "* [[{0}]]", false) }),
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
                new Portal("Татарстан", new IPortalModule[] { new NewPages("Татарстан", "Портал:Татарстан/Новые статьи") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Православие", "Портал:Православие/Новые статьи", 25, "# [[{0}]]", false) }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Тула", "Портал:Тула/Новые статьи", 20, "# [[{0}]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPages("Белоруссия", "Портал:Белоруссия/Новые статьи", 15, "* [[{0}]]") }),
                new Portal("Мальта", new IPortalModule[] { new NewPages("Мальта", "Портал:Мальта/Новые статьи") }),
                new Portal("Израиль", new IPortalModule[]
                {
                    new NewPagesWithProcessing("Израиль", "Портал:Израиль/Новые статьи", 10, "* [[{0}]]", "Статья проекта Израиль", false),
                    new NewTemplates("Израиль", "Википедия:Проект:Израиль/Блоки/Новые шаблоны", 10, "# [[Шаблон:{0}|]]"),
                    new NewCategories("Израиль", "Википедия:Проект:Израиль/Блоки/Новые категории", 10, "# {{{{cl|{0}|1}}}}"),
                    new PagesForDeletion("Израиль", "Википедия:Проект:Израиль/Блоки/К удалению", "* [[{0}]] — [[{1}#{0}|{2}]]"),
                    new PagesForCleanup("Израиль", "Википедия:Проект:Израиль/Блоки/К улучшению", "* [[{0}]] — [[{1}#{0}|{2}]]"),
                    new PagesForMoving("Израиль", "Википедия:Проект:Израиль/Блоки/К переименованию", "* [[{0}]] — [[{1}#{0}|{2}]]"),
                    new PagesForMerging("Израиль", "Википедия:Проект:Израиль/Блоки/К объединению", "* [[{0}]] — [[{1}#{0}|{2}]]"),
                    new FeaturedArticles("Израиль", "Портал:Израиль/Избранные статьи", "* [[{0}]]"),
                    new GoodArticles("Израиль", "Портал:Израиль/Хорошие статьи", "* [[{0}]]"),
                    new FeaturedLists("Израиль", "Портал:Израиль/Избранные списки", "* [[{0}]]"),
                    new FeaturedArticleCandidates("Израиль", "Портал:Израиль/Кандидаты в избранные статьи", "* [[{0}]]"),
                    new GoodArticleCandidates("Израиль", "Портал:Израиль/Кандидаты в хорошие статьи", "* [[{0}]]"),
                }),
                new Portal("Египет", new IPortalModule[]
                {
                    new NewPages("Египет", "Портал:Египет/Новые статьи", 20, "* [[{0}]]"),
                    new FeaturedArticles("Египет", "Портал:Египет/Статьи/Избранные статьи", "* [[{0}]]"),
                    new GoodArticles("Египет", "Портал:Египет/Статьи/Хорошие статьи", "* [[{0}]]"),
                    new FeaturedLists("Египет", "Портал:Египет/Статьи/Избранные списки", "* [[{0}]]"),
                    new FeaturedArticleCandidates("Египет", "Портал:Египет/Статьи/Кандидаты в избранные статьи", "* [[{0}]]"),
                    new GoodArticleCandidates("Египет", "Портал:Египет/Статьи/Кандидаты в хорошие статьи", "* [[{0}]]"),
                }),
                new Portal("Литература", new IPortalModule[] { new NewPages("Литература", "Портал:Литература/Новые статьи", 25, "* [[{0}]]") }),
                new Portal("Кино", new IPortalModule[]
                    {
                        new NewPages("Кинематограф", "Портал:Кино/Новые статьи о кинематографе"),
                        new NewPages("Кинематографисты", "Портал:Кино/Новые статьи о персоналиях"),
                        new NewPages("Фильмы", "Портал:Кино/Новые статьи о фильмах"),
                    }),

                new Portal("Украинский футбол", new IPortalModule[]
                    {
                        new NewPagesWithArchive("Футбол на Украине", "Портал:Украинский футбол/Новые статьи", "Портал:Украинский футбол/Новые статьи/Архив", 20, "* {2} — [[{0}]]", "d MMMM yyyy"),
                    }),
                new Portal("Квебек", new IPortalModule[] { new NewPagesWithAuthors("Квебек", "Портал:Квебек/Новые статьи", 20, "* [[{0}]] ''{2}''", "yyyy-MM-dd") }),
                new Portal("Биология", new IPortalModule[] { new NewPagesWithAuthors("Биология", "Википедия:Проект:Биология/Новые статьи", 60, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Тюменская область", new IPortalModule[] { new NewPagesWithAuthors("Тюменская область", "Портал:Тюменская область/Новые статьи", 10, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Армения", new IPortalModule[] { new NewPagesWithArchive("Армения", "Портал:Армения/Новые статьи", "Портал:Армения/Новые статьи/Архив", 25, "* [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Карелия", new IPortalModule[] { new NewPagesWithAuthors("Карелия", "Портал:Карелия/Новые статьи", 20, "* [[{0}]] — <small>''{2}''</small>", "d MMMM yyyy") }),
                new Portal("Буддизм", new IPortalModule[] { new NewPagesWithArchive("Буддизм", "Портал:Буддизм/Новые статьи", "Портал:Буддизм/Архив статей", 20, "* [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Китай", new IPortalModule[] { new NewPagesWithArchive("Китай", "Портал:Китай/Новые статьи", "Портал:Китай/Новые статьи/Архив", 25, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Индия", new IPortalModule[] { new NewPagesWithArchive("Индия", "Портал:Индия/Новые статьи", "Википедия:Проект:Индия/Новые статьи", 10, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Сингапур", new IPortalModule[] { new NewPagesWithArchive("Сингапур", "Портал:Сингапур/Новые статьи", "Портал:Сингапур/Новые статьи/Архив", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Соборы", new IPortalModule[] { new NewPagesWithAuthors("Соборы", "Портал:Соборы/Новые статьи", 20, "{2} — [[{0}]]", "d MMMM", ", ") }),
                new Portal("Бурятия", new IPortalModule[] { new NewPagesWithArchive(new string[] {"Бурятия", "Буряты" }, "Портал:Бурятия/Новые статьи", "Портал:Бурятия/Новые статьи/Архив", "Бурятия", 35, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Древний Восток", "Портал:Древний Восток/Новые статьи", 25, "* [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Индейцы", "Портал:Индейцы/Новые статьи", 20, "* [[{0}]] <small>— [[User:{1}|{1}]] {2}</small>", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Доисторическая Европа", "Портал:Доисторическая Европа/Новые статьи", 25, "* [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Лингвистика", "Портал:Лингвистика/Новые статьи", 30, "# [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Ирландия", "Портал:Ирландия/Новые статьи", 20, "* '''{2}''' — [[{0}]]", "[[d MMMM]] [[yyyy]]") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithArchive("Монголия", "Портал:Монголия/Новые статьи", "Портал:Монголия/Новости/Архив", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Удмуртия", new IPortalModule[] { new NewPagesWithArchive("Удмуртия", "Портал:Удмуртия/Новые статьи", "Портал:Удмуртия/Новые статьи/Архив", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Германия", "Портал:Германия/Новые статьи", 20, "* [[{0}]] <small>— {2}</small>", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Этнология", "Портал:Этнология/Новые статьи", 20, "* [[{0}]] — [[User:{1}|{1}]] {2}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Канада", "Портал:Канада/Новые статьи", 15, "# [[{0}]] <small>— {2} [[User:{1}|{1}]]</small>", "dd.MM.yy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Сельское хозяйство", "Портал:Сельское хозяйство/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Психология", new IPortalModule[] { new NewPagesWithAuthors(new string[] { "Психология", "Психиатрия" }, "Портал:Психология/Новые статьи", "Психология", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Финляндия", "Портал:Финляндия/Новые статьи", 35, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Швеция", "Портал:Швеция/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Ужасы", "Портал:Хоррор/Новые статьи", 20, "# [[{0}]] — {2}", "d MMMM") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Телевидение", "Портал:Телевидение/Новые статьи", 20, "* [[{0}]] — {2}", "d MMMM") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithAuthors("Крым", "Портал:Крым/Новые статьи", 20, "* [[{0}]] — {2}", "d MMMM") }),

                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Энтомология", "Портал:Энтомология/Новые статьи", 9, "Файл:{1}|[[{0}]]",
                    "<gallery perrow=\"3\" widths=\"110px\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Энтомология]].") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Микология", "Портал:Микология/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=\"center\"><gallery perrow=\"1\" widths=\"80px\">",
                    "</gallery></div>") }),
                new Portal("Религия", new IPortalModule[] { new NewPagesWithImages("Ботаника", "Портал:Ботаника/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=\"center\">\n<gallery perrow=\"2\" widths=\"125px\" heights=\"125px\" caption=\"Ботаника\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Ботаника]].\n</div>") }),
                new Portal("Кулинария", new IPortalModule[] { new NewPagesWithImages("Кулинария", "Портал:Кулинария/Новые статьи", 6, "Файл:{1}|[[{0}]]",
                    "<div align=\"center\">\n<gallery perrow=\"3\" widths=\"110px\">",
                    "</gallery></div>",
                    new Regex(@"\[{2}(Image|File|Файл|Изображение):(?'fileName'.+?)\|")) }),
                new Portal("Мода", new IPortalModule[] { new NewPagesWithImages("Мода", "Портал:Мода/Новые статьи", 6, "Файл:{1}|[[{0}]]",
                    "<div align=\"center\">\n<gallery perrow=\"3\" widths=\"110px\">",
                    "</gallery></div>",
                    new Regex(@"\[{2}(Image|File|Файл|Изображение):(?'fileName'.+?)\|")) }),

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
                    new FeaturedArticleCandidates("Япония", "Портал:Япония/Кандидаты в избранные статьи", "* [[{0}]]"),
                    new GoodArticleCandidates("Япония", "Портал:Япония/Кандидаты в хорошие статьи", "* [[{0}]]"),
                }),
                new Portal("Ядро энцкиплопедии", new IPortalModule[] { new EncyShell("Cache\\EncyShell") }),
            };

            if (!File.Exists("Errors.txt"))
            {
                using (FileStream stream = File.Create("Errors.txt")) { }
            }

            int lastIndex = 0;
            using (TextReader streamReader = new StreamReader("Errors.txt"))
            {
                string line = streamReader.ReadToEnd();
                if (!string.IsNullOrEmpty(line))
                {
                    lastIndex = int.Parse(line);
                }
            }

            for (int i = lastIndex; i < portals.Count; ++i)
            {
                try
                {
                    portals[i].GetData(wiki);
                    portals[i].ProcessData(wiki);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine("Failed to get data for " + portals[i].Name + ": " + e.Message);
                    using (TextWriter streamWriter = new StreamWriter("Errors.txt"))
                    {
                        streamWriter.Write(i);
                    }
                    return -1;
                }
                catch (WebException e)
                {
                    Console.Out.WriteLine("Failed to get data for " + portals[i].Name + ": " + e.Message);
                    using (TextWriter streamWriter = new StreamWriter("Errors.txt"))
                    {
                        streamWriter.Write(i);
                    }
                    return -1;
                }
                int j = 0;
                for (j = 0; j < 3; ++j)
                {
                    try
                    {
                        portals[i].UpdatePages(wiki);
                        break;
                    }
                    catch (WikiException)
                    {
                    }
                }
                if (j == 3)
                {
                    using (TextWriter streamWriter = new StreamWriter("Errors.txt"))
                    {
                        streamWriter.Write(i);
                    }
                    return -1;
                }
            }
            if (File.Exists("Errors.txt"))
            {
                File.Delete("Errors.txt");
            }
            return 0;
        }
    }

    internal interface IPortalModule
    {
        void GetData(Wiki wiki);
        bool UpdatePage(Wiki wiki);
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
}
