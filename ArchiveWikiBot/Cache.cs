using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Claymore.SharpMediaWiki;
using System.Xml;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Claymore.ArchiveWikiBot
{
    internal class Cache
    {
        public static string EscapePath(string path)
        {
            Regex charsRE = new Regex(@"[:/\*\?<>\|]");
            return charsRE.Replace(path, "_").Replace('"', '_').Replace('\\', '_');
        }

        public static WikiPage Load(Wiki wiki, string title, string directory)
        {
            ParameterCollection parameters = new ParameterCollection();
            parameters.Add("prop", "info|revisions");
            parameters.Add("intoken", "edit");
            parameters.Add("rvprop", "timestamp");
            XmlDocument xml = wiki.Query(QueryBy.Titles, parameters, new string[] { title });
            XmlNode node = xml.SelectSingleNode("//rev");
            string baseTimeStamp = null;
            if (node != null && node.Attributes["timestamp"] != null)
            {
                baseTimeStamp = node.Attributes["timestamp"].Value;
            }
            node = xml.SelectSingleNode("//page");
            string editToken = node.Attributes["edittoken"].Value;

            string pageFileName = directory + EscapePath(title);
            string text = LoadPageFromCache(pageFileName, node.Attributes["lastrevid"].Value, title);

            if (string.IsNullOrEmpty(text))
            {
                Console.Out.WriteLine("Downloading " + title + "...");
                text = WikiPage.LoadText(title, wiki);
                CachePage(pageFileName, node.Attributes["lastrevid"].Value, text);
            }

            WikiPage page = WikiPage.Parse(title, text);
            page.BaseTimestamp = baseTimeStamp;
            page.Token = editToken;
            return page;
        }

        public static string LoadPageFromCache(string fileName,
            string revisionId,
            string pageName)
        {
            if (File.Exists(fileName))
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                using (GZipStream gs = new GZipStream(fs, CompressionMode.Decompress))
                using (TextReader sr = new StreamReader(gs))
                {
                    string revid = sr.ReadLine();
                    if (revid == revisionId)
                    {
                        Console.Out.WriteLine("Loading " + pageName + "...");
                        return sr.ReadToEnd();
                    }
                }
            }
            return null;
        }

        public static void CachePage(string fileName, string revisionId, string text)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            using (GZipStream gs = new GZipStream(fs, CompressionMode.Compress))
            using (StreamWriter sw = new StreamWriter(gs))
            {
                sw.WriteLine(revisionId);
                sw.Write(text);
            }
        }
    }
}
