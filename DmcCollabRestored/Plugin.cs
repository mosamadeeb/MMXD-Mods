using BepInEx;
using BepInEx.Logging;
using DmcCollabRestored.Character;
using System;
using Tangerine.Manager.Mod;

namespace DmcCollabRestored;

// Add dependency to Tangerine. This is required for the mod to show up in the mods menu.
[BepInDependency(Tangerine.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
// Do not modify this line. You can change AssemblyName, Product, and Version directly in the .csproj
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : TangerinePlugin
{
    private TangerineMod _tangerine = null;
    internal static new ManualLogSource Log;

    public override void Load(TangerineMod tangerine)
    {
        _tangerine = tangerine;

        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Tangerine plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        RestoreCharacters();
    }

    private void RestoreCharacters()
    {
        _tangerine.Character.AddController(139, typeof(CH140_Controller), new Type[] { typeof(ILogicUpdate) });
    }
}
