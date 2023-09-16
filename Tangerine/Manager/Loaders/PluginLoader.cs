using System;
using System.IO;
using System.Linq;
using Tangerine.Manager.Mod;
using Tangerine.Utils;

namespace Tangerine.Manager.Loaders
{
    internal static class PluginLoader
    {
        public static bool Load(string modPath, TangerineMod mod)
        {
            try
            {
                var files = Directory.GetFiles(modPath, "*.dll");
                foreach (var file in files)
                {
                    var plugin = ScriptEngine.LoadDLL(mod.Id, file);

                    if (plugin != null)
                    {
                        plugin.Load(mod);
                        Plugin.Log.LogWarning($"Loaded plugin {Path.GetFileName(file)} for mod \"{modPath}\"");
                        return true;
                    }

                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to load plugins for mod \"{modPath}\": {e}");
            }

            return false;
        }

        public static bool Unload(string modId)
        {
            return ScriptEngine.UnloadDLL(modId);
        }

        public static bool HasContentToLoad(string modPath)
        {
            return Directory.GetFiles(modPath, "*.dll").Any();
        }
    }
}
