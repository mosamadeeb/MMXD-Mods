using BepInEx.Unity.IL2CPP.Utils.Collections;
using CallbackDefs;
using System;
using System.Collections;
using Tangerine.Patchers;
using UnityEngine;

namespace Tangerine.Manager
{
    internal class ModManagerBehaviour : MonoBehaviour
    {
        public ModManagerBehaviour(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            if (Input.GetKeyDown(ManagerConfig.ReloadKey.Value))
            {
                foreach (var mod in ModManager.GetModsToReload())
                {
                    Plugin.Log.LogMessage($"Reloading mod: {mod}");
                    ModManager.ReloadMod(mod);
                }
            }

            if (Input.GetKeyDown(ManagerConfig.BackToHometopKey.Value))
            {
                if (OrangeSceneManager.Instance.NowScene == "title" || OrangeSceneManager.Instance.NowScene == "switch")
                {
                    Plugin.Log.LogError($"Cannot switch to Hometop scene from Title!");
                }
                else
                {
                    Plugin.Log.LogMessage($"Going back to Hometop");
                    switch (ManagerConfig.BackToSceneReloadMode.Value)
                    {
                        case ManagerConfig.ReloadMode.BackToHome:
                        case ManagerConfig.ReloadMode.Both:
                            ChangeSceneWithReload("hometop", OrangeSceneManager.LoadingType.BLACK);
                            break;
                        default:
                            AudioManager.Instance.StopAllVoice();
                            OrangeSceneManager.Instance.ChangeScene("hometop", OrangeSceneManager.LoadingType.BLACK, p_skipSameScene: false);
                            break;
                    }
                }
            }
            else if (Input.GetKeyDown(ManagerConfig.BackToTitleKey.Value))
            {
                Plugin.Log.LogMessage($"Going back to Title");
                switch (ManagerConfig.BackToSceneReloadMode.Value)
                {
                    case ManagerConfig.ReloadMode.BackToTitle:
                    case ManagerConfig.ReloadMode.Both:
                        ChangeSceneWithReload("title");
                        break;
                    default:
                        AudioManager.Instance.StopAllVoice();
                        OrangeSceneManager.Instance.ChangeScene("title", p_skipSameScene: false);
                        break;
                }
            }
        }

        private void ChangeSceneWithReload(string scene, OrangeSceneManager.LoadingType loadingType = OrangeSceneManager.LoadingType.DEFAULT)
        {
            // Start (quick) loading screen before assets get unloaded
            UIManager.Instance.OpenLoadingUI(null, loadingType, 0.2f);

            TangerineLoader.PatchAssetbundleIds();
            Plugin.Log.LogWarning($"Unloading all asset bundle cache");
            StartCoroutine(UnloadAllBundleCache((Callback)new Action(() =>
            {
                Plugin.Log.LogWarning($"Reloading asset bundle manifest");
                AssetsBundleManager.Instance.dictBundleID.Clear();
                AssetsBundleManager.Instance.Init();

                AudioManager.Instance.StopAllVoice();
                OrangeSceneManager.Instance.ChangeScene(scene, loadingType, null, true, false);
            })).WrapToIl2Cpp());
        }

        private static IEnumerator UnloadAllBundleCache(Callback p_cb)
        {
            var __instance = AssetsBundleManager.Instance;

            int count = 0;
            int waitCount = 1;
            __instance.bundleKeepMap.Clear();

            
            var keys = new Il2CppSystem.Collections.Generic.List<string>(
                __instance.dictBundleInfo.Keys.Cast<Il2CppSystem.Collections.Generic.IEnumerable<string>>());
            foreach (string key in keys)
            {
                AssetbundleInfo assetbundleInfo = __instance.dictBundleInfo[key];
                if (!__instance.bundleKeepMap.Contains(key))
                {
                    // Unload all loaded objects of the bundle
                    assetbundleInfo.Bundle.Unload(true);
                    assetbundleInfo.Bundle = null;
                    if (count > waitCount)
                    {
                        count = 0;
                        yield return CoroutineDefine._waitForEndOfFrame;
                    }
                    else
                    {
                        int i = count;
                        count = i + 1;
                    }
                    __instance.dictBundleInfo.Remove(key);
                }
            }
            yield return CoroutineDefine._waitForEndOfFrame;

            // This is not used because it also unloads Unity Explorer's fonts
            //AssetBundle.UnloadAllAssetBundles(true);
            Resources.UnloadUnusedAssets();
            MonoBehaviourSingleton<StageMaterialManager>.Instance.Clear();

            p_cb.CheckTargetToInvoke();
            yield break;
        }
    }
}
