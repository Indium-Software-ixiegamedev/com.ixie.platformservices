using System.Collections.Generic;

namespace PlatformServices.Core
{
    /// <summary>Holds installers contributed by optional assemblies, which register themselves at load time.</summary>
    public static class PlatformFeatureInstallerRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IPlatformFeatureInstaller> Installers = new List<IPlatformFeatureInstaller>();

        /// <summary>Adds an installer, replacing any previously registered installer of the same type.</summary>
        public static void Register(IPlatformFeatureInstaller installer)
        {
            if (installer == null)
                return;

            lock (SyncRoot)
            {
                Installers.RemoveAll(existing => existing.GetType() == installer.GetType());
                Installers.Add(installer);
                Installers.Sort((left, right) => left.Order.CompareTo(right.Order));
            }
        }

        /// <summary>Returns a snapshot of the installers in install order.</summary>
        public static IReadOnlyList<IPlatformFeatureInstaller> GetInstallers()
        {
            lock (SyncRoot)
                return Installers.ToArray();
        }
    }
}
