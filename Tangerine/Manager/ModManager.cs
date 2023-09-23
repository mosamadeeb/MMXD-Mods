using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tangerine.Manager.Mod;

namespace Tangerine.Manager
{
    /// <summary>
    /// Provides info about the current load order of mods
    /// </summary>
    public static class ModManager
    {
        internal static ModManagerBehaviour Behaviour = null;

        internal static readonly List<TangerineMod> LoadedMods = new();
        internal static List<ModInfo> Mods => LoadedMods.Select(m => m.Info).ToList();

        internal static bool ShouldReplace(string currentMod, string newMod)
        {
            return GetEnabledModsLowerThan(newMod).Contains(currentMod);
        }

        /// <summary>
        /// Gets the IDs of enabled mods that are overridden by the given mod
        /// </summary>
        /// <param name="mod">Mod ID to check against</param>
        /// <returns>List of enabled mods that that are lower than the given mod</returns>
        public static IEnumerable<string> GetEnabledModsLowerThan(string mod)
        {
            var i = Mods.FindIndex(info => info.Id == mod);
            return ((i != -1) ? Mods.GetRange(0, i) : Mods).Where(info => info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// Gets the IDs of enabled mods
        /// </summary>
        /// <returns>List of enabled mods</returns>
        public static IEnumerable<string> GetEnabledMods()
        {
            return Mods.Where(info => info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// Gets the IDs of disabled mods
        /// </summary>
        /// <returns>List of disabled mods</returns>
        public static IEnumerable<string> GetDisabledMods()
        {
            return Mods.Where(info => !info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// Checks if a mod exists in the load order
        /// </summary>
        /// <param name="id">ID of the mod</param>
        /// <returns><see langword="true"/> if a mod with the given id exists; otherwise <see langword="false"/></returns>
        public static bool ModExists(string id)
        {
            return Mods.Any(info => info.Id == id);
        }

        /// <summary>
        /// Checks if a mod exists in the load order and is enabled
        /// </summary>
        /// <param name="id">ID of the mod</param>
        /// <returns><see langword="true"/> if a mod with the given id is enabled; otherwise <see langword="false"/></returns>
        public static bool ModEnabled(string id)
        {
            return Mods.Any(info => info.Id == id && info.IsEnabled);
        }

        internal static bool EnableMod(string id)
        {
            var mod = LoadedMods.Find(m => m.Id == id);

            if (mod != null && !mod.Info.IsEnabled)
            {
                mod.Info.IsEnabled = true;
                ModLoader.LoadMod(mod);
                return true;
            }

            return false;
        }

        internal static bool DisableMod(string id, string reason = null)
        {
            var mod = LoadedMods.Find(m => m.Id == id);

            if (mod != null && mod.Info.IsEnabled)
            {
                mod.Info.IsEnabled = false;
                mod.Info.DisabledReason = reason;
                ModLoader.UnloadMod(mod);
                return true;
            }

            return false;
        }

        internal static bool ReloadMod(string id)
        {
            return ModEnabled(id) ? (DisableMod(id) && EnableMod(id)) : EnableMod(id);
        }

        internal static IEnumerable<string> GetModsToReload()
        {
            return GetEnabledMods().Where(mod => File.Exists(Path.Combine(ModLoader.ModsDir, mod, ManagerConfig.ModReloadFile)));
        }

        internal static void Initialize(Plugin instance)
        {
            ManagerConfig.Initialize();

            ClassInjector.RegisterTypeInIl2Cpp<ModManagerBehaviour>();
            Behaviour = instance.AddComponent<ModManagerBehaviour>();

            foreach (var mod in LoadedMods)
            {
                DisableMod(mod.Id);
            }

            LoadedMods.Clear();

            var mods = ModLoader.PreloadMods();
            while (mods.MoveNext())
            {
                // Add each mod right after it is loaded so the load order can apply
                LoadedMods.Add(mods.Current);
            }
        }
    }
}
