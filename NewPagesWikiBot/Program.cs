using System;
using System.Collections.Generic;
using System.Net;
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
                new Portal("Религия", "Портал:Религия/Новые статьи", 35, "* [[{0}]]"),
                new Portal("Италия", "Портал:Италия/Новые статьи", 35, "* [[{0}]]"),
                new Portal("Христианство", "Портал:Христианство/Новые статьи", 25, "* [[{0}]]"),
                new Portal("Католицизм", "Портал:Католицизм/Новые статьи"),
                new Portal("Перу", "Портал:Перу/Новые статьи", 20, "# [[{0}]]"),
                new Portal("Шотландия", "Портал:Шотландия/Новые статьи"),
                new Portal("Уэльс", "Портал:Уэльс/Новые статьи"),
                new Portal("Эротика", "Портал:Эротика и порнография/Новые статьи"),
                new Portal("Греция", "Портал:Греция/Новые статьи"),
                new Portal("Награды", "Портал:Награды/Новые статьи"),
                new Portal("Лютеранство", "Портал:Лютеранство/Новые статьи", 15, "* [[{0}]]"),
                new Portal("Мексика", "Портал:Мексика/Новые статьи"),
                new Portal("Средние века", "Портал:Средневековье/Новые статьи"),
                new Portal("Одесса", "Портал:Одесса/Новые статьи"),
                new Portal("Психоактивные вещества", "Портал:Психоактивные субстанции/Новые статьи"),
                new Portal("Шотландия", "Портал:Шотландия/Новые статьи"),
                new Portal("Южная Америка", "Портал:Южная Америка/Новые статьи"),
                new Portal("Испания", "Портал:Испания/Новые статьи"),
                new Portal("Турция", "Портал:Турция/Новые статьи"),
                new Portal("Великобритания", "Портал:Великобритания/Новые статьи"),
                new Portal("США", "Портал:США/Новые статьи"),
                new Portal("Эстония", "Портал:Эстония/Новые статьи"),
                new Portal("Генетика", "Портал:Генетика/Новые статьи"),
                new Portal("Франция", "Портал:Франция/Новые статьи"),
                new Portal("Каталония", "Портал:Каталония/Новые статьи"),
                new Portal("Камбоджа", "Портал:Камбоджа/Новые статьи"),
                new Portal("Таиланд", "Портал:Таиланд/Новые статьи"),
                new Portal("Православие", "Портал:Православие/Новые статьи", 25, "# [[{0}]]"),
                new Portal("Тула", "Портал:Тула/Новые статьи", 20, "# [[{0}]]"),

                new PortalWithAuthors("Древний Восток", "Портал:Древний Восток/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy"),
                new PortalWithAuthors("Индеанистика", "Портал:Индейцы/Новые статьи", 20, "* [[{0}]] <small>— [[User:{1}|]] {2}</small>", "d MMMM yyyy"),
                new PortalWithAuthors("Доисторическая Европа", "Портал:Доисторическая Европа/Новые статьи", 25, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy"),
                new PortalWithAuthors("Лингвистика", "Портал:Лингвистика/Новые статьи", 30, "# [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy"),
                new PortalWithAuthors("Ирландия", "Портал:Ирландия/Новые статьи", 20, "* '''{2}''' — [[{0}]]", "[[d MMMM]] [[yyyy]]"),
                new PortalWithAuthors("Монголия", "Портал:Монголия/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy"),
                new PortalWithAuthors("Индия", "Портал:Индия/Новые статьи", 10, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy"),
                new PortalWithAuthors("Германия", "Портал:Германия/Новые статьи", 20, "* [[{0}]] <small>— {2}</small>", "d MMMM yyyy"),
                new PortalWithAuthors("Этнология", "Портал:Этнология/Новые статьи", 20, "* [[{0}]] — [[User:{1}|]] {2}", "d MMMM yyyy"),
                new PortalWithAuthors("Канада", "Портал:Канада/Новые статьи", 15, "# [[{0}]] <small>— {2} [[User:{1}|]]</small>", "dd.MM.yy"),
                new PortalWithAuthors("Сельское хозяйство", "Портал:Сельское хозяйство/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy"),
                new PortalWithAuthors("Финляндия", "Портал:Финляндия/Новые статьи", 35, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy"),
                new PortalWithAuthors("Швеция", "Портал:Швеция/Новые статьи", 20, "{{{{Новая статья|{0}|{2}|{1}}}}}", "d MMMM yyyy"),
                new PortalWithAuthors("Ужасы", "Портал:Хоррор/Новые статьи", 20, "# [[{0}]] — {2}", "d MMMM"),
                new PortalWithAuthors("Телевидение", "Портал:Телевидение/Новые статьи", 20, "* [[{0}]] — {2}", "d MMMM"),

                new PortalWithImages("Энтомология", "Портал:Энтомология/Новые статьи", 9, "Файл:{1}|[[{0}]]",
                    "<gallery perrow=\"3\" widths=\"110px\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Энтомология]]."),
                new PortalWithImages("Микология", "Портал:Микология/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=center><gallery perrow=\"1\" widths=\"80px\">",
                    "</gallery></div>"),
                new PortalWithImages("Ботаника", "Портал:Ботаника/Новые статьи", 8, "Файл:{1}|[[{0}]]",
                    "<div align=center>\n<gallery perrow=\"2\" widths=\"125px\" heights=\"125px\" caption=\"Ботаника\">",
                    "</gallery>\nОсновной список новых статей в [[Википедия:Проект:Ботаника]].\n</div>"),

                new PortalWithWeeks("Музыка", "Википедия:Проект:Музыка/Статьи", "* {2} — [[{0}]] &nbsp; <small>{{{{u|{1}}}}}</small>", "HH:mm",
                    "{{МСВС}}", "{{МСВС-предупреждение}}"),
                new PortalWithWeeks("Футбол", "Википедия:Проект:Футбол/Статьи", "* {2} — [[{0}]] &nbsp; <small>{{{{u|{1}}}}}</small>", "HH:mm",
                    "{{ФСВС}}", "{{ФСВС-предупреждение}}"),
            };

            for (int i = 0; i < portals.Count; ++i)
            {
                try
                {
                    portals[i].GetData(wiki);
                    portals[i].ProcessData(wiki);
                }
                catch (WebException e)
                {
                    Console.Out.WriteLine("Failed to fetch data for " + portals[i].Page + ": " + e.Message);
                    continue;
                }
                try
                {
                    portals[i].UpdatePage(wiki);
                }
                catch (WikiException e)
                {
                    Console.Out.WriteLine("Failed to update " + portals[i].Page + ": " + e.Message);
                }
            }
        }
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
