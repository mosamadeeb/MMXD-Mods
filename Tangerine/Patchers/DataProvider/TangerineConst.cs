using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Tangerine.Manager;
using Tangerine.Utils;

namespace Tangerine.Patchers.DataProvider
{
    /// <summary>
    /// Contains methods for patching properties in <see cref="OrangeConst"/>
    /// </summary>
    public class TangerineConst
    {
        private static bool _isConstInit = false;

        private static readonly Dictionary<string, int> _originalDict = new();
        internal static readonly ModDictionary<string, int> PatchDict = new();

        private readonly string _modGuid;

        static TangerineConst()
        {
            PatchDict.BaseChangedEvent += ApplyPatch;
            PatchDict.BaseResetEvent += ResetPatch;
        }

        internal TangerineConst(string modGuid)
        {
            _modGuid = modGuid;
        }

        /// <summary>
        /// Patches a property in <see cref="OrangeConst"/>
        /// </summary>
        /// <param name="key">Name of the property</param>
        /// <param name="value">Value to set</param>
        /// <returns><see langword="true"/> if the property exists and was patched; otherwise <see langword="false"/></returns>
        public bool PatchConst(string key, int value)
        {
            if (!PropertyExists(key))
            {
                return false;
            }

            PatchDict.Set(_modGuid, key, value);
            return true;
        }

        private static void ApplyPatch(string key, BaseChangeType changeType)
        {
            if (!_isConstInit)
            {
                return;
            }

            switch (changeType)
            {
                case BaseChangeType.Add:

                    // Store original for restoring later
                    _originalDict[key] = GetConst(key);

                    // Fallthrough
                    goto case BaseChangeType.Update;
                case BaseChangeType.Update:

                    // Update the game's dictionary
                    SetConst(key, PatchDict.Base[key]);

                    break;
                case BaseChangeType.Remove:

                    if (_originalDict.TryGetValue(key, out int value))
                    {
                        // Patch original value back
                        SetConst(key, value);
                    }

                    break;
            }
        }

        private static void ResetPatch(IEnumerable<string> oldKeys)
        {
            if (!_isConstInit)
            {
                return;
            }

            // Patch original values back
            foreach (var pair in _originalDict)
            {
                SetConst(pair.Key, pair.Value);
            }

            // Reset original dict and fill it again based on the new Base
            _originalDict.Clear();

            // Patch new Base
            foreach (var pair in PatchDict.Base)
            {
                _originalDict[pair.Key] = GetConst(pair.Key);
                SetConst(pair.Key, pair.Value);
            }
        }

        private static bool PropertyExists(string key)
        {
            return typeof(OrangeConst).GetProperty(key, BindingFlags.Public | BindingFlags.Static) != null;
        }

        private static int GetConst(string key)
        {
            return (int)ReflectionCache.GetPropertyCached(typeof(OrangeConst), null, key);
        }

        private static void SetConst(string key, int value)
        {
            ReflectionCache.SetPropertyCached(typeof(OrangeConst), null, key, value);
        }

        internal static void InitializeHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(TangerineConst));
        }

        // High priority so this prefix is done before TangerineCharacter registers any controllers
        // Because registering a controller will initialize the static fields of OrangeCharacter
        // Which means they will not be using the patched values
        [HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(OrangeConst), nameof(OrangeConst.ConstInit))]
        [HarmonyPostfix]
        private static void ConstInitPostfix()
        {
            _isConstInit = true;
            Plugin.Log.LogInfo($"OrangeConst.ConstInit finished: patching parameters");
            ResetPatch(Array.Empty<string>());
        }
    }
}
