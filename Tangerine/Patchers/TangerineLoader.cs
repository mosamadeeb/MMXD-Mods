using Fasterflect;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Tangerine.Manager;

namespace Tangerine.Patchers
{
    public class TangerineLoader
    {
        private static Harmony _harmony;

        private static bool _assetsBundleManagerUnpatched = false;

        // assetbundleName: id
        private static readonly Dictionary<string, AssetbundleId> _originalAssetBundleIds = new();
        internal static readonly ModDictionary<string, AssetbundleId> AssetBundleIds = new();

        // id.hash: bundleFilePath
        internal static readonly ModDictionary<string, string> AssetBundlePaths = new();

        // hash: filePath
        internal static readonly ModDictionary<string, string> FilePaths = new();

        // (oldBundleName, oldAssetName): (newBundleName, newAssetName)
        internal static readonly ModDictionary<(string, string), (string, string)> AssetRemapping = new();

        private readonly string _modGuid;

        static TangerineLoader()
        {
            AssetBundleIds.BaseChangedEvent += ApplyBundlePatch;
            AssetBundleIds.BaseResetEvent += ResetBundlePatch;
        }

        internal TangerineLoader(string modGuid)
        {
            _modGuid = modGuid;
        }

        /// <summary>
        /// Adds an Asset Bundle to the game's dictionary to allow it to be loaded from a custom path.
        /// Asset bundles existing in the game will be overridden if a bundle with the same name is added.
        /// </summary>
        /// <param name="id">Id entry to add</param>
        /// <param name="filePath">The path the file will be loaded from</param>
        public void AddAssetBundleId(AssetbundleId id, string filePath)
        {
            id.SetKeys();
            AssetBundleIds.Set(_modGuid, id.name, id);
            AssetBundlePaths.Set(_modGuid, id.hash, filePath);

            // No need to apply anything to the game here, as the event in the Base dictionary will do it
        }

