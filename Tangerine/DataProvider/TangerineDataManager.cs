using Fasterflect;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tangerine.Utils;

namespace Tangerine
{
    public class TangerineDataManager
    {
        private static readonly Dictionary<string, List<Action>> _patchDict = new();
        private static readonly Dictionary<string, IntPtr> _pointerDict = new();

        private static readonly string[] _tablePropertyTypes = new string[] { "f_", "n_", "s_", "w_" };

        private static IEnumerable<ReflectionCache.Accessor> GetTablePropertyAccessors(Type type)
        {
            return type.GetProperties()
                    .Select(prop => prop.Name)
                    .Where(name => _tablePropertyTypes.Contains(name.Substring(0, 2)))
                    .Select(name => ReflectionCache.GetPropertyAccessor(type, name));
        }

        internal static void AddPatch(Dictionary<string, List<Action>> patchDict, string tableName, Action patchAction)
        {
            if (!patchDict.TryGetValue(tableName, out var patchList))
            {
                patchList = new List<Action>();
                patchDict[tableName] = patchList;
            }

            patchList.Add(patchAction);
        }

        internal static string GetTableInitializedName(string tableName)
        {
            if (!tableName.EndsWith("_DICT"))
            {
                throw new ArgumentException($"Not a valid table name: {tableName}");
            }

            return "_initialize" + tableName.Remove(tableName.Length - "_DICT".Length);
        }

        internal static string GetTableInternalDictName(string tableName)
        {
            if (!tableName.EndsWith("_DICT"))
            {
                throw new ArgumentException($"Not a valid table name: {tableName}");
            }

            return "_dic" + tableName.Remove(tableName.Length - "_DICT".Length);
        }

        private static bool IsTableInitialized(string tableName)
        {
            return (bool)OrangeDataManager.Instance.GetPropertyValue(GetTableInitializedName(tableName));
        }

