using System.Collections.Generic;
using Claymore.SharpMediaWiki;

namespace Claymore.NewPagesWikiBot
{
    internal class Portal
    {
        public string Name { get; private set; }
        private readonly List<IPortalModule> _modules;

        public Portal(string name, IEnumerable<IPortalModule> modules)
        {
            Name = name;
            _modules = new List<IPortalModule>(modules);
        }

        public void GetData(Wiki wiki)
        {
            foreach (IPortalModule module in _modules)
            {
                module.GetData(wiki);
            }
        }

        public void ProcessData(Wiki wiki)
        {
            foreach (IPortalModule module in _modules)
            {
                module.ProcessData(wiki);
            }
        }

        public void UpdatePages(Wiki wiki)
        {
            foreach (IPortalModule module in _modules)
            {
                module.UpdatePage(wiki);
            }
        }
    }
}
