using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Tangerine.Manager.Mod;
using UnityEngine;

namespace HomeCharacterAnimation;

// Add dependency to Tangerine. This is required for the mod to show up in the mods menu.
[BepInDependency(Tangerine.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
// Do not modify this line. You can change AssemblyName, Product, and Version directly in the .csproj
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : TangerinePlugin
{
    private TangerineMod _tangerine = null;
    private static Harmony _harmony;
    internal static new ManualLogSource Log;

    public override void Load(TangerineMod tangerine)
    {
        _tangerine = tangerine;

        // Plugin startup logic
        Plugin.Log = base.Log;
        Log.LogInfo($"Tangerine plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        // This line will apply all patches in this class (Plugin) when uncommented
        _harmony.PatchAll(typeof(Plugin));
    }

    public override bool Unload()
    {
        _harmony.UnpatchSelf();
        return true;
    }

    [HarmonyPatch(typeof(HometopSceneController), nameof(HometopSceneController.UpdateCharacter))]
    [HarmonyPrefix]
    static void UpdateCharacterPrefix(CHARACTER_TABLE character)
    {
        // Make the game load the unique anims and the _U_S prefab
        character.n_SPECIAL_SHOWPOSE = 1;
    }

    [HarmonyPatch(typeof(HometopSceneController.__c__DisplayClass19_2), nameof(HometopSceneController.__c__DisplayClass19_2._UpdateCharacter_b__3))]
    [HarmonyPostfix]
    static void UpdateCharacterPostfix(HometopSceneController.__c__DisplayClass19_2 __instance)
    {
        // Reduce model size a tiiiny bit
        var character = __instance.field_Public___c__DisplayClass19_1_0.field_Public___c__DisplayClass19_0_0.character;
        __instance.field_Public___c__DisplayClass19_1_0.go.transform.localScale = new Vector3(
            character.f_MODELSIZE - 0.07f, character.f_MODELSIZE - 0.07f, character.f_MODELSIZE - 0.07f);
    }

    [HarmonyPatch(typeof(OrangeAnimatonHelper), nameof(OrangeAnimatonHelper.GetUniqueDebutName))]
    [HarmonyPrefix]
    static void GetUniqueDebutNamePrefix(ref string s_modelName)
    {
        // Allow loading unique debut for skins
        s_modelName = s_modelName[..^4] + "_000";
    }

    [HarmonyPatch(typeof(CharacterAnimatorStandBy), nameof(CharacterAnimatorStandBy.SetWeapon))]
    [HarmonyPostfix]
    static void SetWeaponPostfix(CharacterAnimatorStandBy __instance)
    {
        // Disable weapon (always)
        __instance.weapon.SetActive(false);
    }
}
