using Fasterflect;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.Patchers.DataProvider
{
    public class TangerineTextDataManager
    {
        internal static readonly DataProviderBase<string, LOCALIZATION_TABLE> Provider = new(OrangeTextDataManager.Instance);
        private static readonly Dictionary<string, IntPtr> _pointerDict = new();

        private readonly string _modGuid;

        internal TangerineTextDataManager(string modGuid)
        {
            _modGuid = modGuid;
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            foreach (var method in Provider.TableGetterTargetMethods())
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(TangerineTextDataManager).GetMethod(nameof(TableDictGetterPostfix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
        }

        /// <summary>
        /// Patches a table in <see cref="OrangeTextDataManager"/>. Table entries are matched with the <c>w_KEY</c> property
        /// </summary>
        /// <param name="entry"><c>Key: Value</c> mapping to patch in table</param>
        /// <param name="localizationTableDictName">Name of the table dictionary property in <see cref="OrangeTextDataManager"/></param>
        public void PatchTable(Dictionary<string, object> entry, string localizationTableDictName)
        {
            PatchTable(new Dictionary<string, object>[] { entry }, localizationTableDictName);
        }

        /// <param name="entries">List of <c>Key: Value</c> mappings to patch in table</param>
        /// <inheritdoc cref="PatchTable(Dictionary{string, object}, string)"/>
        public void PatchTable(IEnumerable<Dictionary<string, object>> entries, string localizationTableDictName)
        {
            PatchTable(DataProviderBase<string, LOCALIZATION_TABLE>.Deserialize(entries), localizationTableDictName);
        }

        /// <param name="entry">Table entry to patch</param>
        /// <inheritdoc cref="PatchTable(Dictionary{string, object}, string)"/>
        public void PatchTable(LOCALIZATION_TABLE entry, string localizationTableDictName)
        {
            PatchTable(new LOCALIZATION_TABLE[] { entry }, localizationTableDictName);
        }

        /// <param name="entries">List of table entries to patch</param>
        /// <inheritdoc cref="PatchTable(Dictionary{string, object}, string)"/>
        public void PatchTable(IEnumerable<LOCALIZATION_TABLE> entries, string localizationTableDictName)
        {
            var patchDict = Provider.GetPatchDict(localizationTableDictName);
            patchDict.SetRange(_modGuid, entries.Select(v => KeyValuePair.Create(v.w_KEY, v)));
        }

        private static void TableDictGetterPostfix(MethodBase __originalMethod)
        {
            var tableName = __originalMethod.Name.Substring("_get".Length);

            // Get last pointer for this table
            if (!_pointerDict.TryGetValue(tableName, out var pointer))
            {
                pointer = IntPtr.Zero;
            }

            var internalDict = (Il2CppSystem.Object)OrangeTextDataManager.Instance.GetPropertyValue(
                DataProviderBase<string, LOCALIZATION_TABLE>.GetTableInternalDictName(tableName));

            if (internalDict.Pointer != pointer)
            {
                // Invalidated, start patching
                if (Provider.PatchDictExists(tableName))
                {
                    Plugin.Log.LogWarning($"{tableName} instance changed! Reapplying patches...");
                    Provider.ResetPatch(tableName, Array.Empty<string>());
                }
            }

            // Update last pointer
            _pointerDict[tableName] = internalDict.Pointer;
        }
    }
}
