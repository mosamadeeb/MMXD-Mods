using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;

namespace Tangerine.Game
{
    public static class TangerineCharacter
    {
        private static readonly Dictionary<int, Type> _characterDict = new();

        internal static void InitializeHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(TangerineCharacter));
        }

        /// <summary>
        /// Adds a controller class by injecting it into the game's runtime
        /// </summary>
        /// <param name="characterId"><c>n_ID</c> of the character that will use this controller</param>
        /// <param name="controllerType"><see langword="typeof"/> the controller class</param>
        /// <param name="interfaces">Interfaces the class should implement (e.g. <see cref="ILogicUpdate"/>)</param>
        public static void AddController(int characterId, Type controllerType, Type[] interfaces)
        {
            // Throw an exception if a controller with the same ID is already registered
            _characterDict[characterId] = controllerType;
            
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp(controllerType))
            {
                Plugin.Log.LogWarning($"Registering character controller: {controllerType}");

                var options = new RegisterTypeOptions()
                {
                    Interfaces = new Il2CppInterfaceCollection(interfaces),
                };

                ClassInjector.RegisterTypeInIl2Cpp(controllerType, options);
            }

            // Example of injecting an enum value. This is not needed as the game can cast the values itself
            // EnumInjector.InjectEnumValues<EControlCharacter>(new Dictionary<string, object>() { { "X_DMC", 139 } });
        }

        [HarmonyPatch(typeof(CharacterControlFactory), nameof(CharacterControlFactory.GetCharacterControlType))]
        [HarmonyPrefix]
        private static bool CharacterControlTypePrefix(EControlCharacter character, int subID, ref Il2CppSystem.Type __result)
        {
            if (_characterDict.TryGetValue((int)character, out var type))
            {
                __result = Il2CppType.From(type);
                Plugin.Log.LogWarning($"Loading character controller {__result}");
                return false;
            }

            return true;
        }
    }
}
