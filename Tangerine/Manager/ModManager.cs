using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Manager.Mod;

namespace Tangerine.Manager
{
    public static class ModManager
    {
        internal static readonly List<TangerineMod> LoadedMods = new();
        internal static List<ModInfo> Mods => LoadedMods.Select(m => m.Info).ToList();

        internal static bool ShouldReplace(string currentMod, string newMod)
        {
            return GetEnabledModsLowerThan(newMod).Contains(currentMod);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentMod"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetEnabledModsLowerThan(string currentMod)
        {
            var i = Mods.FindIndex(mod => mod.Id == currentMod);
            return ((i != -1) ? Mods.GetRange(0, i) : Mods).Where(info => info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetEnabledMods()
        {
            return Mods.Where(info => info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetDisabledMods()
        {
            return Mods.Where(info => !info.IsEnabled).Select(info => info.Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static bool ModExists(string guid)
        {
            return Mods.Any(info => info.Id == guid);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static bool ModEnabled(string guid)
        {
            return Mods.Any(info => info.Id == guid && info.IsEnabled);
        }

        internal static bool EnableMod(string guid)
        {
            var mod = LoadedMods.Find(m => m.Id == guid);

            if (mod != null)
            {
                mod.Info.IsEnabled = true;
                ModLoader.LoadMod(mod);
                return true;
            }

            return false;
        }

        internal static bool DisableMod(string guid, string reason = null)
        {
            var mod = Mods.Find(info => info.Id == guid);

            if (mod != null)
            {
                mod.IsEnabled = false;
                mod.DisabledReason = reason;
                ModLoader.UnloadMod(mod);
                return true;
            }

            return false;
        }

        internal static void Initialize()
        {
            foreach (var mod in LoadedMods)
            {
                DisableMod(mod.Id);
            }

            LoadedMods.Clear();
            LoadedMods.AddRange(ModLoader.PreloadMods());
        }
    }
}
