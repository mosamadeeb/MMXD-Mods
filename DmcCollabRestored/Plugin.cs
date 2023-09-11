using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DmcCollabRestored.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine;
using Tangerine.Game;

namespace DmcCollabRestored;

// Do not modify this line. You can change AssemblyName, Product, and Version directly in the .csproj
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Provide temporary log to Tangerine
        Tangerine.Plugin.ProvideLog(Log);

        RestoreCharacters();
    }

    private static void RestoreCharacters()
    {
        TangerineCharacter.AddController(139, typeof(CH140_Controller), new Type[] { typeof(ILogicUpdate) });
        TestPatches();
    }

    private static List<Dictionary<string, object>> ReadTable(string path)
    {
        var j = JsonObject.Parse(File.ReadAllText(path)).AsObject().First().Value;
        var list = j.Deserialize<List<Dictionary<string, object>>>();

        foreach (var dict in list)
        {
            foreach (var key in dict.Keys)
            {
                switch (key[0])
                {
                    case 'n':
                    case '#':
                        try
                        {
                            dict[key] = ((JsonElement)dict[key]).Deserialize<int>();
                        }
                        catch
                        {
                            dict[key] = unchecked((int)((JsonElement)dict[key]).Deserialize<uint>());
                        }
                        break;
                    case 'f':
                        dict[key] = ((JsonElement)dict[key]).Deserialize<float>();
                        break;
                    case 's':
                    case 'w':
                    default:
                        dict[key] = (dict[key] == null) ? "null" : ((JsonElement)dict[key]).Deserialize<string>();

                        break;
                }
            }
        }

        return list;
    }

    private static void TestPatches()
    {
        var abPath = Path.Combine(Path.GetDirectoryName(IL2CPPChainloader.Instance.Plugins[MyPluginInfo.PLUGIN_NAME].Location), "assetbundles");
        

        // Load missing asset bundles from online
        var j = JsonObject.Parse(File.ReadAllText(Path.Combine(abPath, "assetbundleids.json")));
        var list = j.Deserialize<List<Dictionary<string, object>>>();
        foreach (var item in list)
        {
            var ab = new AssetbundleId(
                ((JsonElement)item["name"]).Deserialize<string>(),
                ((JsonElement)item["hash"]).Deserialize<string>(),
                ((JsonElement)item["crc"]).Deserialize<uint>(),
                ((JsonElement)item["size"]).Deserialize<long>()
            );

            TangerineLoader.AddAssetBundleId(ab, Path.Combine("C:\\Users\\Admin\\AppData\\LocalLow\\CAPCOM\\ROCKMAN X DiVE", ab.hash));
        }

        // Load audio acbs
        foreach (var file in Directory.GetFiles(Path.Combine(abPath, "audio")))
        {
            TangerineLoader.AddFile(Path.GetFileName(file), file);
        }

        var dataProviderAssembly = Assembly.GetAssembly(typeof(CHARACTER_TABLE));
        var pluginPath = Path.GetDirectoryName(IL2CPPChainloader.Instance.Plugins[MyPluginInfo.PLUGIN_NAME].Location);

        // Data tables
        foreach (var file in Directory.GetFiles(Path.Combine(pluginPath, "tables", "OrangeData_patch")))
        {
            var typeName = Path.GetFileNameWithoutExtension(file);
            TangerineDataManager.PatchTable(ReadTable(file), Type.GetType(Assembly.CreateQualifiedName(dataProviderAssembly.GetName().Name, typeName)));
        }

        // Text tables
        foreach (var file in Directory.GetFiles(Path.Combine(pluginPath, "tables", "OrangeTextData_patch")))
        {
            TangerineTextDataManager.PatchTable(ReadTable(file), Path.GetFileNameWithoutExtension(file) + "_DICT");
        }
    }
}
