using System;
using System.Collections.Generic;
using System.IO;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class NewPagesWithArchive : NewPages
    {
        public string ArchivePage { get; private set; }

        public NewPagesWithArchive(PortalModule module,
                        IEnumerable<string> categories,
                        IEnumerable<string> categoriesToIgnore,
                        string page,
                        string archive,
                        int depth,
                        int hours,
                        int maxItems,
                        string format,
                        string delimeter,
                        string header,
                        string footer)
            : base(module,
                   categories,
                   categoriesToIgnore,
                   page,
                   depth,
                   hours,
                   maxItems,
                   format,
                   delimeter,
                   header,
                   footer)
        {
            ArchivePage = archive;
        }

        public override void Update(Wiki wiki)
        {
            string text = GetData(wiki);
            string newText = "";
            if (Categories.Count == 1)
            {
                newText = ProcessCategory(wiki, text);
            }
            else if (Categories.Count > 1)
            {
                newText = ProcessData(wiki, text);
            }
            if (!string.IsNullOrEmpty(newText) && newText != text)
            {
                Console.Out.WriteLine("Updating " + Page);
                wiki.SavePage(Page,
                    "",
                    newText,
                    Module.UpdateComment,
                    MinorFlags.Minor,
                    CreateFlags.None,
                    WatchFlags.None,
                    SaveFlags.Replace);
            }
            

            string oldText = text;
            if (!string.IsNullOrEmpty(Header))
            {
                if (oldText.StartsWith(Header))
                {
                    oldText = oldText.Substring(Header.Length);
                }
                if (newText.StartsWith(Header))
                {
                    newText = newText.Substring(Header.Length);
                }
            }
            if (!string.IsNullOrEmpty(Footer))
            {
                if (oldText.EndsWith(Footer))
                {
                    oldText = oldText.Substring(0, oldText.Length - Footer.Length);
                }
                if (newText.EndsWith(Footer))
                {
                    newText = newText.Substring(0, oldText.Length - Footer.Length);
                }
            }
            string[] items = oldText.Split(new string[] { Delimeter },
                       StringSplitOptions.RemoveEmptyEntries);
            var newItems = new HashSet<string>(newText.Split(new string[] { Delimeter },
                       StringSplitOptions.RemoveEmptyEntries));
            var archiveItems = new List<string>();
            for (int i = 0; i < items.Length; ++i)
            {
                if (!newItems.Contains(items[i]))
                {
                    archiveItems.Add(items[i]);
                }
            }

            if (archiveItems.Count != 0)
            {
                Console.Out.WriteLine("Updating " + ArchivePage);
                string archiveText = string.Join(Delimeter, archiveItems.ToArray()) + "\n";
                wiki.SavePage(ArchivePage,
                        "",
                        archiveText,
                        Module.UpdateComment,
                        MinorFlags.Minor,
                        CreateFlags.None,
                        WatchFlags.None,
                        SaveFlags.Prepend);
            }
        }
    }
}
