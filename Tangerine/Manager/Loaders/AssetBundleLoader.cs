using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Patchers;
using static EventManager;

namespace Tangerine.Manager.Loaders
{
    internal static class AssetBundleLoader
    {
        private const string JsonFile = "AssetBundleConfig.json";
        private const string AssetBundleFolder = "AssetBundles";
        private static readonly string DownloadFolder = Path.Combine("StreamingAssets", "DownloadData");

        public static bool Load(string modPath, TangerineLoader loader)
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(Path.Combine(modPath, JsonFile)));
                var list = node["ListAssetbundleId"]?.AsArray();

                if (list == null)
                {
                    Plugin.Log.LogError($"Failed to read {JsonFile} for mod \"{modPath}\"");
                    return false;
                }

                var assetBundleFolder = Path.Combine(modPath, AssetBundleFolder);

                foreach (var id in list.Select(DeserializeAssetBundleId))
                {
                    var bundleNamePath = Path.Combine(assetBundleFolder, id.name.Replace('/', Path.DirectorySeparatorChar));
                    var bundleHashPath = Path.Combine(assetBundleFolder, id.hash);
                    if (File.Exists(bundleNamePath))
                    {
                        // Prioritize loading from real file name
                        loader.AddAssetBundleId(id, bundleNamePath);
                    }
                    else if (File.Exists(bundleHashPath))
                    {
                        // Next option, load from hash name in mod folder
                        loader.AddAssetBundleId(id, bundleHashPath);
                    }
                    else if (File.Exists(Path.Combine(BepInEx.Paths.GameDataPath, DownloadFolder, id.hash)))
                    {
                        // Fall back to game folder if neither of the above exist (this can be used to modify hash, crc, and size of existing vanilla bundles)
                        Plugin.Log.LogWarning($"Custom bundle {id.name} does not exist in \"{Path.Combine(Path.GetFileName(modPath), AssetBundleFolder)}\". Falling back to game's DownloadData folder");
                        loader.AddAssetBundleId(id, Path.Combine(BepInEx.Paths.GameDataPath, DownloadFolder, id.hash));
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Failed to add bundle {id.name} for mod \"{modPath}\": No bundle exists on disk");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to read {JsonFile} for mod \"{modPath}\": {e}");
                return false;
            }
            
            return true;
        }

        public static void Unload(string modId)
        {
            TangerineLoader.AssetBundleIds.OnModDisabled(modId);
            TangerineLoader.AssetBundlePaths.OnModDisabled(modId);
        }

        public static bool HasContentToLoad(string modPath)
        {
            return File.Exists(Path.Combine(modPath, JsonFile));
        }

        private static AssetbundleId DeserializeAssetBundleId(JsonNode node)
        {
            return new AssetbundleId(
                node["name"].Deserialize<string>(),
                node["hash"].Deserialize<string>(),
                node["crc"].Deserialize<uint>(),
                node["size"].Deserialize<long>()
            );
        }
    }
}
