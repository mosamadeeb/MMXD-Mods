using Fasterflect;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine
{
    public class TangerineTextDataManager
    {
        private static readonly Dictionary<string, List<Action>> _patchDict = new();
        private static readonly Dictionary<string, IntPtr> _pointerDict = new();

        private static bool IsTableInitialized(string tableName)
        {
            return (bool)OrangeTextDataManager.Instance.GetPropertyValue(TangerineDataManager.GetTableInitializedName(tableName));
        }

        /// <summary>
        /// Patches a table in <see cref="OrangeTextDataManager"/>. Table entries are matched with the <c>w_KEY</c> property
        /// </summary>
        /// <param name="entry">Table entry to patch</param>
        /// <param name="localizationTableDictName">Name of the table dictionary property in <see cref="OrangeTextDataManager"/></param>
        public static void PatchTable(LOCALIZATION_TABLE entry, string localizationTableDictName)
        {
            PatchTable(new LOCALIZATION_TABLE[] { entry }, localizationTableDictName);
        }

        /// <param name="entries">List of table entries to patch</param>
        /// <inheritdoc cref="PatchTable(LOCALIZATION_TABLE, string)"/>
        public static void PatchTable(IEnumerable<LOCALIZATION_TABLE> entries, string localizationTableDictName)
        {
            var patchAction = () => TangerineDataManager.PatchTableOnce(entries, localizationTableDictName, true);
            TangerineDataManager.AddPatch(_patchDict, localizationTableDictName, patchAction);

            // If not, will patch after initialization
            if (IsTableInitialized(localizationTableDictName))
            {
                patchAction();
            }
        }

        /// <param name="entry"><c>Key: Value</c> mapping to patch in table</param>
        /// <inheritdoc cref="PatchTable(LOCALIZATION_TABLE, string)"/>
        public static void PatchTable(Dictionary<string, object> entry, string localizationTableDictName)
        {
            PatchTable(new Dictionary<string, object>[] { entry }, localizationTableDictName);
        }

        /// <param name="entries">List of <c>Key: Value</c> mappings to patch in table</param>
        /// <inheritdoc cref="PatchTable(LOCALIZATION_TABLE, string)"/>
        public static void PatchTable(IEnumerable<Dictionary<string, object>> entries, string localizationTableDictName)
        {
            var patchAction = () => TangerineDataManager.PatchTableOnce(typeof(LOCALIZATION_TABLE), entries, localizationTableDictName, true);
            TangerineDataManager.AddPatch(_patchDict, localizationTableDictName, patchAction);

            // If not, will patch after initialization
            if (IsTableInitialized(localizationTableDictName))
            {
                patchAction();
            }
        }

        private static IEnumerable<MethodBase> TableGetterTargetMethods()
        {
            return typeof(OrangeTextDataManager).GetProperties().Where(prop => prop.Name.EndsWith("_TABLE_DICT")).Select(prop => prop.GetGetMethod()).Cast<MethodBase>();
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            foreach (var method in TableGetterTargetMethods())
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(TangerineTextDataManager).GetMethod(nameof(TableDictGetterPostfix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
        }

        private static void TableDictGetterPostfix(MethodBase __originalMethod)
        {
            var tableName = __originalMethod.Name.Substring("_get".Length);

            // Get last pointer for this table
            if (!_pointerDict.TryGetValue(tableName, out var pointer))
            {
                pointer = IntPtr.Zero;
            }

            var internalDict = ((Il2CppSystem.Object)OrangeTextDataManager.Instance.GetPropertyValue(TangerineDataManager.GetTableInternalDictName(tableName)));

            if (internalDict.Pointer != pointer)
            {
                // Invalidated, start patching
                if (_patchDict.TryGetValue(tableName, out var patches))
                {
                    Plugin.Log.LogWarning($"{tableName} instance changed! Reapplying patches...");
                    foreach (var patch in patches)
                    {
                        patch();
                    }
                }
            }

            // Update last pointer
            _pointerDict[tableName] = internalDict.Pointer;
        }
    }
}
