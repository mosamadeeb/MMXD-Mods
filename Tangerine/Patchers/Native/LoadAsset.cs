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
        public unsafe delegate IntPtr AsyncLoadDel(IntPtr _this, IntPtr bundleName, IntPtr assetName, IntPtr p_cb, IntPtr keepMode, IntPtr methodInfo);

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

        private unsafe static IntPtr AsyncLoadDetour(IntPtr _this, IntPtr bundleNamePtr, IntPtr assetNamePtr, IntPtr p_cb, IntPtr keepMode, IntPtr methodInfo)
        {
            Detour(ref bundleNamePtr, ref assetNamePtr);
            return hookGetAssetAndAsyncLoad.OriginalFunction.Invoke(_this, bundleNamePtr, assetNamePtr, p_cb, keepMode, methodInfo);
        }

        private unsafe static IntPtr SyncLoadDetour(IntPtr _this, IntPtr bundleNamePtr, IntPtr assetNamePtr, IntPtr methodInfo)
        {
            Detour(ref bundleNamePtr, ref assetNamePtr, true);
            return hookGetAssstSync.OriginalFunction.Invoke(_this, bundleNamePtr, assetNamePtr, methodInfo);
        }

        private static void Detour(ref IntPtr bundleNamePtr, ref IntPtr assetNamePtr, bool isSync = false)
        {
            var bundleName = IL2CPP.Il2CppStringToManaged(bundleNamePtr);
            var assetName = IL2CPP.Il2CppStringToManaged(assetNamePtr);

            if (TangerineLoader.AssetRemapping.Base.TryGetValue((bundleName, assetName), out var target))
            {
                Plugin.Log.LogWarning($"Remapping asset from [{bundleName}]{assetName} to [{target.Item1}]{target.Item2}");

                bundleNamePtr = IL2CPP.il2cpp_string_new(target.Item1);
                assetNamePtr = IL2CPP.il2cpp_string_new(target.Item2);

                // GetAssstSync only
                if (isSync && !AssetsBundleManager.Instance.dictBundleInfo.ContainsKey(target.Item1))
                {
                    Plugin.Log.LogWarning($"Loading missing bundle [{target.Item1}]");

                    if (AssetsBundleManager.Instance.dictBundleID.TryGetValue(target.Item1, out var bundleId))
                    {
                        // This blocks
                        LoadAssetBundle(bundleId);
                    }
                }
            }
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

        private unsafe static IntPtr GetMethodPtr(MethodInfo methodInfo)
        {
            return UnityVersionHandler.Wrap(
                (Il2CppMethodInfo*)(nint)Il2CppInteropUtils
                .GetIl2CppMethodInfoPointerFieldForGeneratedMethod(methodInfo)
                .GetValue(null))
                .MethodPointer;
        }
    }
}
