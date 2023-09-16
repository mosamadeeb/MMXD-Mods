using Fasterflect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tangerine.Manager;
using Tangerine.Utils;

namespace Tangerine.Patchers.DataProvider
{
    internal class DataProviderBase<TKey, TTable> where TTable : new()
    {
        private static readonly char[] _tablePropertyTypes = new char[] { 'f', 'n', 's', 'w', '#' };

        private readonly Dictionary<string, Dictionary<TKey, TTable>> _originalDict = new();
        internal readonly Dictionary<string, ModDictionary<TKey, TTable>> PatchDict = new();

        private readonly Il2CppSystem.Object _managerInstance;

        private Type ManagerType => _managerInstance.GetType();

        public DataProviderBase(Il2CppSystem.Object managerInstance)
        {
            _managerInstance = managerInstance;
        }

        private static IEnumerable<ReflectionCache.Accessor> GetTablePropertyAccessors(Type type)
        {
            return type.GetProperties()
                    .Select(prop => prop.Name)
                    .Where(name => _tablePropertyTypes.Contains(name[0]))
                    .Select(name => ReflectionCache.GetPropertyAccessor(type, name));
        }

        private static void ValidateTableName(string tableName)
        {
            if (!tableName.EndsWith("_DICT"))
            {
                throw new ArgumentException($"Not a valid table name: {tableName}");
            }
        }

        public static string GetTableInitializedName(string tableName)
        {
            ValidateTableName(tableName);

            return "_initialize" + tableName.Remove(tableName.Length - "_DICT".Length);
        }

        public static string GetTableInternalDictName(string tableName)
        {
            ValidateTableName(tableName);

            return "_dic" + tableName.Remove(tableName.Length - "_DICT".Length);
        }

        public ModDictionary<TKey, TTable> GetPatchDict(string tableName)
        {
            if (!PatchDict.TryGetValue(tableName, out var modDict))
            {
                modDict = new ModDictionary<TKey, TTable>();
                modDict.BaseChangedEvent += (key, changeType) => ApplyPatch(tableName, key, changeType);
                modDict.BaseMultiChangedEvent += (pairs) => ApplyMultiPatch(tableName, pairs);
                modDict.BaseResetEvent += (keys) => ResetPatch(tableName, keys);

                PatchDict[tableName] = modDict;
                _originalDict[tableName] = new Dictionary<TKey, TTable>();
            }

            return modDict;
        }

        public bool PatchDictExists(string tableName)
        {
            return PatchDict.ContainsKey(tableName);
        }

        public bool IsTableInitialized(string tableName)
        {
            return (bool)_managerInstance.GetPropertyValue(GetTableInitializedName(tableName));
        }

        public IEnumerable<MethodBase> TableGetterTargetMethods()
        {
            return ManagerType.GetProperties().Where(prop => prop.Name.EndsWith("_TABLE_DICT")).Select(prop => prop.GetGetMethod()).Cast<MethodBase>();
        }

        public void ApplyPatch(string tableName, TKey key, BaseChangeType changeType)
        {
            ApplyMultiPatch(tableName, new (TKey, BaseChangeType)[] { (key, changeType) });
        }

        public void ApplyMultiPatch(string tableName, IEnumerable<(TKey, BaseChangeType)> pairs)
        {
            if (!IsTableInitialized(tableName))
            {
                return;
            }

            var tablePatchDict = GetPatchDict(tableName);
            var tableOriginalDict = _originalDict[tableName];

            var tableDict = new DictionaryWrapper<TKey, TTable>(
                _managerInstance.GetPropertyValue(GetTableInternalDictName(tableName)),
                typeof(TKey),
                GetTableType(tableName)
            );


            TTable value;
            foreach (var pair in pairs)
            {
                var key = pair.Item1;
                var changeType = pair.Item2;

                switch (changeType)
                {
                    case BaseChangeType.Add:
                        if (tableDict.TryGetValue(key, out value))
                        {
                            // Store original for restoring later
                            tableOriginalDict[key] = value;
                        }
                        goto case BaseChangeType.Update;
                    case BaseChangeType.Update:
                        // Update the game's dictionary
                        // TODO: If it's ever needed, we can use getters and setters to update the objects here instead of setting them
                        tableDict[key] = tablePatchDict.Base[key];
                        break;
                    case BaseChangeType.Remove:
                        if (tableOriginalDict.TryGetValue(key, out value))
                        {
                            // Patch original value back
                            tableDict[key] = value;
                        }
                        else
                        {
                            tableDict.Remove(key);
                        }
                        break;
                }
            }
        }

        public void ResetPatch(string tableName, IEnumerable<TKey> keys)
        {
            if (!IsTableInitialized(tableName))
            {
                return;
            }

            // Remove the old keys, then add the new keys
            ApplyMultiPatch(tableName, keys.Select(k => (k, BaseChangeType.Remove)));
            ApplyMultiPatch(tableName, GetPatchDict(tableName).Base.Keys.Select(k => (k, BaseChangeType.Add)));
        }

        public static Type GetTableType(string tableName)
        {
            if (tableName.EndsWith("_DICT"))
            {
                tableName = tableName.Remove(tableName.Length - "_DICT".Length);
            }

            if (typeof(OrangeTextDataManager).GetProperty(tableName + "_DICT") != null)
            {
                return typeof(LOCALIZATION_TABLE);
            }

            var assembly = Assembly.GetAssembly(typeof(CHARACTER_TABLE));
            var type = Type.GetType(Assembly.CreateQualifiedName(assembly.GetName().Name, tableName));
            return type;
        }

        public static IEnumerable<TTable> Deserialize(IEnumerable<Dictionary<string, object>> entries, Type tableType = null)
        {
            tableType ??= typeof(TTable);
            var tableAccessors = GetTablePropertyAccessors(tableType);

            var newEntries = new List<TTable>(entries.Count());
            foreach (var entry in entries)
            {
                // Create a new table entry
                var newEntry = (TTable)tableType.CreateInstance();
                foreach (var accessor in tableAccessors)
                {
                    if (entry.TryGetValue(accessor.Name, out var value))
                    {
                        accessor.Setter(newEntry, value);
                    }
                }

                newEntries.Add(newEntry);
            }

            return newEntries;
        }
    }
}
