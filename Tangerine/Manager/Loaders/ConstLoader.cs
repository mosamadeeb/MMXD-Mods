using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Patchers.DataProvider;

namespace Tangerine.Manager.Loaders
{
    internal static class ConstLoader
    {
        private const string ConstFile = "PARAMETERS.json";

        public static bool Load(string modPath, TangerineConst konst)
        {
            try
            {
                var constFilePath = Path.Combine(modPath, TableLoader.TablesFolder, ConstFile);

                if (File.Exists(constFilePath))
                {
                    var constDict = DeserializeTable(JsonNode.Parse(File.ReadAllText(constFilePath)));

                    foreach (var pair in constDict)
                    {
                        konst.PatchConst(pair.Key, pair.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to read ConstFile: {e}");
                return false;
            }

            return true;
        }

        public static void Unload(string modId)
        {
            TangerineConst.PatchDict.OnModDisabled(modId);
        }

        public static bool HasContentToLoad(string modPath)
        {
            return File.Exists(Path.Combine(modPath, TableLoader.TablesFolder, ConstFile));
        }

        private static Dictionary<string, int> DeserializeTable(JsonNode node)
        {
            var dict = node.AsObject().First().Value.Deserialize<Dictionary<string, object>>();
            var newDict = new Dictionary<string, int>();

            foreach (var key in dict.Keys)
            {
                var strVal = ((JsonElement)dict[key]).Deserialize<string>();

                if (strVal != null)
                {
                    newDict[key] = int.Parse(strVal);
                }
                else
                {
                    newDict[key] = ((JsonElement)dict[key]).Deserialize<int>();
                }
            }

            return newDict;
        }
    }
}
