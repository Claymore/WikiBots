using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Claymore.SharpMediaWiki;
using TalkCleanupWikiBot.Properties;

namespace Claymore.TalkCleanupWikiBot
{
    class Program
    {
        static int Main(string[] args)
        {
            bool success = true;
            foreach (var arg in args)
            {
                if (arg == "/ru")
                {
                    success = UpdateRuWiki() && success;
                }
                else if (arg == "/uk")
                {
                    success = UpdateUkWiki() && success;
                }
            }
            return success ? 0 : -1;
        }

        private static bool UpdateUkWiki()
        {
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return false;
            }

            Wiki wiki = new Wiki("http://uk.wikipedia.org/w/");
            wiki.SleepBetweenQueries = 2;
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            try
            {
                Directory.CreateDirectory(@"Cache\uk");
                string cookieFile = @"Cache\uk\cookie.jar";
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password, cookieFile);

                if (!WikiCache.LoadNamespaces(wiki, @"Cache\uk\namespaces.dat"))
                {
                    wiki.GetNamespaces();
                    WikiCache.CacheNamespaces(wiki, @"Cache\uk\namespaces.dat");
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return false;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категорія:Незавершені обговорення вилучення сторінок";
            l10i.Culture = "uk-UA";
            l10i.MainPage = "Вікіпедія:Статті-кандидати на вилучення";
            l10i.Template = "Вилучення статей";
            l10i.TopTemplate = "/шапка";
            l10i.BottomTemplate = "/низ";
            l10i.Results = new string[] { "Підсумок" };
            l10i.Language = "uk";
            l10i.MainPageUpdateComment = "оновлення даних";
            l10i.ArchiveTemplate = "Статті, винесені на вилучення";
            l10i.ArchivePage = "Вікіпедія:Архів запитів на вилучення/";
            l10i.EmptyArchive = "обговорення не розпочато";
            l10i.Processor = RemoveVotes;
            l10i.StrikeOutComment = "закреслення заголовків";
            l10i.AutoResultMessage = "Сторінка була вилучена {1} адміністратором [[User:{0}|]]. Була вказана наступна причина: «{2}». Це повідомлення було автоматично згенеровано ботом ~~~~.\n";
            l10i.DateFormat = "d MMMM yyyy";
            l10i.AutoResultComment = ", підбиття підсумків";
            l10i.AutoResultSection = "Підсумок";
            l10i.NotificationTemplate = "Залишено";
            l10i.EmptyResult = "Підсумок";
            l10i.ChallengedResult = "Оспоренный итог";
            l10i.ArchiveHeader = "{| class=standard\n|-\n!| Дата\n!|" + l10i.ArchiveTemplate + "\n|-\n";
            l10i.ArchiveFooter = "|}";

            Cleanup.Localization cleanupL10i = new Cleanup.Localization();
            cleanupL10i.Language = "uk";
            cleanupL10i.Category = "Категорія:Вікіпедія:Незакриті обговорення поліпшення статей";
            cleanupL10i.MainPage = "Вікіпедія:Статті, що необхідно поліпшити";
            cleanupL10i.Culture = CultureInfo.CreateSpecificCulture("uk-UA");
            cleanupL10i.Template = "Поліпшення статей";
            cleanupL10i.TopTemplate = "/шапка";
            cleanupL10i.BottomTemplate = "/низ";
            cleanupL10i.Processor = RemoveOK;
            cleanupL10i.MainPageUpdateComment = "оновлення даних";
            cleanupL10i.closedRE = new Regex(@"({{ВППОЛ-навігація}}\s*({{Закрито|Closed|закрито|closed)}})|({{(Закрито|Closed|закрито|closed)}}\s*{{ВППОЛ-навігація}})");
            cleanupL10i.CloseComment = "закрито";
            cleanupL10i.ClosePage = ClosePageUk;
            cleanupL10i.MainPageSection = "0";
            cleanupL10i.ArchiveTemplate = "Статті, що необхідно поліпшити";
            cleanupL10i.ArchivePage = "Вікіпедія:Статті, що необхідно поліпшити/Архів/";
            cleanupL10i.EmptyArchive = "обговорення не розпочато";
            cleanupL10i.NavigationTemplate = "ВППОЛ-навігація";
            cleanupL10i.ArchiveHeader = "{| class=standard\n|-\n!| Дата\n!|" + cleanupL10i.ArchiveTemplate + "\n|-\n";
            cleanupL10i.ArchiveFooter = "|}";

            List<IModule> modules = new List<IModule>()
            {
                new Cleanup(cleanupL10i),
                new ArticlesForDeletion(l10i),
            };

            for (int i = 0; i < modules.Count; ++i)
            {
                try
                {
                    modules[i].Run(wiki);
                }
                catch (WikiException)
                {
                    return false;
                }
                catch (WebException)
                {
                    return false;
                }
            }

            Console.Out.WriteLine("Done.");
            return true;
        }

