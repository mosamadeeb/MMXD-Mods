using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System;
using System.IO;

namespace MMXDNetworkLogger;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    internal static Harmony PluginHarmony;

    private void Awake()
    {
        Plugin.Logger = base.Logger;

        var logsPath = Path.Combine(Path.GetDirectoryName(Info.Location), "Logs", DateTime.Now.ToString("yyyy-MM-dd_HH'h'-mm'm'-ss's'"));
        Directory.CreateDirectory(logsPath);

        Loggers.RequestLogger.Initialize(Logger, logsPath);
        Loggers.ProtocolLogger.Initialize(Logger, logsPath);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        PluginHarmony = new Harmony(typeof(Plugin).Namespace);

        Loggers.RequestLogger.PatchHarmony(PluginHarmony);
        Loggers.ProtocolLogger.PatchHarmony(PluginHarmony);
    }
}
