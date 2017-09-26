using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alkahest.Core.Plugins
{
    public class PluginsSharedInfo
    {
        private static PluginsSharedInfo _instance;
        public static PluginsSharedInfo Instance => _instance ?? (_instance = new PluginsSharedInfo());
        public virtual string PluginsList { get; protected set; }
        public PluginsSharedInfo()
        {
            
        }
    }


}
