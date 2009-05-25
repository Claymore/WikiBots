using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Claymore.SharpMediaWiki;
using TalkCleanupWikiBot.Properties;

namespace Claymore.TalkCleanupWikiBot
{
    class Program
    {
        static void Main(string[] args)
        {
            UpdateUkWiki();
            UpdateRuWiki();
        }

        private static void UpdateUkWiki()
        {
            Wiki wiki = new Wiki("http://uk.wikipedia.org");
            wiki.SleepBetweenQueries = 2;
            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            wiki.Login(Settings.Default.Login, Settings.Default.Password);
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категорія:Незавершені обговорення вилучення сторінок";
            l10i.Culture = "uk-UA";
            l10i.MainPage = "Вікіпедія:Статті-кандидати на вилучення";
            l10i.Template = "Вилучення статей";
            l10i.TopTemplate = "/шапка";
            l10i.BottomTemplate = "/низ";
            l10i.Result = "Підсумок";
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

            ArticlesForDeletion afd = new ArticlesForDeletion(l10i);
            afd.Analyse(wiki);
            afd.UpdateMainPage(wiki);
            afd.UpdateArchive(wiki);
            afd.UpdatePages(wiki);

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
            
            Cleanup cleanup = new Cleanup(cleanupL10i);
            cleanup.Analyze(wiki);
            cleanup.UpdateMainPage(wiki);

            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        private static void UpdateRuWiki()
        {
            Wiki wiki = new Wiki("http://ru.wikipedia.org");
            wiki.SleepBetweenQueries = 2;

            if (string.IsNullOrEmpty(Settings.Default.Login) ||
                string.IsNullOrEmpty(Settings.Default.Password))
            {
                Console.Out.WriteLine("Please add login and password to the configuration file.");
                return;
            }
            Console.Out.WriteLine("Logging in as " + Settings.Default.Login + " to " + wiki.Uri + "...");
            wiki.Login(Settings.Default.Login, Settings.Default.Password);
            Console.Out.WriteLine("Logged in as " + Settings.Default.Login + ".");

            DeletionReview dr = new DeletionReview();
            dr.Analyze(wiki);
            dr.UpdateMainPage(wiki);

            ProposedSplits ps = new ProposedSplits();
            ps.Analyze(wiki);
            ps.UpdateMainPage(wiki);

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

            Cleanup cleanup = new Cleanup(cleanupL10i);
            cleanup.Analyze(wiki);
            cleanup.UpdateMainPage(wiki);

            ProposedMerges pm = new ProposedMerges();
            pm.UpdatePages(wiki);
            pm.Analyze(wiki);
            pm.UpdateMainPage(wiki);
            pm.UpdateArchivePages(wiki);

            ArticlesForDeletionLocalization l10i = new ArticlesForDeletionLocalization();
            l10i.Category = "Категория:Википедия:Незакрытые обсуждения удаления страниц";
            l10i.Culture = "ru-RU";
            l10i.MainPage = "Википедия:К удалению";
            l10i.Template = "Удаление статей";
            l10i.TopTemplate = "/Заголовок";
            l10i.BottomTemplate = "/Подвал";
            l10i.Result = "Итог";
            l10i.Language = "ru";
            l10i.MainPageUpdateComment = "обновление";
            l10i.ArchiveTemplate = "Статьи, вынесенные на удаление";
            l10i.ArchivePage = "Википедия:Архив запросов на удаление/";
            l10i.EmptyArchive = "нет обсуждений";
            l10i.Processor = null;
            l10i.StrikeOutComment = "зачёркивание заголовков";
            l10i.AutoResultMessage = "Страница была удалена {1} администратором [[User:{0}|]]. Была указана следующая причина: «{2}». Данное сообщение было автоматически сгенерировано ботом ~~~~.\n";
            l10i.DateFormat = "d MMMM yyyy в HH:mm (UTC)";
            l10i.AutoResultComment = " и подведение итогов";

            ArticlesForDeletion afd = new ArticlesForDeletion(l10i);
            afd.UpdatePages(wiki);
            afd.Analyse(wiki);
            afd.UpdateMainPage(wiki);
            afd.UpdateArchive(wiki);

            RequestedMoves rm = new RequestedMoves();
            rm.Analyze(wiki);
            rm.UpdateMainPage(wiki);
            rm.UpdatePages(wiki);
            //rm.UpdateArchive(wiki, 2009, 2);
            //rm.UpdateArchive(wiki, 2009, 1);

            wiki.Logout();
            Console.Out.WriteLine("Done.");
        }

        static string RemoveVotes(WikiPageSection section)
        {
            Regex re = new Regex(@"\s+\d{1,3}—\d{1,3}(—\d{1,3})?\s*(</s>)?\s*$");
            return re.Replace(section.Title, "$2");
        }

        static string RemoveOK(WikiPageSection section)
        {
            Regex re = new Regex(@"(<s>)?\s*{{(ok|OK|Ok|oK|ОК|ок|Ок|оК)}}\s*(.+?)(</s>)?\s*$");
            return re.Replace(section.Title, "<s>$3</s>");
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
        public DateTime Timestamp;
    }

    internal struct ArticlesForDeletionLocalization
    {
        public string Language;
        public string Category;
        public string MainPage;
        public string Culture;
        public string Template;
        public string TopTemplate;
        public string BottomTemplate;
        public string Result;
        public string MainPageUpdateComment;
        public string ArchiveTemplate;
        public string ArchivePage;
        public string EmptyArchive;
        public ArticlesForDeletion.TitleProcessor Processor;
        public string StrikeOutComment;
        public string AutoResultMessage;
        public string DateFormat;
        public string AutoResultComment;
    }
}