        /// <param name="idDict"><c>Key: Value</c> mapping for <see cref="AssetbundleId"/></param>
        /// <inheritdoc cref="AddAssetBundleId(AssetbundleId, string)"/>
        public void AddAssetBundleId(Dictionary<string, object> idDict, string filePath)
        {
            AddAssetBundleId(CreateAssetbundleIdFromDict(idDict), filePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool RemoveAssetBundleId(string name)
        {
            if (AssetBundleIds.TryGetValue(_modGuid, name, out var id))
            {
                AssetBundlePaths.Remove(_modGuid, id.hash);
                AssetBundleIds.Remove(_modGuid, name);

                // No need to apply anything to the game here, as the event in the Base dictionary will do it
                return true;
            }

            return false;
        }

        private static void ApplyBundlePatch(string name, BaseChangeType changeType)
        {
            if (!_assetsBundleManagerUnpatched)
            {
                return;
            }

            AssetbundleId id;
            switch (changeType)
            {
                case BaseChangeType.Add:
                    if (AssetsBundleManager.Instance.dictBundleID.TryGetValue(name, out id))
                    {
                        // Store original for restoring later
                        _originalAssetBundleIds[name] = id;
                    }
                    goto case BaseChangeType.Update;
                case BaseChangeType.Update:
                    // Update the game's dictionary
                    AssetsBundleManager.Instance.dictBundleID[name] = AssetBundleIds.Base[name];
                    break;
                case BaseChangeType.Remove:
                    if (_originalAssetBundleIds.TryGetValue(name, out id))
                    {
                        // Patch original value back
                        AssetsBundleManager.Instance.dictBundleID[name] = id;
                    }
                    else
                    {
                        AssetsBundleManager.Instance.dictBundleID.Remove(name);
                    }
                    break;
            }
        }

        private static void ResetBundlePatch(IEnumerable<string> names)
        {
            if (!_assetsBundleManagerUnpatched)
            {
                return;
            }

            var managerDict = AssetsBundleManager.Instance.dictBundleID;

            // Unpatch existing Base
            foreach (string name in names)
            {
                managerDict.Remove(name);
            }

            // Patch original values back
            foreach (var pair in _originalAssetBundleIds)
            {
                managerDict[pair.Key] = pair.Value;
            }

            // Reset original dict and fill it again based on the new Base
            _originalAssetBundleIds.Clear();

            // Patch new Base
            foreach (var pair in AssetBundleIds.Base)
            {
                if (managerDict.TryGetValue(pair.Key, out var value))
                {
                    _originalAssetBundleIds[pair.Key] = value;
                }

                managerDict[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Adds a file hash to be loaded from a custom path when the game asks for the file.
        /// This method is for adding files that are not asset bundles, like <c>.acb</c> files.
        /// Use <see cref="AddAssetBundleId(AssetbundleId, string)"/> for adding asset bundles.
        /// </summary>
        /// <param name="hash">MD5 hash of the file name that the game will search for</param>
        /// <inheritdoc cref="AddAssetBundleId(AssetbundleId, string)"/>
        public void AddFile(string hash, string filePath)
        {
            FilePaths.Set(_modGuid, hash, filePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool RemoveFile(string hash)
        {
            return FilePaths.Remove(_modGuid, hash);
        }

        /// <summary>
        /// Remaps an asset from an existing asset bundle to another
        /// </summary>
        /// <param name="oldBundleName">Name (not hash) of original bundle</param>
        /// <param name="oldAssetName">Name of original asset to remap</param>
        /// <param name="newBundleName">Name (not hash) of target bundle, which must be added first using <see cref="AddAssetBundleId(AssetbundleId, string)"/></param>
        /// <param name="newAssetName">Name of target asset in the target bundle</param>
        public void RemapAsset(string oldBundleName, string oldAssetName, string newBundleName, string newAssetName)
        {
            AssetRemapping.Set(_modGuid, (oldBundleName, oldAssetName), (newBundleName, newAssetName));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldBundleName"></param>
        /// <param name="oldAssetName"></param>
        /// <returns></returns>
        public bool RemoveRemapping(string oldBundleName, string oldAssetName)
        {
            return AssetRemapping.Remove(_modGuid, (oldBundleName, oldAssetName));
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            _harmony = harmony;
            _harmony.PatchAll(typeof(TangerineLoader));

            _harmony.Patch(
                typeof(AssetsBundleManager).Method(nameof(AssetsBundleManager.GetAssetAndAsyncLoad)).MakeGenericMethod(typeof(UnityEngine.Object)),
                prefix: new HarmonyMethod(typeof(TangerineLoader).GetMethod(nameof(AsyncLoadAssetObjectPrefix), BindingFlags.NonPublic | BindingFlags.Static)));

            _harmony.Patch(
                typeof(AssetsBundleManager).Method(nameof(AssetsBundleManager.GetAssstSync)).MakeGenericMethod(typeof(UnityEngine.Object)),
                prefix: new HarmonyMethod(typeof(TangerineLoader).GetMethod(nameof(LoadAssetPrefix), BindingFlags.NonPublic | BindingFlags.Static)));
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
            foreach (var id in AssetBundleIds.Base.Values)
            {
                if (__instance.dictBundleID.TryGetValue(id.name, out var orgId))
                {
                    // Store original for restoring later
                    _originalAssetBundleIds[id.name] = orgId;
                    Plugin.Log.LogWarning($"Updating AssetbundleId: [{id.hash}] {id.name}");
                }
                else
                {
                    Plugin.Log.LogWarning($"Adding AssetbundleId: [{id.hash}] {id.name}");
                }

                __instance.dictBundleID[id.name] = id;
            }

            Plugin.Log.LogWarning($"Unpatching {nameof(AssetsBundleManager)} postfix");
            _harmony.Unpatch(__originalMethod, MethodBase.GetCurrentMethod() as MethodInfo);
            _assetsBundleManagerUnpatched = true;
        }

        [HarmonyPatch(typeof(AssetsBundleManager), nameof(AssetsBundleManager.GetPath))]
        [HarmonyPostfix]
        private static void GetPathPostfix(string file, ref string __result)
        {
            string filePath;
            if (AssetBundlePaths.Base.TryGetValue(file, out filePath) || FilePaths.Base.TryGetValue(file, out filePath))
            {
                // Update file path
                __result = filePath;
                Plugin.Log.LogWarning($"Replaced path for file: ({__result})");
            }
        }

        private static void LoadAssetPrefix(ref string bundleName, ref string assetName)
        {
            if (AssetRemapping.Base.TryGetValue((bundleName, assetName), out var target))
            {
                Plugin.Log.LogWarning($"Remapped asset from [{bundleName}]{assetName} to [{target.Item1}]{target.Item2}");
                bundleName = target.Item1;
                assetName = target.Item2;
            }
        }

        private static void AsyncLoadAssetObjectPrefix(ref string bundleName, ref string assetName)
        {
            if (AssetRemapping.Base.TryGetValue((bundleName, assetName), out var target))
            {
                Plugin.Log.LogWarning($"Remapped asset from [{bundleName}]{assetName} to [{target.Item1}]{target.Item2}");
                bundleName = target.Item1;
                assetName = target.Item2;
            }

            /*
            // Example for updating callback
            var p_cb_org = p_cb;

            p_cb = (AssetsBundleManager.OnAsyncLoadAssetComplete<UnityEngine.Object>)
                new Action<UnityEngine.Object>(
                    (asset) =>
                    {
                        p_cb_org.Invoke(asset);
                    }
                );
            */
        }

        [HarmonyPatch(typeof(AssetbundleId), nameof(AssetbundleId.SetKeys))]
        [HarmonyPrefix]
        private static bool SetKeysPrefix(AssetbundleId __instance)
        {
            if (__instance.crc == 0)
            {
                __instance.Keys = new byte[] { 0 };
                return false;
            }

            return true;
        }
    }
}
