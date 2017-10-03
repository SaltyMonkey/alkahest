using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using Alkahest.Core.Logging;
using Alkahest.Core.Net;

namespace Alkahest.Core.Plugins
{
    public sealed class PluginLoader
    {
        const string ClassSuffix = "Plugin";

        const string NamespacePrefix = "Alkahest.Plugins.";

        static readonly Log _log = new Log(typeof(PluginLoader));

        public IReadOnlyCollection<IPlugin> Plugins => _plugins;

        readonly IPlugin[] _plugins;
        
        public PluginLoader(string directory, string pattern, string[] exclude)
        {
            Directory.CreateDirectory(directory);

            using (var container = new CompositionContainer(
                new DirectoryCatalog(directory, pattern), true))
                    _plugins = container.GetExports<IPlugin>()
                        .Select(x => x.Value)
                        .Where(x => !exclude.Contains(x.Name))
                        .ToArray();

            foreach (var plugin in _plugins)
                EnforceConventions(plugin);
        }

        static void EnforceConventions(IPlugin plugin)
        {
            var name = plugin.Name;

            if (name.Any(c => char.IsUpper(c)))
                throw new PluginException($"{name}: Plugin name must not contain upper case characters.");

            var type = plugin.GetType();

            if (!type.Name.EndsWith(ClassSuffix))
                throw new PluginException($"{name}: Plugin class name must end with '{ClassSuffix}'.");

            if (!type.Namespace.StartsWith(NamespacePrefix))
                throw new PluginException($"{name}: Plugin namespace must start with '{NamespacePrefix}'.");

            var asm = type.Assembly;
            var asmName = $"alkahest-{name}";

            if (asm.GetName().Name != asmName)
                throw new PluginException($"{name}: Plugin assembly name must be '{asmName}'.");

            var fileName = asmName + ".dll";

            if (Path.GetFileName(asm.Location.ToLowerInvariant()) != fileName)
                throw new PluginException($"{name}: Plugin file name must be '{fileName}'.");
        }

        static void CheckProxies(GameProxy[] proxies)
        {
            if (proxies == null)
                throw new ArgumentNullException(nameof(proxies));

            if (proxies.Any(x => x == null))
                throw new ArgumentException("A null proxy was given.", nameof(proxies));
        }

        public void Start(GameProxy[] proxies)
        {
            CheckProxies(proxies);

            foreach (var p in _plugins)
            {
                p.Start(proxies.ToArray());

                _log.Info("Started plugin {0}", p.Name);
                PluginsSharedInfo.Instance.PluginsList.Add(p.Name);
            }
            
            _log.Basic("Started {0} plugins", _plugins.Length);
            _log.Info("Building queue for hooks...");
            foreach (var proxy in proxies)
            {
                proxy.Processor.UpdatePrioritiesForHandlers();
            }
            _log.Info("Hooks queues builded");
        }

        public void Stop(GameProxy[] proxies)
        {
            CheckProxies(proxies);
            foreach (var p in _plugins)
            {
               
                p.Stop(proxies.ToArray());

                _log.Info("Stopped plugin {0}", p.Name);
            }
        }
    }
}
