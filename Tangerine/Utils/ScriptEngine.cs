using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Tangerine.Manager;
using Tangerine.Manager.Mod;

namespace Tangerine.Utils
{
    // Based on https://github.com/kremnev8/BepInEx.Debug/blob/e709f2e21efa5eea25a98337c43c11de6629c499/src/Il2CPP%20ScriptEngine/ScriptEngine/ScriptEngineBehaviour.cs
    internal static class ScriptEngine
    {
        public static readonly Dictionary<string, TangerinePlugin> loadedPlugins = new();

        public static bool UnloadDLL(string modId)
        {
            if (loadedPlugins.Remove(modId, out var plugin))
            {
                return plugin.Unload();
            }

            return false;
        }

        public static TangerinePlugin LoadDLL(string modId, string path)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ModLoader.ModsDir);
            defaultResolver.AddSearchDirectory(Path.GetDirectoryName(Plugin.Location));
            defaultResolver.AddSearchDirectory(BepInEx.Paths.ManagedPath);
            defaultResolver.AddSearchDirectory(BepInEx.Paths.BepInExAssemblyDirectory);
            Plugin.Log.Log(LogLevel.Info, $"Loading plugins from {path}");

            using var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = defaultResolver });
            dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

            using var ms = new MemoryStream();

            dll.Write(ms);
            var ass = Assembly.Load(ms.ToArray());

            foreach (Type type in GetTypesSafe(ass))
            {
                try
                {
                    if (typeof(TangerinePlugin).IsAssignableFrom(type))
                    {
                        var metadata = BepInEx.MetadataHelper.GetMetadata(type);
                        if (metadata != null)
                        {
                            var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                            var pluginInfo = IL2CPPChainloader.ToPluginInfo(typeDefinition, path);

                            IL2CPPChainloader.Instance.Plugins[metadata.GUID] = pluginInfo;

                            Plugin.Log.Log(LogLevel.Info, $"Loading {metadata.GUID}");

                            // TODO: async
                            try
                            {
                                TryRunModuleCtor(pluginInfo, ass);
                                MethodInfo loadInfo = typeof(BepInEx.PluginInfo).GetProperty(nameof(BepInEx.PluginInfo.Instance)).GetSetMethod(true);
                                TangerinePlugin inst = (TangerinePlugin)IL2CPPChainloader.Instance.LoadPlugin(pluginInfo, ass);
                                loadInfo.Invoke(pluginInfo, new object[] { inst });
                                
                                loadedPlugins[modId] = inst;

                                // Only load one plugin per assembly
                                return inst;
                            }
                            catch (Exception e)
                            {
                                Plugin.Log.LogError($"Failed to load plugin {metadata.GUID} because of exception: {e}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                }
            }

            return null;
        }

        private static void TryRunModuleCtor(BepInEx.PluginInfo plugin, Assembly assembly)
        {
            try
            {
                RuntimeHelpers.RunModuleConstructor(assembly.GetType(plugin.TypeName).Module.ModuleHandle);
            }
            catch (Exception e)
            {
                Plugin.Log.Log(LogLevel.Warning,
                    $"Couldn't run Module constructor for {assembly.FullName}::{plugin.TypeName}: {e}");
            }
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sbMessage = new StringBuilder();
                sbMessage.AppendLine("\r\n-- LoaderExceptions --");
                foreach (var l in ex.LoaderExceptions)
                    sbMessage.AppendLine(l.ToString());
                sbMessage.AppendLine("\r\n-- StackTrace --");
                sbMessage.AppendLine(ex.StackTrace);
                Plugin.Log.LogError(sbMessage.ToString());
                return ex.Types.Where(x => x != null);
            }
        }
    }
}
