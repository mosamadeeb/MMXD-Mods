using CallbackDefs;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Tangerine.Patchers
{
    internal static class TangerineAudioManager
    {
        private static readonly Dictionary<string, bool> _acbIsLoading = new();

        internal static void InitializeHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(TangerineAudioManager));
        }

        [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.GetAcb))]
        [HarmonyPrefix]
        private static void GetAcbPrefix(string s_acb)
        {
            if (s_acb != null && s_acb != string.Empty && !s_acb.EndsWith("_null") && !AudioManager.Instance.orangePool.ContainsKey(s_acb))
            {
                Plugin.Log.LogWarning($"ACB is not loaded: {s_acb}");
                if (!_acbIsLoading.ContainsKey(s_acb))
                {
                    Plugin.Log.LogInfo($"Preloading ACB: {s_acb}");
                    _acbIsLoading[s_acb] = true;
                    AudioManager.Instance.PreloadAtomSource(s_acb, (Callback)new Action(() =>
                    {
                        Plugin.Log.LogInfo($"Preloading finished for ACB: {s_acb}");

                        lock (_acbIsLoading)
                        {
                            _acbIsLoading[s_acb] = false;
                        }
                    }));
                }

                Plugin.Log.LogInfo($"Waiting for ACB {s_acb} to be loaded...");

                // TODO: lock?
                while (_acbIsLoading[s_acb])
                {
                    // Wait
                }

                Plugin.Log.LogMessage($"Finished waiting for ACB {s_acb}");
                _acbIsLoading.Remove(s_acb);
            }
        }
    }
}
