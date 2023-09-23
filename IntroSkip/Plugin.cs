using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Tangerine.Manager.Mod;

namespace IntroSkip;

// Add dependency to Tangerine. This is required for the mod to show up in the mods menu.
[BepInDependency(Tangerine.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
// Do not modify this line. You can change AssemblyName, Product, and Version directly in the .csproj
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : TangerinePlugin
{
    private TangerineMod _tangerine = null;
    private static Harmony _harmony;
    internal static new ManualLogSource Log;

    public override void Load(TangerineMod tangerine)
    {
        _tangerine = tangerine;

        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Tangerine plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(Plugin));
    }

    public override bool Unload()
    {
        _harmony.UnpatchSelf();
        return true;
    }
    [HarmonyPatch(typeof(OrangeSceneManager), nameof(OrangeSceneManager.ChangeScene))]
    [HarmonyPrefix]
    static void ChangeScenePrefix(ref string p_scene)
    {
        if (p_scene == "splash")
        {
            p_scene = "title";
            Plugin.Log.LogMessage("Skipping splash screen");
        }
        else if (p_scene == "OpeningStage")
        {
            p_scene = "title";
            Plugin.Log.LogMessage("Skipping intro");
        }
    }
}
