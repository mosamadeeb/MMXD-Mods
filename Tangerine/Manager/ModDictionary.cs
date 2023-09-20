using System;
using System.Collections.Generic;
using System.Linq;

namespace Tangerine.Manager
{
    /// <summary>
    /// Type of change in the base dictionary
    /// </summary>
    internal enum BaseChangeType
    {
        /// <summary>
        /// An element was added
        /// </summary>
        Add,

        /// <summary>
        /// An element was removed
        /// </summary>
        Remove,

        /// <summary>
        /// An element was updated
        /// </summary>
        Update,
    }

    internal class ModDictionary<TKey, TValue>
    {
        /// <summary>
        /// Maps each key to the GUID of the mod that currently applies a patch for that key.
        /// </summary>
        private readonly Dictionary<TKey, string> _appliedMods;

        /// <summary>
        /// Maps each mod's GUID to the mod's patch dictionary
        /// </summary>
        private readonly Dictionary<string, Dictionary<TKey, TValue>> _modsDict;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid">GUID of the mod</param>
        /// <returns>The dictionary instance of the mod</returns>
        private Dictionary<TKey, TValue> this[string guid]
        {
            get
            {
                TryGetMod(guid, out var mod);
                return mod;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="mod"></param>
        /// <returns>Mod dictionary, or a new dictionary if it did not exist</returns>
        private bool TryGetMod(string guid, out Dictionary<TKey, TValue> mod)
        {
            if (!_modsDict.TryGetValue(guid, out mod))
            {
                mod = new Dictionary<TKey, TValue>();
                _modsDict[guid] = mod;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Dictionary containing the applied patches
        /// </summary>
        public Dictionary<TKey, TValue> Base;

        public ModDictionary()
        {
            _appliedMods = new();
            _modsDict = new();
            Base = new();
        }

        public bool ContainsKey(string guid, TKey key)
        {
            return this[guid].ContainsKey(key);
        }

        public bool Remove(string guid, TKey key)
        {
            var wasRemoved = this[guid].Remove(key);

            if (wasRemoved && _appliedMods.ContainsKey(key) && _appliedMods[key] == guid)
            {
                var updated = false;
                foreach (var mod in ModManager.GetEnabledModsLowerThan(guid).Reverse())
                {
                    if (TryGetValue(mod, key, out var value))
                    {
                        // Update the patch from the next highest mod
                        Base[key] = value;
                        _appliedMods[key] = mod;
                        OnBaseChanged(key, BaseChangeType.Update);

                        updated = true;
                        break;
                    }
                }

                if (!updated)
                {
                    // If no mod has a patch, remove the current patch
                    Base.Remove(key);
                    _appliedMods.Remove(key);
                    OnBaseChanged(key, BaseChangeType.Remove);
                }
            }

            return wasRemoved;
        }

        // TODO: make Remove use RemoveRange, and Set use SetRange
        public void RemoveRange(string guid, IEnumerable<TKey> keys)
        {
            var curMod = this[guid];
            var lowerMods = ModManager.GetEnabledModsLowerThan(guid).Reverse();
            var list = new List<(TKey, BaseChangeType)>();

            foreach (var key in keys)
            {
                if (curMod.Remove(key) && _appliedMods.ContainsKey(key) && _appliedMods[key] == guid)
                {
                    var updated = false;
                    foreach (var mod in lowerMods)
                    {
                        if (TryGetValue(mod, key, out var value))
                        {
                            // Update the patch from the next highest mod
                            Base[key] = value;
                            _appliedMods[key] = mod;
                            list.Add((key, BaseChangeType.Update));

                            updated = true;
                            break;
                        }
                    }

                    if (!updated)
                    {
                        Base.Remove(key);
                        _appliedMods.Remove(key);
                        list.Add((key, BaseChangeType.Remove));
                    }
                }
            }

            OnBaseMultiChanged(list);
        }

        public void Set(string guid, TKey key, TValue value)
        {
            this[guid][key] = value;
            BaseChangeType changeType = _appliedMods.ContainsKey(key) ? BaseChangeType.Update : BaseChangeType.Add;

            if (!_appliedMods.ContainsKey(key)
                || _appliedMods[key] == guid
                || ModManager.ShouldReplace(_appliedMods[key], guid))
            {
                // Update patch
                Base[key] = value;
                _appliedMods[key] = guid;
                OnBaseChanged(key, changeType);
            }
        }

        public void SetRange(string guid, IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            var mod = this[guid];
            var list = new List<(TKey, BaseChangeType)>();

            foreach (var pair in pairs)
            {
                if (!_appliedMods.ContainsKey(pair.Key)
                    || _appliedMods[pair.Key] == guid
                    || ModManager.ShouldReplace(_appliedMods[pair.Key], guid))
                {
                    list.Add((pair.Key, _appliedMods.ContainsKey(pair.Key) ? BaseChangeType.Update : BaseChangeType.Add));
                    mod[pair.Key] = pair.Value;
                    Base[pair.Key] = pair.Value;
                    _appliedMods[pair.Key] = guid;
                }
            }

            OnBaseMultiChanged(list);
        }

        public bool TryGetValue(string guid, TKey key, out TValue value)
        {
            return this[guid].TryGetValue(key, out value);
        }

        // We don't need OnModEnabled, as the keys are set during mod loading 
        internal void OnModDisabled(string guid)
        {
            RemoveRange(guid, this[guid].Keys);
            _modsDict.Remove(guid);
        }

        internal void OnLoadOrderChanged()
        {
            IEnumerable<TKey> oldKeys = Base.Keys;
            Base.Clear();
            _appliedMods.Clear();

            // Apply mods from bottom to top
            foreach (var guid in ModManager.GetEnabledMods().Reverse())
            {
                foreach (var pair in this[guid])
                {
                    // Add only if the key does not exist
                    Base.TryAdd(pair.Key, pair.Value);
                    _appliedMods.TryAdd(pair.Key, guid);
                }
            }

            // Invoke reset action
            OnBaseReset(oldKeys);
        }

        /// <summary>
        /// Event handler for when Base has a change (to apply patches in game's dict etc)
        /// </summary>
        public event Action<TKey, BaseChangeType> BaseChangedEvent;

        /// <summary>
        /// Event handler for when Base has a change to multiple elements (to apply patches in game's dict etc)
        /// </summary>
        public event Action<IEnumerable<(TKey, BaseChangeType)>> BaseMultiChangedEvent;

        /// <summary>
        /// Event handler for when Base is completely reset
        /// </summary>
        public event Action<IEnumerable<TKey>> BaseResetEvent;

        private void OnBaseChanged(TKey key, BaseChangeType changeType)
        {
            BaseChangedEvent?.Invoke(key, changeType);
        }

        private void OnBaseMultiChanged(IEnumerable<(TKey, BaseChangeType)> pairs)
        {
            BaseMultiChangedEvent?.Invoke(pairs);
        }

        private void OnBaseReset(IEnumerable<TKey> keys)
        {
            BaseResetEvent?.Invoke(keys);
        }
    }
}
