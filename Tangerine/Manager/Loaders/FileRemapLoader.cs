using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tangerine.Patchers;

namespace Tangerine.Manager.Loaders
{
    internal static class FileRemapLoader
    {
        private struct FileRemap
        {
            public string file;
            public string newFile;
        }

        private const string JsonFile = "FileRemap.json";
        private const string FilesFolder = "Files";

        public static bool Load(string modPath, TangerineLoader loader)
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(Path.Combine(modPath, JsonFile)));
                var list = node["ListFile"]?.AsArray();

                if (list == null)
                {
                    Plugin.Log.LogError($"Failed to read {JsonFile} for mod \"{modPath}\"");
                    return false;
                }

                var modFilesFolder = Path.Combine(modPath, FilesFolder);

                foreach (var remap in list.Select(DeserializeFileRemap))
                {
                    var newFilePath = Path.Combine(modFilesFolder, remap.newFile.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(newFilePath))
                    {
                        loader.AddFile(remap.file, newFilePath);
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Failed to remap file \"{remap.file}\" for mod \"{modPath}\": Remapped file does not exist on disk");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to read {JsonFile} for mod \"{modPath}\": {e}");
                return false;
            }

            return true;
        }

        public static void Unload(string modId)
        {
            TangerineLoader.FilePaths.OnModDisabled(modId);
        }

        public static bool HasContentToLoad(string modPath)
        {
            return File.Exists(Path.Combine(modPath, JsonFile));
        }

        private static FileRemap DeserializeFileRemap(JsonNode node)
        {
            return new FileRemap()
            {
                file = node["file"].Deserialize<string>(),
                newFile = node["newFile"].Deserialize<string>(),
            };
        }
    }
}
