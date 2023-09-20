using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Tangerine.Manager;
using Tangerine.Patchers;
using Tangerine.Patchers.DataProvider;

namespace Tangerine;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
[BepInPlugin(GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static string Location;
    internal static new ManualLogSource Log;
    private static Harmony _harmony;

    public const string GUID = "0Tangerine";

    public override void Load()
    {
        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Tangerine is loaded!");

        // Get folder
        Location = IL2CPPChainloader.Instance.Plugins[GUID].Location;

        _harmony = new Harmony(GUID);
        TangerineDataManager.InitializeHarmony(_harmony);
        TangerineTextDataManager.InitializeHarmony(_harmony);
        TangerineCharacter.InitializeHarmony(_harmony);
        TangerineLoader.InitializeHarmony(_harmony);

        // Start loading mods
        ModManager.Initialize(this);
    }

    public override bool Unload()
    {
        _harmony.UnpatchSelf();
        return true;
    }
}
