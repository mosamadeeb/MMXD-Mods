﻿using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using Tangerine.Manager;

namespace Tangerine.Patchers
{
    public class TangerineCharacter
    {
        internal static readonly ModDictionary<int, Type> CharacterDict = new();
        private static readonly List<(Type, Type[])> _initialControllerList = new();
        private static bool _orangeConstInitialized = false;

        internal static void InitializeHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(TangerineCharacter));
        }

        private readonly string _modGuid;
        internal TangerineCharacter(string modGuid)
        {
            _modGuid = modGuid;
        }

        private static void RegisterController(Type controllerType, Type[] interfaces)
        {
            if (!_orangeConstInitialized)
            {
                // Delay registration until OrangeConst is initialized
                // Prevents an issue where OrangeCharacter's static fields are initialized before OrangeConst is
                _initialControllerList.Add((controllerType, interfaces));
            }
            else if (!ClassInjector.IsTypeRegisteredInIl2Cpp(controllerType))
            {
                Plugin.Log.LogWarning($"Registering character controller: {controllerType.FullName}");

                var options = new RegisterTypeOptions()
                {
                    Interfaces = new Il2CppInterfaceCollection(interfaces),
                };

                ClassInjector.RegisterTypeInIl2Cpp(controllerType, options);
            }
        }

        /// <summary>
        /// Adds a controller class by injecting it into the game's runtime
        /// </summary>
        /// <param name="characterId"><c>n_ID</c> of the character that will use this controller</param>
        /// <param name="controllerType"><see langword="typeof"/> the controller class</param>
        /// <param name="interfaces">Interfaces the class should implement (e.g. <see cref="ILogicUpdate"/>)</param>
        public void AddController(int characterId, Type controllerType, Type[] interfaces)
        {
            CharacterDict.Set(_modGuid, characterId, controllerType);
            RegisterController(controllerType, interfaces);

            // Example of injecting an enum value. This is not needed as the game can cast the values itself
            // EnumInjector.InjectEnumValues<EControlCharacter>(new Dictionary<string, object>() { { "X_DMC", 139 } });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public bool RemoveController(int characterId)
        {
            // We can't unregister controllers, so all we can do is stop overriding the character id
            return CharacterDict.Remove(_modGuid, characterId);
        }

        [HarmonyPatch(typeof(CharacterControlFactory), nameof(CharacterControlFactory.GetCharacterControlType))]
        [HarmonyPrefix]
        private static bool CharacterControlTypePrefix(EControlCharacter character, int subID, ref Il2CppSystem.Type __result)
        {
            if (CharacterDict.Base.TryGetValue((int)character, out var type))
            {
                __result = Il2CppType.From(type);
                Plugin.Log.LogWarning($"Loading character controller {__result.Name}");
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(OrangeConst), nameof(OrangeConst.ConstInit))]
        [HarmonyPostfix]
        private static void OrangeConstInitPostfix()
        {
            if (!_orangeConstInitialized)
            {
                _orangeConstInitialized = true;
                foreach (var args in _initialControllerList)
                {
                    RegisterController(args.Item1, args.Item2);
                }

                _initialControllerList.Clear();
            }
        }
    }
}
