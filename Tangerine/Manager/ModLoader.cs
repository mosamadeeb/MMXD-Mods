using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Manager.Loaders;
using Tangerine.Manager.Mod;
using Tangerine.Patchers;

namespace Tangerine.Manager
{
    internal static class ModLoader
    {
        internal struct LoadOrderEntry
        {
            public string name;
            public bool enabled;
        }

        private const string ModFile = "Mod.json";
        private const string ModLoadOrderFile = "ModLoadOrder.json";

        internal static readonly string ModsDir = Path.Combine(BepInEx.Paths.BepInExRootPath, "mods");
        internal static readonly string LoadOrderPath = Path.Combine(Plugin.Location, ModLoadOrderFile);

        internal static List<LoadOrderEntry> ReadLoadOrder()
        {
            if (File.Exists(LoadOrderPath))
            {
                try
                {
                    var list = JsonNode.Parse(File.ReadAllText(LoadOrderPath)).AsArray();
                    var loadOrderList = new List<LoadOrderEntry>();
                    foreach (var node in list)
                    {
                        loadOrderList.Add(new LoadOrderEntry()
                        {
                            name = node["name"].Deserialize<string>(),
                            enabled = node["enabled"].Deserialize<bool>(),
                        });
                    }
                    
                    return loadOrderList;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Failed to read {ModLoadOrderFile}: {e}");
                }
            }

            return null;
        }

        internal static void SaveLoadOrder(List<LoadOrderEntry> mods)
        {
            try
            {
                // TODO: rewrite this
                File.WriteAllText(LoadOrderPath, JsonSerializer.Serialize(mods));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to save {ModLoadOrderFile}: {e}");
            }
        }

        internal static List<TangerineMod> PreloadMods()
        {
            var loadedFolders = new HashSet<string>();
            var mods = new List<TangerineMod>();

            if (Directory.Exists(ModsDir))
            {
                var loadOrder = ReadLoadOrder();
                var loadOrderMods = loadOrder?.Select(m => m.name);
                var disabledMods = loadOrder?.Where(m => !m.enabled)?.Select(m => m.name)?.ToHashSet();

                var modFolders = Directory.EnumerateDirectories(ModsDir).Select(Path.GetFileName);

                if (loadOrderMods != null)
                {
                    // Concat sorted load order list with the rest of the existing mods
                    modFolders = loadOrderMods.Where(modFolders.Contains).Concat(modFolders.Where(m => !loadOrderMods.Contains(m)));
                }

                foreach (var folderName in modFolders)
                {
                    if (loadedFolders.Contains(folderName))
                    {
                        Plugin.Log.LogError($"Cannot add mod because it was added before: {folderName}");
                        continue;
                    }

                    var mod = CreateMod(folderName);
                    if (mod == null)
                    {
                        // Error was reported in the function itself
                        continue;
                    }

                    mods.Add(mod);
                    loadedFolders.Add(folderName);

                    if (disabledMods == null || !disabledMods.Contains(mod.Id))
                    {
                        LoadMod(mod);
                    }
                }
            }

            if (mods.Count == 0)
            {
                Plugin.Log.LogWarning($"No mods were loaded!");
            }

            return mods;
        }

        private static TangerineMod CreateMod(string folderName)
        {
            var modPath = Path.Combine(ModsDir, folderName);
            var jsonPath = Path.Combine(modPath, ModFile);
            try
            {
                if (!File.Exists(jsonPath))
                {
                    Plugin.Log.LogWarning($"Ignoring mod folder because it does not have \"{ModFile}\": {folderName}");
                    return null;
                }

                var info = new ModInfo(folderName, JsonNode.Parse(File.ReadAllText(jsonPath)).AsObject());

                return new TangerineMod(info);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to add mod \"{folderName}\": {e}");
            }

            return null;
        }

        public static void LoadMod(TangerineMod mod)
        {
            // Read files in the mod's folder and perform patches, then load plugin if it exists
            var modPath = Path.Combine(ModsDir, mod.Id);

            if (AssetBundleLoader.HasContentToLoad(modPath))
            {
                AssetBundleLoader.Load(modPath, mod.Loader);
            }

            if (AssetRemapLoader.HasContentToLoad(modPath))
            {
                AssetRemapLoader.Load(modPath, mod.Loader);
            }

            if (FileRemapLoader.HasContentToLoad(modPath))
            {
                FileRemapLoader.Load(modPath, mod.Loader);
            }

            if (TableLoader.HasContentToLoad(modPath))
            {
                TableLoader.Load(modPath, mod.DataManager, mod.TextDataManager);
            }

            if (PluginLoader.HasContentToLoad(modPath))
            {
                PluginLoader.Load(modPath, mod);
            }
        }

        public static void UnloadMod(ModInfo info)
        {
            var id = info.Id;

            TangerineCharacter.CharacterDict.OnModDisabled(id);

            AssetBundleLoader.Unload(id);
            AssetRemapLoader.Unload(id);
            FileRemapLoader.Unload(id);
            TableLoader.Unload(id);
            PluginLoader.Unload(id);
        }
    }
}
