using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Claymore.TalkCleanupWikiBot
{
    public struct Candidate
    {
        public string RawTitle;
        public string Title;
        public bool hasVerdict;
        public string Text;
        public bool StrikenOut;
        public List<Candidate> SubSections;
        public int Level;

        internal static Candidate Parse(string text, int level)
        {
            string currentLevel = level.ToString();
            string nextLevel = (level + 1).ToString();
            Candidate candidate = new Candidate();
            candidate.hasVerdict = false;
            candidate.Level = level;
            
            string[] lines = text.Split(new char[] { '\n' });
            candidate.RawTitle = lines[0];
            Regex titleRE = new Regex(@"^={" + currentLevel + @"}([^=].+)\s*={" + currentLevel + @"}\s*$");
            Match m = titleRE.Match(lines[0]);
            if (m.Success)
            {
                candidate.Title = m.Groups[1].Value.Trim();
                candidate.StrikenOut = lines[0].Contains("<s>");
                candidate.hasVerdict = candidate.StrikenOut;
            }
            
            Regex verdictRE = new Regex(@"^={" + nextLevel + @"}\s*Итог\s*={" + nextLevel + @"}\s*$");
            foreach (string line in lines)
            {
                m = verdictRE.Match(line);
                if (m.Success)
                {
                    candidate.hasVerdict = true;
                    break;
                }
            }
            candidate.SubSections = new List<Candidate>();
            for (int i = candidate.Level + 1; i < candidate.Level + 4; ++i)
            {
                candidate.Text = "";
                nextLevel = i.ToString();
                bool found = false;
                StringBuilder message = new StringBuilder();
                Regex subSectionRE = new Regex(@"^={" + nextLevel + @"}([^=].+)\s*={" + nextLevel + @"}\s*$");
                bool skip = true;
                foreach (string line in lines)
                {
                    m = subSectionRE.Match(line);
                    if (m.Success)
                    {
                        skip = false;
                        if (message.Length > 0)
                        {
                            found = true;
                            candidate.SubSections.Add(Candidate.Parse(message.ToString(), level + 1));
                            message = new StringBuilder();
                        }
                        message.Append(line + "\n");
                    }
                    else if (!skip)
                    {
                        message.Append(line + "\n");
                    }
                    else
                    {
                        candidate.Text += line + "\n";
                    }
                }
                if (!skip && message.Length > 0)
                {
                    found = true;
                    candidate.SubSections.Add(Candidate.Parse(message.ToString(), level + 1));
                }
                else if (message.Length > 0)
                {
                    candidate.Text += message.ToString();
                }
                if (found)
                {
                    break;
                }
            }
            return candidate;
        }

        public string SubsectionsToString()
        {
            string level = (Level + 1).ToString();
            Regex titleRE = new Regex(@"^={" + level + @"}\s*(<s>)?\s*(\[{2}[^=]+\]{2})\s*(</s>)?\s*={" + level + @"}\s*$");
            StringBuilder result = new StringBuilder();
            bool remove = false;
            bool empty = true;
            foreach (Candidate candidate in SubSections)
            {
                Match m = titleRE.Match(candidate.RawTitle);
                if (m.Success)
                {
                    remove = true;
                    empty = false;
                    result.Append(" • " + candidate.Title);
                }
                string subsections = candidate.SubsectionsToString();
                if (!string.IsNullOrEmpty(subsections))
                {
                    empty = false;
                }
                result.Append(subsections);
            }
            if (!empty)
            {
                if (remove)
                {
                    result.Remove(0, 3);
                }
                if (Level == 2)
                {
                    result.Insert(0, " • <small>");
                    result.Append("</small>");
                }
            }            
            else
            {
                result = new StringBuilder();
            }
            return result.ToString();
        }

        public override string ToString()
        {
            string result = hasVerdict && !StrikenOut ? "<s>" + Title + "</s>" : Title;
            return result;
        }
    }
}
