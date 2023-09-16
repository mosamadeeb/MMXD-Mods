using BepInEx.Unity.IL2CPP;

namespace Tangerine.Manager.Mod
{
    public abstract class TangerinePlugin : BasePlugin
    {
        public override void Load()
        {

        }

        public abstract void Load(TangerineMod tangerine);
    }
}