        private static IEnumerable<MethodBase> TableGetterTargetMethods()
        {
            return typeof(OrangeDataManager).GetProperties().Where(prop => prop.Name.EndsWith("_TABLE_DICT")).Select(prop => prop.GetGetMethod()).Cast<MethodBase>();
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            foreach (var method in TableGetterTargetMethods())
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(TangerineDataManager).GetMethod(nameof(TableDictGetterPostfix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
        }

        /// <summary>
        /// Patches a table in <see cref="OrangeDataManager"/>. Table entries are matched with the <c>n_ID</c> property
        /// </summary>
        /// <typeparam name="T">Table type. Must be a subclass of <see cref="CapTableBase"/></typeparam>
        /// <param name="entry">Table entry to patch</param>
        public static void PatchTable<T>(T entry) where T: CapTableBase
        {
            PatchTable(new T[] { entry });
        }


        /// <param name="entries">List of table entries to patch</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public static void PatchTable<T>(IEnumerable<T> entries) where T: CapTableBase
        {
            PatchTable(entries, null, false);
        }

        internal static void PatchTable<T>(IEnumerable<T> entries, string tableDictName = null, bool isLocalization = false) where T : CapTableBase
        {
            if (tableDictName == null)
            {
                if (typeof(T) == typeof(CapTableBase))
                {
                    throw new ArgumentException($"{nameof(CapTableBase)} is a base class and cannot be patched");
                }

                tableDictName = typeof(T).Name + "_DICT";
            }

            var patchAction = () => PatchTableOnce(entries, tableDictName, isLocalization);
            AddPatch(_patchDict, tableDictName, patchAction);

            // If not, will patch after initialization
            if (IsTableInitialized(tableDictName))
            {
                patchAction();
            }
        }

        internal static void PatchTableOnce<T>(IEnumerable<T> entries, string tableDictName, bool isLocalization = false) where T : CapTableBase
        {
            if (!entries.Any())
            {
                return;
            }

            var tableType = typeof(T);

            DictionaryWrapper<object, object> tableDict;
            if (isLocalization)
            {
                tableDict = new DictionaryWrapper<object, object>(
                    OrangeTextDataManager.Instance.GetPropertyValue(GetTableInternalDictName(tableDictName)),
                    typeof(string),
                    tableType);
            }
            else
            {
                tableDict = new DictionaryWrapper<object, object>(
                    OrangeDataManager.Instance.GetPropertyValue(GetTableInternalDictName(tableDictName)),
                    typeof(int),
                    tableType);
            }

            var getTableId = ReflectionCache.GetPropertyGetter(tableType, isLocalization ? "w_KEY" : "n_ID");
            var tableAccessors = GetTablePropertyAccessors(tableType);

            foreach (var entry in entries)
            {
                var id = getTableId(entry);

                if (!tableDict.TryGetValue(id, out var tableEntry))
                {
                    // Add the new entry
                    tableDict.Add(id, entry);
                }
                else
                {
                    // Update the table entry using the new entry
                    // We do this instead of simply replacing the entry to allow changes to be seen in references outside of the dictionary
                    foreach (var accessor in tableAccessors)
                    {
                        accessor.Setter(tableEntry, accessor.Getter(entry));
                    }
                }
            }
        }

        /// <param name="entry"><c>Key: Value</c> mapping to patch in table</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public static void PatchTable<T>(Dictionary<string, object> entry) where T : CapTableBase
        {
            PatchTable<T>(new Dictionary<string, object>[] { entry });
        }

        /// <param name="entries">List of <c>Key: Value</c> mappings to patch in table</param>
        /// <inheritdoc cref="PatchTable{T}(T)"/>
        public static void PatchTable<T>(IEnumerable<Dictionary<string, object>> entries) where T : CapTableBase
        {
            PatchTable(typeof(T), entries, null, false);
        }

        /// <param name="tableType">Table type. Must be a subclass of <see cref="CapTableBase"/></param>
        /// <inheritdoc cref="PatchTable{T}(Dictionary{string, object})"/>
        public static void PatchTable(Dictionary<string, object> entry, Type tableType)
        {
            PatchTable(new Dictionary<string, object>[] { entry }, tableType);
        }

        /// <param name="tableType">Table type. Must be a subclass of <see cref="CapTableBase"/></param>
        /// <inheritdoc cref="PatchTable{T}(IEnumerable{Dictionary{string, object}})"/>
        public static void PatchTable(IEnumerable<Dictionary<string, object>> entries, Type tableType)
        {
            PatchTable(tableType, entries, null, false);
        }

        internal static void PatchTable(Type tableType, IEnumerable<Dictionary<string, object>> entries, string tableDictName = null, bool isLocalization = false)
        {
            if (tableDictName == null)
            {
                if (tableType == typeof(CapTableBase))
                {
                    throw new ArgumentException($"{nameof(CapTableBase)} is a base class and cannot be patched");
                }

                tableDictName = tableType.Name + "_DICT";
            }

            var patchAction = () => PatchTableOnce(tableType, entries, tableDictName, isLocalization);
            AddPatch(_patchDict, tableDictName, patchAction);

            // If not, will patch after initialization
            if (IsTableInitialized(tableDictName))
            {
                patchAction();
            }
        }

        internal static void PatchTableOnce(Type tableType, IEnumerable<Dictionary<string, object>> entries, string tableDictName, bool isLocalization = false)
        {
            if (!entries.Any())
            {
                return;
            }

            DictionaryWrapper<object, object> tableDict;
            if (isLocalization)
            {
                tableDict = new DictionaryWrapper<object, object>(
                    OrangeTextDataManager.Instance.GetPropertyValue(GetTableInternalDictName(tableDictName)),
                    typeof(string),
                    tableType);
            }
            else
            {
                tableDict = new DictionaryWrapper<object, object>(
                    OrangeDataManager.Instance.GetPropertyValue(GetTableInternalDictName(tableDictName)),
                    typeof(int),
                    tableType);
            }

            var tableIdKey = isLocalization ? "w_KEY" : "n_ID";
            var tableAccessors = GetTablePropertyAccessors(tableType);

            foreach (var entry in entries)
            {
                var id = entry[tableIdKey];

                if (!tableDict.TryGetValue(id, out var tableEntry))
                {
                    // Add the new entry
                    tableEntry = (CapTableBase)tableType.CreateInstance();
                    tableDict.Add(id, tableEntry);
                }

                // Update the table entry using the new entry
                // We do this instead of simply replacing the entry to allow changes to be seen in references outside of the dictionary
                foreach (var accessor in tableAccessors)
                {
                    if (entry.TryGetValue(accessor.Name, out object value))
                    {
                        accessor.Setter(tableEntry, value);
                    }
                }
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

            var internalDict = ((Il2CppSystem.Object)OrangeDataManager.Instance.GetPropertyValue(GetTableInternalDictName(tableName)));

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
