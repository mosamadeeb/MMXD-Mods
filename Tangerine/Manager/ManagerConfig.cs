using BepInEx.Configuration;
using UnityEngine;

namespace Tangerine.Manager
{
    internal static class ManagerConfig
    {
        internal const string ModReloadFile = "ModReload.txt";

        internal static ConfigEntry<KeyCode> ReloadKey { get; set; }

        public static void Initialize()
        {
            ReloadKey = Plugin.Config.Bind("General", "Reload Key", KeyCode.F4,
                new ConfigDescription($"Press this key to reload all mods that have the \"{ModReloadFile}\" file in their folder"));
        }
    }
}
