using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Patchers.DataProvider;

namespace Tangerine.Manager.Loaders
{
    internal static class TableLoader
    {
        private const string TablesFolder = "Tables";
        private const string TextTablesFolder = "TextTables";

        public static bool Load(string modPath, TangerineDataManager dataManager, TangerineTextDataManager textDataManager)
        {
            string lastTable = "";
            try
            {
                var dataProviderAssembly = Assembly.GetAssembly(typeof(CHARACTER_TABLE));

                var tablesDir = Path.Combine(modPath, TablesFolder);
                var textTablesDir = Path.Combine(modPath, TextTablesFolder);

                if (Directory.Exists(tablesDir))
                {
                    foreach (var tableFile in Directory.EnumerateFiles(tablesDir))
                    {
                        lastTable = tableFile;
                        var typeName = Path.GetFileNameWithoutExtension(tableFile);
                        var tableType = Type.GetType(Assembly.CreateQualifiedName(dataProviderAssembly.GetName().Name, typeName));

                        if (tableType == null)
                        {
                            Plugin.Log.LogError($"Unknown table name {Path.GetFileNameWithoutExtension(tableFile)} for mod \"{modPath}\"");
                            continue;
                        }

                        var tableList = DeserializeTable(JsonNode.Parse(File.ReadAllText(tableFile)));

                        dataManager.PatchTable(tableList, tableType);
                    }
                }

                if (Directory.Exists(textTablesDir))
                {
                    foreach (var tableFile in Directory.EnumerateFiles(textTablesDir))
                    {
                        lastTable = tableFile;
                        var tableName = Path.GetFileNameWithoutExtension(tableFile) + "_DICT";
                        var tableList = DeserializeTable(JsonNode.Parse(File.ReadAllText(tableFile)));

                        textDataManager.PatchTable(tableList, tableName);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to read table {lastTable}: {e}");
                return false;
            }

            return true;
        }

        public static void Unload(string modId)
        {
            foreach (var dict in TangerineDataManager.Provider.PatchDict.Values)
            {
                dict.OnModDisabled(modId);
            }

            foreach (var dict in TangerineTextDataManager.Provider.PatchDict.Values)
            {
                dict.OnModDisabled(modId);
            }
        }

        public static bool HasContentToLoad(string modPath)
        {
            return Directory.Exists(Path.Combine(modPath, TablesFolder)) || Directory.Exists(Path.Combine(modPath, TextTablesFolder));
        }

        private static List<Dictionary<string, object>> DeserializeTable(JsonNode node)
        {
            var list = node.AsObject().First().Value.Deserialize<List<Dictionary<string, object>>>();

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
    }
}
