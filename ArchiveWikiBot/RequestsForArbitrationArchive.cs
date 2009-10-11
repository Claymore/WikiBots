using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Claymore.SharpMediaWiki;

namespace Claymore.ArchiveWikiBot
{
    internal class RequestsForArbitrationArchive : IArchive
    {
        public string Title { get; private set; }
        public string Accepted { get; private set; }
        public string Rejected { get; private set; }
        public string Directory { get; private set; }
        public int Days { get; private set; }

        public RequestsForArbitrationArchive(string title,
            string accepted,
            string rejected,
            int days,
            string directory)
        {
            Title = title;
            Accepted = accepted;
            Rejected = rejected;
            Days = days;
            Directory = directory;
        }

        private static int FindTemplate(string text, int offset, out string request, out bool accepted, out DateTime date)
        {
            int begin = text.IndexOf("{{Иск", offset);
            if (begin == -1)
            {
                accepted = false;
                date = DateTime.MinValue;
                request = "";
                return -1;
            }
            int index = 1;
            int end = -1;
            for (int i = begin + 2; i < text.Length - 1; ++i)
            {
                if (text[i] == '{' && text[i + 1] == '{')
                {
                    ++index;
                }
                else if (text[i] == '}' && text[i + 1] == '}')
                {
                    --index;
                    if (index == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end == -1)
            {
                accepted = false;
                date = DateTime.MinValue;
                request = "";
                return -1;
            }

            request = text.Substring(begin, end - begin + 2);
            Regex requestRE = new Regex(@"\{\{Иск\s*\|.+?\|\s*Статус\s*=(.+?)\|.+?\|\s*Закрыт\s*=(\d{4}-\d{1,2}-\d{1,2})\s*\}\}",
                RegexOptions.Singleline);
            Match m = requestRE.Match(text, begin, end - begin + 2);
            if (m.Success)
            {
                request = m.Groups[0].Value;
                string decision = m.Groups[1].Value.Trim();
                string dateString = m.Groups[2].Value;

                DateTime.TryParse(dateString, out date);
                accepted = decision.ToLower() == "решение принято";
                return end;
            }
            else
            {
                accepted = false;
                date = DateTime.MinValue;
                request = text.Substring(begin, end - begin + 2);
                return end;
            }
        }

        #region IArchive Members

        public void Archivate(Wiki wiki)
        {
            WikiPage page = Cache.Load(wiki, Title, Directory);
            bool accepted;
            DateTime date;
            int offset = 0;
            string request;
            List<string> acceptedRequests = new List<string>();
            List<string> rejectedRequests = new List<string>();
            string text = page.Text;
            while ((offset = FindTemplate(page.Text, offset, out request, out accepted, out date)) != -1)
            {
                if (date != DateTime.MinValue && (DateTime.Today - date).TotalDays >= Days)
                {
                    if (accepted)
                    {
                        acceptedRequests.Add(request);
                    }
                    else
                    {
                        rejectedRequests.Add(request);
                    }
                    text = text.Replace(request, "");
                }
            }
            if (acceptedRequests.Count != 0 || rejectedRequests.Count != 0)
            {
                while (text.Contains("\n\n\n"))
                {
                    text = text.Replace("\n\n\n", "\n\n").Trim();
                }
            }
            else
            {
                return;
            }
            for (int i = 0; i < 5; ++i)
            {
                try
                {
                    Console.Out.WriteLine("Saving " + Title + "...");
                    wiki.SavePage(Title,
                        "",
                        text,
                        "архивация",
                        MinorFlags.Minor,
                        CreateFlags.None,
                        WatchFlags.None,
                        SaveFlags.Replace);
                    break;
                }
                catch (WikiException)
                {
                }
            }
            WikiPage acceptedPage = Cache.Load(wiki, Accepted, Directory);
            string start = "{{Иск/Заголовок таблицы}}";
            int index = acceptedPage.Sections[0].SectionText.IndexOf(start);
            if (acceptedRequests.Count != 0 && index != -1)
            {
                acceptedPage.Sections[0].SectionText = acceptedPage.Sections[0].SectionText.Insert(index + start.Length,
                    "\n" + string.Join("\n", acceptedRequests.ToArray()));
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        Console.Out.WriteLine("Saving " + Accepted + "...");
                        wiki.SavePage(Accepted,
                            "",
                            acceptedPage.Text,
                            "архивация",
                            MinorFlags.Minor,
                            CreateFlags.None,
                            WatchFlags.None,
                            SaveFlags.Replace);
                        break;
                    }
                    catch (WikiException)
                    {
                    }
                }
            }
            WikiPage rejectedPage = Cache.Load(wiki, Rejected, Directory);
            index = rejectedPage.Sections[0].SectionText.IndexOf(start);
            if (rejectedRequests.Count != 0 && index != -1)
            {
                rejectedPage.Sections[0].SectionText = rejectedPage.Sections[0].SectionText.Insert(index + start.Length,
                    "\n" + string.Join("\n", rejectedRequests.ToArray()));
                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        Console.Out.WriteLine("Saving " + Rejected + "...");
                        wiki.SavePage(Rejected,
                            "",
                            rejectedPage.Text,
                            "архивация",
                            MinorFlags.Minor,
                            CreateFlags.None,
                            WatchFlags.None,
                            SaveFlags.Replace);
                        break;
                    }
                    catch (WikiException)
                    {
                    }
                }
            }
        }

        #endregion
    }
}