        private static bool UpdateRuWiki()
        {
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return false;
            }

            Wiki wiki = new Wiki("http://ru.wikipedia.org/w/");
            wiki.SleepBetweenQueries = 2;
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            try
            {
                Directory.CreateDirectory(@"Cache\ru");
                string cookieFile = @"Cache\ru\cookie.jar";
                WikiCache.Login(wiki, Settings.Default.Login, Settings.Default.Password, cookieFile);

                if (!WikiCache.LoadNamespaces(wiki, @"Cache\ru\namespaces.dat"))
                {
                    wiki.GetNamespaces();
                    WikiCache.CacheNamespaces(wiki, @"Cache\ru\namespaces.dat");
                }
            }
            catch (WikiException e)
            {
                Console.Out.WriteLine(e.Message);
                return false;
            }
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            string errorFileName = @"Cache\ru\Errors.txt";

            Cleanup.Localization cleanupL10i = new Cleanup.Localization();
            cleanupL10i.Language = "ru";
            cleanupL10i.Category = "Категория:Википедия:Незакрытые обсуждения статей для улучшения";
            cleanupL10i.MainPage = "Википедия:К улучшению";
            cleanupL10i.Culture = CultureInfo.CreateSpecificCulture("ru-RU");
            cleanupL10i.SectionTitle = "К улучшению";
            cleanupL10i.Template = "Улучшение статей/День";
            cleanupL10i.TopTemplate = "Улучшение статей/Статьи, вынесенные на улучшение";
            cleanupL10i.BottomTemplate = "Википедия:К улучшению/Подвал";
            cleanupL10i.Processor = RemoveOK;
            cleanupL10i.MainPageUpdateComment = "обновление";
            cleanupL10i.closedRE = new Regex(@"({{ВПКУЛ-(Н|н)авигация}}\s*{{(Закрыто|Closed|закрыто|closed)}})|({{(Закрыто|Closed|закрыто|closed)}}\s*{{ВПКУЛ-(Н|н)авигация}})");
            cleanupL10i.CloseComment = "обсуждение закрыто";
            cleanupL10i.ClosePage = ClosePageRu;
            cleanupL10i.MainPageSection = "1";
            cleanupL10i.ArchiveTemplate = "Статьи, вынесенные на улучшение";
            cleanupL10i.ArchivePage = "Википедия:К улучшению/Архив/";
            cleanupL10i.EmptyArchive = "нет обсуждений";
            cleanupL10i.NavigationTemplate = "ВПКУЛ-Навигация";
            cleanupL10i.ArchiveFooter = "{{Улучшение статей/Конец}}";
            cleanupL10i.ArchiveHeader = "{{Навигация по архиву КУЛ}}\n{{Улучшение статей/Начало}}";

            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категория:Википедия:Незакрытые обсуждения удаления страниц";
            l10i.Culture = "ru-RU";
            l10i.MainPage = "Википедия:К удалению";
            l10i.Template = "Удаление статей";
            l10i.TopTemplate = "/Заголовок";
            l10i.BottomTemplate = "/Подвал";
            l10i.Results = new string[] { "Итог", "Общий итог", "Автоматический итог", "Автоитог" };
            l10i.Language = "ru";
            l10i.MainPageUpdateComment = "обновление";
            l10i.ArchiveTemplate = "Статьи, вынесенные на удаление";
            l10i.ArchivePage = "Википедия:Архив запросов на удаление/";
            l10i.EmptyArchive = "нет обсуждений";
            l10i.Processor = null;
            l10i.StrikeOutComment = "зачёркивание заголовков";
            l10i.AutoResultMessage = "Страница была удалена {1} участником [[User:{0}|]]. Была указана следующая причина: «{2}». Данное сообщение было автоматически сгенерировано ботом ~~~~.\n";
            l10i.DateFormat = "d MMMM yyyy в HH:mm (UTC)";
            l10i.AutoResultComment = " и подведение итогов";
            l10i.AutoResultSection = "Автоитог";
            l10i.NotificationTemplate = "Оставлено";
            l10i.EmptyResult = "Пустой итог";
            l10i.ChallengedResult = "Оспоренный итог";
            l10i.ArchiveHeader = "{{Навигация по архиву КУ}}\n{{Удаление статей/Начало}}\n";
            l10i.ArchiveFooter = "{{Удаление статей/Конец}}";

