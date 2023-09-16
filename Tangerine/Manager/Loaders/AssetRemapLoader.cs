using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Patchers;

namespace Tangerine.Manager.Loaders
{
    internal static class AssetRemapLoader
    {
        private struct AssetRemap
        {
            public string bundleName;
            public string assetName;
            public string newBundleName;
            public string newAssetName;
        }

        private const string JsonFile = "AssetRemap.json";

        public static bool Load(string modPath, TangerineLoader loader)
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(Path.Combine(modPath, JsonFile)));
                var list = node["ListAsset"]?.AsArray();

                if (list == null)
                {
                    Plugin.Log.LogError($"Failed to read {JsonFile} for mod \"{modPath}\"");
                    return false;
                }

                foreach (var remap in list.Select(DeserializeAssetRemap))
                {
                    // No concrete way of verifying the assets bundles exist before the vanilla abconfig is loaded
                    loader.RemapAsset(
                        remap.bundleName,
                        remap.assetName,
                        remap.newBundleName,
                        remap.newAssetName);
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
            TangerineLoader.AssetRemapping.OnModDisabled(modId);
        }

        public static bool HasContentToLoad(string modPath)
        {
            return File.Exists(Path.Combine(modPath, JsonFile));
        }

        private static AssetRemap DeserializeAssetRemap(JsonNode node)
        {
            return new AssetRemap()
            {
                bundleName = node["bundleName"].Deserialize<string>(),
                assetName = node["assetName"].Deserialize<string>(),
                newBundleName = node["newBundleName"].Deserialize<string>(),
                newAssetName = node["newAssetName"].Deserialize<string>(),
            };
        }
    }
}
