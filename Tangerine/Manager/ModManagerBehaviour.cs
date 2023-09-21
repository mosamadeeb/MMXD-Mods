using System;
using UnityEngine;

namespace Tangerine.Manager
{
    internal class ModManagerBehaviour : MonoBehaviour
    {
        public ModManagerBehaviour(IntPtr ptr) : base(ptr) { }

        private void Update()
        {
            if (Input.GetKeyDown(ManagerConfig.ReloadKey.Value))
            {
                foreach (var mod in ModManager.GetModsToReload())
                {
                    Plugin.Log.LogMessage($"Reloading mod: {mod}");
                    ModManager.ReloadMod(mod);
                }
            }
        }

    }
}
