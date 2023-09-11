using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Tangerine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private static Harmony _harmony;

    public override void Load()
    {
        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        TangerineDataManager.InitializeHarmony(_harmony);
        TangerineTextDataManager.InitializeHarmony(_harmony);
        Game.TangerineCharacter.InitializeHarmony(_harmony);
        TangerineLoader.InitializeHarmony(_harmony);
    }

    /// <summary>
    /// Provide a log to this plugin so it can log before being loaded
    /// </summary>
    /// <param name="log">Any log source</param>
    public static void ProvideLog(ManualLogSource log)
    {
        Plugin.Log = log;
    }
}
