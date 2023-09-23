using BepInEx.Unity.IL2CPP;
using Tangerine.Patchers;
using Tangerine.Patchers.DataProvider;

namespace Tangerine.Manager.Mod
{
    /// <summary>
    /// Provides access to Tangerine's patchers.
    /// All of the patches applied here are per-mod, and can be overridden depending on the mod's load order.
    /// </summary>
    public class TangerineMod
    {
        internal ModInfo Info { get; }

        /// <summary>
        /// Unique ID of the mod, which is equal to the name of the folder the mod is located in.
        /// </summary>
        public string Id { get => Info.Id; }

        /// <inheritdoc cref="TangerineCharacter"/>
        public TangerineCharacter Character { get; }

        /// <inheritdoc cref="TangerineDataManager"/>
        public TangerineDataManager DataManager { get; }

        /// <inheritdoc cref="TangerineTextDataManager"/>
        public TangerineTextDataManager TextDataManager { get; }

        /// <inheritdoc cref="TangerineLoader"/>
        public TangerineLoader Loader { get; }

        /// <inheritdoc cref="TangerineConst"/>
        public TangerineConst Const { get; }

        internal TangerineMod(ModInfo modInfo)
        {
            Info = modInfo;
            Character = new TangerineCharacter(Id);
            DataManager = new TangerineDataManager(Id);
            TextDataManager = new TangerineTextDataManager(Id);
            Loader = new TangerineLoader(Id);
            Const = new TangerineConst(Id);
        }

        /// <summary>
        /// Disables the mod, which can only be re-enabled by the user.
        /// Note: this will unpatch all of the mod's files and call your plugin's <see cref="BasePlugin.Unload"/> method.
        /// </summary>
        /// <param name="reason">Custom message to display as the reason the mod was disabled. Currently unused.</param>
        /// <returns></returns>
        public bool DisableSelf(string reason)
        {
            return ModManager.DisableMod(Id, reason);
        }
    }
}
