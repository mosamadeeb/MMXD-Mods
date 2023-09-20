using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Manager;
using Tangerine.Patchers.LogicUpdate;

namespace Tangerine.Patchers
{
    /// <summary>
    /// Contains methods for adding character controller classes that inherit from <see cref="CharacterControlBase"/>
    /// </summary>
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

        private static void RegisterController(Type controllerType, Type[] interfaces = null)
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

                interfaces ??= Array.Empty<Type>();
                if (typeof(ITangerineLogicUpdate).IsAssignableFrom(controllerType)
                    && !interfaces.Contains(typeof(ILogicUpdate)))
                {
                    // Add ILogicUpdate to list of interfaces
                    interfaces = interfaces.AddToArray(typeof(ILogicUpdate));
                }

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
        /// <param name="interfaces">Il2Cpp interfaces the class should implement, if any (e.g. <see cref="ILogicUpdate"/>)</param>
        public void AddController(int characterId, Type controllerType, Type[] interfaces = null)
        {
            CharacterDict.Set(_modGuid, characterId, controllerType);
            RegisterController(controllerType, interfaces);

            // Example of injecting an enum value. This is not needed as the game can cast the values itself
            // EnumInjector.InjectEnumValues<EControlCharacter>(new Dictionary<string, object>() { { "X_DMC", 139 } });
        }

        /// <summary>
        /// Removes a controller so it will not be loaded by the game.
        /// </summary>
        /// <param name="characterId"><c>n_ID</c> of the character that the controller was added for</param>
        /// <returns><see langword="true"/> if the controller was successfully removed; otherwise <see langword="false"/></returns>
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
