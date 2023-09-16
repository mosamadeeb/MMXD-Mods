using Tangerine.Patchers;
using Tangerine.Patchers.DataProvider;

namespace Tangerine.Manager.Mod
{
    public class TangerineMod
    {
        internal ModInfo Info { get; }

        /// <summary>
        /// 
        /// </summary>
        public string Id { get => Info.Id; }

        /// <summary>
        /// 
        /// </summary>
        public TangerineCharacter Character { get; }

        /// <summary>
        /// 
        /// </summary>
        public TangerineDataManager DataManager { get; }

        /// <summary>
        /// 
        /// </summary>
        public TangerineTextDataManager TextDataManager { get; }

        /// <summary>
        /// 
        /// </summary>
        public TangerineLoader Loader { get; }

        internal TangerineMod(ModInfo modInfo)
        {
            Info = modInfo;
            Character = new TangerineCharacter(Id);
            DataManager = new TangerineDataManager(Id);
            TextDataManager = new TangerineTextDataManager(Id);
            Loader = new TangerineLoader(Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public bool DisableSelf(string reason)
        {
            return ModManager.DisableMod(Id, reason);
        }
    }
}
