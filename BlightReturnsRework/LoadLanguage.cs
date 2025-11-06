using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlightReturnsRework
{
    internal class LoadLanguage
    {
        internal static string assemblyDir
        {
            get
            {
                return System.IO.Path.GetDirectoryName(BlightReturnsReworkPlugin.pluginInfo.Location);
            }
        }

        internal static string languageRoot => System.IO.Path.Combine(assemblyDir, "language");

        internal static void fixme(On.RoR2.Language.orig_SetFolders orig, RoR2.Language self, System.Collections.Generic.IEnumerable<string> newFolders)
        {
            if (System.IO.Directory.Exists(languageRoot))
            {
                var dirs = System.IO.Directory.EnumerateDirectories(System.IO.Path.Combine(languageRoot), self.name);
                orig(self, newFolders.Union(dirs));
                return;
            }
            orig(self, newFolders);
        }
    }
}
