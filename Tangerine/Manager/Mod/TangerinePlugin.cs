using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Tangerine.Manager.Mod
{
    /// <summary>
    /// A subclass of <see cref="BasePlugin"/> that supports being disabled/re-enabled through the mod manager.
    /// </summary>
    public abstract class TangerinePlugin : BasePlugin
    {
        /// <inheritdoc cref="BasePlugin.Load"/>
        public override void Load()
        {

        }

        /// <summary>
        /// Called by the mod loader when the plugin is loaded or re-enabled. Put your initialization code here.
        /// </summary>
        /// <param name="tangerine">Mod instance for your plugin. You should store this for later use</param>
        public abstract void Load(TangerineMod tangerine);

        /// <summary>
        /// Called by the mod loader when the plugin is disabled.
        /// Unpatch your <see cref="Harmony"/> methods here.
        /// <see cref="TangerineMod"/> patches are automatically unpatched.
        /// </summary>
        /// <returns>Should return <see langword="true"/> if the plugin was able to unload itself properly; otherwise <see langword="false"/></returns>
        public override bool Unload()
        {
            return true;
        }
    }
}
