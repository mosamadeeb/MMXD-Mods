using Fasterflect;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tangerine.Patchers.DataProvider
{
    public class TangerineDataManager
    {
        internal static readonly DataProviderBase<int, Il2CppSystem.Object> Provider = new(OrangeDataManager.Instance);
        private static readonly Dictionary<string, IntPtr> _pointerDict = new();

        private readonly string _modGuid;

        internal TangerineDataManager(string modGuid)
        {
            _modGuid = modGuid;
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            foreach (var method in Provider.TableGetterTargetMethods())
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(TangerineDataManager).GetMethod(nameof(TableDictGetterPostfix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
        }

        /// <summary>
        /// Patches a table in <see cref="OrangeDataManager"/>. Table entries are matched with the <c>n_ID</c> property
        /// </summary>
        /// <typeparam name="T">Table type. Must be a subclass of <see cref="CapTableBase"/></typeparam>
        /// <param name="entry">Table entry to patch</param>
        public void PatchTable<T>(T entry) where T : CapTableBase
        {
            PatchTable(new T[] { entry });
        }

        /// <param name="entries">List of table entries to patch</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public void PatchTable<T>(IEnumerable<T> entries) where T : CapTableBase
        {
            var patchDict = Provider.GetPatchDict(typeof(T).Name + "_DICT");
            patchDict.SetRange(_modGuid, entries.Select(v => KeyValuePair.Create((int)v.GetPropertyValue("n_ID"), (Il2CppSystem.Object)v)));
        }

        /// <param name="entry"><c>Key: Value</c> mapping to patch in table</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public void PatchTable<T>(Dictionary<string, object> entry) where T : CapTableBase
        {
            PatchTable<T>(new Dictionary<string, object>[] { entry });
        }

        /// <param name="entries">List of <c>Key: Value</c> mappings to patch in table</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public void PatchTable<T>(IEnumerable<Dictionary<string, object>> entries) where T : CapTableBase
        {
            PatchTable(entries, typeof(T));
        }

        /// <param name="tableType">Table type. Must be a subclass of <see cref="CapTableBase"/></param>
        /// <inheritdoc cref="PatchTable{T}(Dictionary{string, object})"/>
        public void PatchTable(Dictionary<string, object> entry, Type tableType)
        {
            PatchTable(new Dictionary<string, object>[] { entry }, tableType);
        }

        /// <param name="tableType">Table type. Must be a subclass of <see cref="CapTableBase"/></param>
        /// <inheritdoc cref="PatchTable{T}(IEnumerable{Dictionary{string, object}})"/>
        public void PatchTable(IEnumerable<Dictionary<string, object>> entries, Type tableType)
        {
            var patchDict = Provider.GetPatchDict(tableType.Name + "_DICT");
            var objects = DataProviderBase<int, Il2CppSystem.Object>.Deserialize(entries, tableType);

            patchDict.SetRange(_modGuid, objects.Select(v => KeyValuePair.Create((int)v.GetPropertyValue("n_ID"), v)));
        }

        private static void TableDictGetterPostfix(MethodBase __originalMethod)
        {
            var tableName = __originalMethod.Name.Substring("_get".Length);

            // Get last pointer for this table
            if (!_pointerDict.TryGetValue(tableName, out var pointer))
            {
                pointer = IntPtr.Zero;
            }

            var internalDict = (Il2CppSystem.Object)OrangeDataManager.Instance.GetPropertyValue(
                DataProviderBase<int, Il2CppSystem.Object>.GetTableInternalDictName(tableName));

            if (internalDict.Pointer != pointer)
            {
                // Invalidated, start patching

                if (Provider.PatchDictExists(tableName))
                {
                    Plugin.Log.LogWarning($"{tableName} instance changed! Reapplying patches...");
                    Provider.ResetPatch(tableName, Array.Empty<int>());
                }
            }

            // Update last pointer
            _pointerDict[tableName] = internalDict.Pointer;
        }
    }
}