            List<IModule> modules = new List<IModule>()
            {
                new CategoriesForDiscussion(),
                new DeletionReview(),
                new ProposedSplits(),
                new Cleanup(cleanupL10i),
                new ProposedMerges(),
                new ArticlesForDeletion(l10i),
                new IncubatorReview("Википедия:Проект:Инкубатор/Мини-рецензирование"),
                new IncubatorReview("Википедия:Проект:Инкубатор/Мини-рецензирование/Наука"),
                new IncubatorReview("Википедия:Проект:Инкубатор/Стабы"),
                new RequestedMoves()
            };

            if (!File.Exists(errorFileName))
            {
                using (FileStream stream = File.Create(errorFileName)) { }
            }

            int lastIndex = 0;
            using (TextReader streamReader = new StreamReader(errorFileName))
            {
                string line = streamReader.ReadToEnd();
                if (!string.IsNullOrEmpty(line))
                {
                    lastIndex = int.Parse(line);
                }
            }

            for (int i = lastIndex; i < modules.Count; ++i)
            {
                try
                {
                    modules[i].Run(wiki);
                }
                catch (WikiException)
                {
                    using (TextWriter streamWriter = new StreamWriter(errorFileName))
                    {
                        streamWriter.Write(i);
                    }
                    return false;
                }
                catch (WebException)
                {
                    using (TextWriter streamWriter = new StreamWriter(errorFileName))
                    {
                        streamWriter.Write(i);
                    }
                    return false;
                }
            }

            if (File.Exists(errorFileName))
            {
                File.Delete(errorFileName);
            }

            Console.Out.WriteLine("Done.");
            return true;
        }

        static string RemoveVotes(WikiPageSection section)
        {
            Regex re = new Regex(@"\s+\d{1,3}—\d{1,3}(—\d{1,3})?\s*(</s>)?\s*$");
            return re.Replace(section.Title, "$2");
        }

        static string RemoveOK(WikiPageSection section)
        {
            Regex re = new Regex(@"^\s*(<s>)?\s*{{(ok|OK|Ok|oK|ОК|ок|Ок|оК|x|X)}}\s*(.+?)(</s>)?\s*$");
            return re.Replace(section.Title, "$1$3</s>");
        }

        static string ClosePageRu(string text)
        {
            string result = text.Replace("{{ВПКУЛ-Навигация}}", "{{ВПКУЛ-Навигация|nocat=1}}");
            return result.Replace("{{ВПКУЛ-навигация}}", "{{ВПКУЛ-Навигация|nocat=1}}");
        }

        static string ClosePageUk(string text)
        {
            string result = text.Replace("{{ВППОЛ-навігація}}", "{{ВППОЛ-навігація|nocat=1}}");
            return result;
        }
    }

    internal struct Day
    {
        public WikiPage Page;
        public DateTime Date;
        public bool Archived;
        public bool Exists;
    }

    internal struct DeleteLogEvent
    {
        public string Comment;
        public string User;
        public bool Deleted;
        public bool Restored;
        public DateTime Timestamp;
    }

    internal struct ArticlesForDeletionLocalization
    {
        public string AutoResultSection;
        public string Language;
        public string Category;
        public string MainPage;
        public string Culture;
        public string Template;
        public string TopTemplate;
        public string BottomTemplate;
        public string[] Results;
        public string MainPageUpdateComment;
        public string ArchiveTemplate;
        public string ArchivePage;
        public string EmptyArchive;
        public ArticlesForDeletion.TitleProcessor Processor;
        public string StrikeOutComment;
        public string AutoResultMessage;
        public string DateFormat;
        public string AutoResultComment;
        public string NotificationTemplate;
        public string EmptyResult;
        public string ChallengedResult;
        public string ArchiveHeader;
        public string ArchiveFooter;
    }

    internal interface IModule
    {
        void Run(Wiki wiki);
    }
}
