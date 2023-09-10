using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace Tangerine
{
    public static class TangerineLoader
    {
        private static Harmony _harmony;

        private static bool _assetsBundleManagerUnpatched = false;

        // assetbundleName: id
        private static readonly Dictionary<string, AssetbundleId> _assetBundleIds = new();

        // id.hash: filePath
        private static readonly Dictionary<string, string> _assetBundlePaths = new();

        /// <summary>
        /// Adds an Asset Bundle to the game's dictionary to allow it to be loaded from a custom path.
        /// Asset bundles existing in the game will be overridden if a bundle with the same name is added.
        /// </summary>
        /// <param name="id">Id entry to add</param>
        /// <param name="filePath">The path the file will be loaded from</param>
        public static void AddAssetBundleId(AssetbundleId id, string filePath)
        {
            id.SetKeys();
            _assetBundleIds[id.name] = id;
            _assetBundlePaths[id.hash] = filePath;

            if (_assetsBundleManagerUnpatched)
            {
                // Update the game's dictionary
                AssetsBundleManager.Instance.dictBundleID[id.name] = id;
            }
        }

        /// <param name="idDict"><c>Key: Value</c> mapping for <see cref="AssetbundleId"/></param>
        /// <inheritdoc cref="AddAssetBundleId(AssetbundleId, string)"/>
        public static void AddAssetBundleId(Dictionary<string, object> idDict, string filePath)
        {
            AddAssetBundleId(CreateAssetbundleIdFromDict(idDict), filePath);
        }

        /// <summary>
        /// Adds a file hash to be loaded from a custom path when the game asks for the file.
        /// This method is for adding files that are not asset bundles, like <c>.acb</c> files.
        /// Use <see cref="AddAssetBundleId(AssetbundleId, string)"/> for adding asset bundles.
        /// </summary>
        /// <param name="hash">MD5 hash of the file name that the game will search for</param>
        /// <inheritdoc cref="AddAssetBundleId(AssetbundleId, string)"/>
        public static void AddFile(string hash, string filePath)
        {
            _assetBundlePaths[hash] = filePath;
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            _harmony = harmony;
            _harmony.PatchAll(typeof(TangerineLoader));
        }

        private static AssetbundleId CreateAssetbundleIdFromDict(Dictionary<string, object> dict)
        {
            return new AssetbundleId((string)dict["name"], (string)dict["hash"], (uint)dict["crc"], (long)dict["size"]);
        }

        [HarmonyPatch(typeof(AssetsBundleManager), nameof(AssetsBundleManager.OnStartLoadSingleAsset))]
        [HarmonyPostfix]
        public static void AddAssetbundleIdsPatch(AssetsBundleManager __instance, MethodBase __originalMethod)
        {
            // This will override existing bundle ids in the manager's dict
            foreach (var id in _assetBundleIds.Values)
            {
                __instance.dictBundleID[id.name] = id;
                Plugin.Log.LogWarning($"Added AssetbundleId: [{id.hash}] {id.name}");
            }

            Plugin.Log.LogWarning($"Unpatching {nameof(AssetsBundleManager)} postfix");
            _harmony.Unpatch(__originalMethod, MethodBase.GetCurrentMethod() as MethodInfo);
            _assetsBundleManagerUnpatched = true;
        }

        [HarmonyPatch(typeof(AssetsBundleManager), nameof(AssetsBundleManager.GetPath))]
        [HarmonyPostfix]
        private static void GetPathPostfix(string file, ref string __result)
        {
            if (_assetBundlePaths.TryGetValue(file, out var filePath))
            {
                // Update file path
                __result = filePath;
                Plugin.Log.LogWarning($"Replaced path for file: ({__result})");
            }
        }
    }
}
