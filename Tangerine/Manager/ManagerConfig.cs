using BepInEx.Configuration;
using UnityEngine;

namespace Tangerine.Manager
{
    internal static class ManagerConfig
    {
        internal enum ReloadMode
        {
            None,
            BackToHome,
            BackToTitle,
            Both,
        }

        internal const string ModReloadFile = "ModReload.txt";

        internal static ConfigEntry<KeyCode> ReloadKey { get; set; }
        internal static ConfigEntry<KeyCode> BackToTitleKey { get; set; }
        internal static ConfigEntry<KeyCode> BackToHometopKey { get; set; }

        internal static ConfigEntry<ReloadMode> BackToSceneReloadMode { get; set; }

        public static void Initialize()
        {
            ReloadKey = Plugin.Config.Bind("General", "Reload Key", KeyCode.F4,
                new ConfigDescription($"Press this key to reload all mods that have the \"{ModReloadFile}\" file in their folder"));

            BackToTitleKey = Plugin.Config.Bind("General", "Back to Title Key", KeyCode.None,
                new ConfigDescription($"Press this key to go back to the title screen"));

            BackToHometopKey = Plugin.Config.Bind("General", "Back to Hometop Key", KeyCode.None,
                new ConfigDescription($"Press this key to go back to the home screen"));

            BackToSceneReloadMode = Plugin.Config.Bind("General", "Reload when going back to Title/Hometop", ReloadMode.BackToTitle,
                new ConfigDescription($"Specify whether to reload all asset bundles when going back to title, home, or both"));
        }
    }
}
