using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

namespace FixSavingCrash;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        Plugin.Log = base.Log;

        // Plugin startup logic
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Harmony.CreateAndPatchAll(typeof(Plugin));
    }

    [HarmonyPatch(typeof(StandaloneAntiCheat), nameof(StandaloneAntiCheat.IsCheatEngineRunning))]
    [HarmonyPostfix]
    static void IsCheatEngineRunningPostfix(ref bool __result)
    {
        if (__result)
        {
            Log.LogWarning($"Cheat Engine is running! Preventing StandaloneAntiCheat from crashing the game.");
            __result = false;
        }
    }
}
