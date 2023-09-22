using HarmonyLib;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tangerine.Patchers.Native
{
    internal static class Detour_LoadAsset
    {
        [Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Fastcall)]
        public unsafe delegate void AsyncLoadDel(IntPtr _this, IntPtr bundleName, IntPtr assetName, IntPtr p_cb, IntPtr keepMode, IntPtr methodInfo);

        [Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Fastcall)]
        public unsafe delegate IntPtr SyncLoadDel(IntPtr _this, IntPtr bundleName, IntPtr assetName, IntPtr methodInfo);

        private static IHook<AsyncLoadDel> hookGetAssetAndAsyncLoad;
        private static IHook<SyncLoadDel> hookGetAssstSync;

        public unsafe static void Patch()
        {
            var getAssetAndAsyncLoad = GetMethodPtr(AccessTools.Method(typeof(AssetsBundleManager), nameof(AssetsBundleManager.GetAssetAndAsyncLoad), null, new Type[] { typeof(UnityEngine.Object) }));
            var getAssstSync = GetMethodPtr(AccessTools.Method(typeof(AssetsBundleManager), nameof(AssetsBundleManager.GetAssstSync), null, new Type[] { typeof(UnityEngine.Object) }));
            
            hookGetAssetAndAsyncLoad = ReloadedHooks.Instance.CreateHook<AsyncLoadDel>(AsyncLoadDetour, (long)getAssetAndAsyncLoad).Activate();
            hookGetAssstSync = ReloadedHooks.Instance.CreateHook<SyncLoadDel>(SyncLoadDetour, (long)getAssstSync).Activate();
        }

        private unsafe static void AsyncLoadDetour(IntPtr _this, IntPtr bundleNamePtr, IntPtr assetNamePtr, IntPtr p_cb, IntPtr keepMode, IntPtr methodInfo)
        {
            if (!Detour(ref bundleNamePtr, ref assetNamePtr, out var loadedAsset))
            {
                // Invoke callback
                // This switch is done because last time I tried (with harmony) the game would die if I returned UnityEngine.Object instead of the proper type
                switch (loadedAsset)
                {
                    case GameObject gameObj:
                        Plugin.Log.LogWarning($"Invoking GameObject callback: {gameObj.name}");
                        new AssetsBundleManager.OnAsyncLoadAssetComplete<GameObject>(p_cb).Invoke(gameObj);
                        break;
                    case Sprite sprite:
                        Plugin.Log.LogWarning($"Invoking Sprite callback: {sprite.name}");
                        new AssetsBundleManager.OnAsyncLoadAssetComplete<Sprite>(p_cb).Invoke(sprite);
                        break;
                    case Texture2D texture:
                        Plugin.Log.LogWarning($"Invoking Texture2D callback: {texture.name}");
                        new AssetsBundleManager.OnAsyncLoadAssetComplete<Texture2D>(p_cb).Invoke(texture);
                        break;
                    case UnityEngine.Object obj:
                        Plugin.Log.LogWarning($"Invoking Object callback: {obj.name}");
                        new AssetsBundleManager.OnAsyncLoadAssetComplete<UnityEngine.Object>(p_cb).Invoke(obj);
                        break;
                    default:
                        Plugin.Log.LogError($"Failed to invoke unknown asset callback with type {loadedAsset.GetType().Name}");
                        hookGetAssetAndAsyncLoad.OriginalFunction.Invoke(_this, bundleNamePtr, assetNamePtr, p_cb, keepMode, methodInfo);
                        break;
                }
            }
            else
            {
                hookGetAssetAndAsyncLoad.OriginalFunction.Invoke(_this, bundleNamePtr, assetNamePtr, p_cb, keepMode, methodInfo);
            }
        }

        private unsafe static IntPtr SyncLoadDetour(IntPtr _this, IntPtr bundleNamePtr, IntPtr assetNamePtr, IntPtr methodInfo)
        {
            if (!Detour(ref bundleNamePtr, ref assetNamePtr, out var loadedAsset, true))
            {
                return loadedAsset.Pointer;
            }
            else
            {
                return hookGetAssstSync.OriginalFunction.Invoke(_this, bundleNamePtr, assetNamePtr, methodInfo);
            }
        }

        // Returns true if the original should be run; false if it should be skipped
        private static bool Detour(ref IntPtr bundleNamePtr, ref IntPtr assetNamePtr, out UnityEngine.Object outAsset, bool isSync = false)
        {
            outAsset = null;

            var bundleName = IL2CPP.Il2CppStringToManaged(bundleNamePtr);
            var assetName = IL2CPP.Il2CppStringToManaged(assetNamePtr);

            if (TangerineLoader.AssetRemapping.Base.TryGetValue((bundleName, assetName), out var target))
            {
                var newBundleName = target.Item1;
                var newAssetName = target.Item2;

                if (newBundleName != string.Empty)
                {
                    Plugin.Log.LogWarning($"Remapping asset from [{bundleName}]{assetName} to [{newBundleName}]{newAssetName}");

                    bundleNamePtr = IL2CPP.il2cpp_string_new(newBundleName);
                    assetNamePtr = IL2CPP.il2cpp_string_new(target.Item2);

                    // GetAssstSync only
                    if (isSync && !AssetsBundleManager.Instance.dictBundleInfo.ContainsKey(newBundleName))
                    {
                        Plugin.Log.LogWarning($"Loading missing bundle [{newBundleName}]");

                        if (AssetsBundleManager.Instance.dictBundleID.TryGetValue(newBundleName, out var bundleId))
                        {
                            // This blocks
                            LoadAssetBundle(bundleId);
                        }
                    }
                }
                else
                {
                    // Empty bundle name means the asset should be loaded from disk
                    Plugin.Log.LogWarning($"Loading asset from disk: \"{newAssetName}\"");

                    if (!File.Exists(newAssetName))
                    {
                        Plugin.Log.LogError($"Asset does not exist on disk: \"{newAssetName}\"");
                    }

                    // Load asset from disk
                    switch (Path.GetExtension(newAssetName))
                    {
                        case ".png":
                        case ".jpg":
                            if (!ParseTextureAssetName(newAssetName, out var width, out var height, out var isSprite))
                            {
                                Plugin.Log.LogError($"Failed to parse texture asset name: \"{newAssetName}\"");
                                break;
                            }

                            try
                            {
                                // TODO: Change format?
                                var texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
                                var array = File.ReadAllBytes(newAssetName);
                                ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(array));

                                if (!isSprite)
                                {
                                    outAsset = texture;
                                }
                                else
                                {
                                    var rect = new Rect(0.0f, 0.0f, texture.width, texture.height);
                                    var vect = new Vector2(0.5f, 0.5f);
                                    outAsset = Sprite_Create(texture, rect, vect, 100f, 0, SpriteMeshType.Tight, Vector4.zero, false);
                                }
                            }
                            catch (Exception e)
                            {
                                Plugin.Log.LogError($"Failed to load texture: {e}");
                                return true;
                            }


                            Plugin.Log.LogWarning($"Successfully loaded texture: \"{newAssetName}\"");

                            return false;
                        default:
                            break;
                    }
                }
            }

            return true;
        }

        private static AssetBundle LoadAssetBundle(AssetbundleId id)
        {
            foreach (var dependency in AssetsBundleManager.Instance.manifest.GetAllDependencies(id.name))
            {
                if (!AssetsBundleManager.Instance.dictBundleInfo.ContainsKey(dependency))
                {
                    Plugin.Log.LogWarning($"Loading dependency [{dependency}] for bundle [{id.name}]!");
                    if (AssetsBundleManager.Instance.dictBundleID.TryGetValue(dependency, out var dependencyId))
                    {
                        LoadAssetBundle(dependencyId);
                    }
                    else
                    {
                        Plugin.Log.LogError($"Dependency [{dependency}] is missing!");
                    }
                }
            }

            string text2 = AssetsBundleManager.Instance.GetPath(DataPathEnum.StreamingAssetsDownloadData, id.hash);
            if (File.Exists(text2))
            {
                /*
                if (id.size > AssetsBundleManager.Instance.TRIGGER_GC_MB)
                {
                    GC.Collect();
                }
                */

                byte[] array = File.ReadAllBytes(text2);
                DecryptBundle(id.Keys, ref array);

                // This blocks
                var assetBundle = AssetBundle.LoadFromMemory(new Il2CppStructArray<byte>(array));
                if (assetBundle != null)
                {
                    var assetbundleInfo = new AssetbundleInfo(assetBundle, AssetKeepMode.KEEP_IN_SCENE);
                    AssetsBundleManager.Instance.dictBundleInfo.Add(id.name, assetbundleInfo);
                }

                return assetBundle;
            }

            return null;
        }

        private static void DecryptBundle(byte[] keys, ref byte[] bytes)
        {
            int num = keys.Length;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte[] array = bytes;
                int num2 = i;
                array[num2] ^= keys[i % num];
            }
        }

        private static bool ParseTextureAssetName(string name, out int width, out int height, out bool isSprite)
        {
            // TODO: Move this feature into its own json file
            // File name format: <original_name>.<width>x<height>.sprite.png

            width = 0;
            height = 0;

            name = Path.GetFileNameWithoutExtension(name);
            isSprite = Path.GetExtension(name) == ".sprite";

            if (isSprite)
            {
                name = Path.GetFileNameWithoutExtension(name);
            }

            try
            {
                var array = Path.GetExtension(name)[1..].Split('x');
                if (array.Length == 2)
                {
                    width = int.Parse(array[0]);
                    height = int.Parse(array[1]);
                    return true;
                }
            }
            catch
            {

            }

            return false;
        }

        private unsafe static IntPtr GetMethodPtr(MethodInfo methodInfo)
        {
            return UnityVersionHandler.Wrap(
                (Il2CppMethodInfo*)(nint)Il2CppInteropUtils
                .GetIl2CppMethodInfoPointerFieldForGeneratedMethod(methodInfo)
                .GetValue(null))
                .MethodPointer;
        }

        // String.Format was stripped from the game, so this is an implementation of the unhollowed method
        internal static Sprite Sprite_Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border, bool generateFallbackPhysicsShape)
        {
            bool flag = texture == null;
            Sprite sprite;
            if (flag)
            {
                sprite = null;
            }
            else
            {
                bool flag2 = rect.xMax > (float)texture.width || rect.yMax > (float)texture.height;
                if (flag2)
                {
                    throw new ArgumentException(string.Format("Could not create sprite ({0}, {1}, {2}, {3}) from a {4}x{5} texture.", rect.x, rect.y, rect.width, rect.height, texture.width, texture.height));
                }
                bool flag3 = pixelsPerUnit <= 0f;
                if (flag3)
                {
                    throw new ArgumentException("pixelsPerUnit must be set to a positive non-zero value.");
                }
                sprite = Sprite.CreateSprite(texture, rect, pivot, pixelsPerUnit, extrude, meshType, border, generateFallbackPhysicsShape);
            }
            return sprite;
        }
    }
}
