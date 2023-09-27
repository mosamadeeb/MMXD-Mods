using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using enums;
using HarmonyLib;
using Il2CppSystem.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Manager.Mod;

namespace PlayerRandomizer;

// Add dependency to Tangerine. This is required for the mod to show up in the mods menu.
[BepInDependency(Tangerine.Plugin.GUID, BepInDependency.DependencyFlags.HardDependency)]
// Do not modify this line. You can change AssemblyName, Product, and Version directly in the .csproj
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : TangerinePlugin
{
    private TangerineMod _tangerine = null;
    private static Harmony _harmony;
    internal static new ManualLogSource Log;
    internal static new ConfigFile Config;

    private static readonly System.Random _random = new(Environment.TickCount);

    internal const int RAND_CHARA_ID = 700001;
    internal const int RAND_WEAPON_ID_MIN = 700000;
    internal const int RAND_WEAPON_ID_MAX = 700100;

    internal const int DEFAULT_CHARA_ID = 1;
    internal const int DEFAULT_BUSTER_ID = 100001;
    internal const int DEFAULT_SABER_ID = 101001;

    internal enum RandomWeaponType
    {
        None,
        Buster = 700000,
        Spray = 700002,
        Saber = 700003,
        MachineGun = 700005,
        Launcher = 700007,
        Any = 700008,
    }

    internal static readonly int[] RandWeaponIds = Enum.GetValues<RandomWeaponType>().Skip(1).Cast<int>().ToArray();

    public override void Load(TangerineMod tangerine)
    {
        _tangerine = tangerine;

        // Plugin startup logic
        Plugin.Log = base.Log;
        Plugin.Config = base.Config;
        Log.LogInfo($"Tangerine plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        ConfigManager.Initialize();

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        _harmony.PatchAll(typeof(Plugin));
        _harmony.PatchAll(typeof(CharacterInfoPatches));
        _harmony.PatchAll(typeof(WeaponInfoPatches));
    }

    public override bool Unload()
    {
        _harmony.UnpatchSelf();
        return true;
    }

    [HarmonyPatch(typeof(StageHelper), nameof(StageHelper.GetStageCharacterStruct))]
    [HarmonyPrefix]
    static void GetStageCharacterStructPrefix()
    {
        var netInfo = PlayerNetManager.Instance.playerInfo.netPlayerInfo;

        // Randomize character
        bool charaWasRandomized = false;
        if (netInfo.StandbyChara == RAND_CHARA_ID)
        {
            Log.LogInfo($"Setting random character");
            var charaId = GetRandomCharacter();

            // We need to set something, even if the player doesn't own anything
            charaWasRandomized = charaId.HasValue;
            netInfo.StandbyChara = charaId ?? DEFAULT_CHARA_ID;
        }

        if (ConfigManager.RandomizeSkin.Value == ConfigManager.RandomGeneration.AlwaysEnabled
            || (charaWasRandomized && ConfigManager.RandomizeSkin.Value == ConfigManager.RandomGeneration.RandomSelectionOnly))
        {
            // Update character's equipped skin
            if (PlayerNetManager.Instance.dicCharacter.TryGetValue(netInfo.StandbyChara, out var charaInfo))
            {
                Log.LogInfo($"Setting random skin");
                var skinId = GetRandomCharacterSkin(netInfo.StandbyChara);
                if (skinId.HasValue)
                {
                    charaInfo.netInfo.Skin = skinId.Value;
                }
            }
        }

        var weaponTableDict = new Dictionary<int, WEAPON_TABLE>();
        foreach (var item in PlayerNetManager.Instance.dicWeapon)
        {
            if (item.Value.netInfo != null
                && item.Key >= OrangeConst.INITIAL_WEAPON_ID
                && item.Key < 200000    // Valid weapon IDs are in the 100000-199999 range (this excludes the random weapons)
                && OrangeDataManager.Instance.WEAPON_TABLE_DICT.TryGetValue(item.Key, out var table))
            {
                weaponTableDict[item.Key] = table;
            }
        }

        var weaponList = new int[] { netInfo.MainWeaponID, netInfo.SubWeaponID };
        var randomizeChipList = new ConfigManager.RandomGeneration[] { ConfigManager.RandomizeMainChip.Value, ConfigManager.RandomizeSubChip.Value };
        
        // Remove non-random weapons from the generation to avoid getting duplicate weapons
        for (int i = 0; i < 2; i++)
        {
            if (!IsRandWeapon(weaponList[i]))
            {
                weaponTableDict.Remove(weaponList[i]);
            }
        }
        
        for (int i = 0; i < 2; i++)
        {
            var weaponId = weaponList[i];
            bool weaponWasRandomized = false;
            if (IsRandWeapon(weaponId))
            {
                weaponWasRandomized = true;
                Log.LogInfo($"Setting random {(i == 0 ? "main" : "sub")} weapon");

                var weaponType = GetWeaponType(weaponId);
                if (!weaponType.HasValue)
                {
                    continue;
                }

                var randWeaponId = GetRandomWeapon(weaponTableDict, weaponType.Value);

                // If no weapon of the specified type was found, give any random weapon. If the player has no weapons left, give default buster
                weaponList[i] = randWeaponId ?? (GetRandomWeapon(weaponTableDict, WeaponType.All) ?? DEFAULT_BUSTER_ID);
            }

            if (randomizeChipList[i] == ConfigManager.RandomGeneration.AlwaysEnabled
                || (weaponWasRandomized && randomizeChipList[i] == ConfigManager.RandomGeneration.RandomSelectionOnly))
            {
                // Set random weapon chip
                var randChipId = GetRandomChip();
                if (randChipId.HasValue && PlayerNetManager.Instance.dicWeapon.TryGetValue(weaponList[i], out var weaponInfo))
                {
                    weaponInfo.netInfo.Chip = (ushort)randChipId.Value;
                }
            }
        }

        if (weaponList[0] == weaponList[1])
        {
            // Only case this would happen is if both random categories did not have any unlocked weapons
            weaponList[1] = (weaponList[1] == DEFAULT_BUSTER_ID) ? DEFAULT_SABER_ID : DEFAULT_BUSTER_ID;
        }

        netInfo.MainWeaponID = weaponList[0];
        netInfo.SubWeaponID = weaponList[1];

        var fStrikeList = new int[] { netInfo.MainWeaponFSID, netInfo.SubWeaponFSID };
        var randomizeFStrikeList = new bool[] { ConfigManager.RandomizeMainFinalStrike.Value, ConfigManager.RandomizeSubFinalStrike.Value };
        var netFStrikeList = PlayerNetManager.Instance.dicFinalStrike.Keys.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>().ToList();

        // Remove non-random strikes from the generation to avoid getting duplicate strikes
        for (int i = 0; i < 2; i++)
        {
            if (!randomizeFStrikeList[i])
            {
                netFStrikeList.Remove(fStrikeList[i]);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            // Do not randomize strike if it is not equipped for this slot
            if (fStrikeList[i] != 0 && randomizeFStrikeList[i])
            {
                Log.LogInfo($"Setting random {(i == 0 ? "main" : "sub")} DiVE trigger");
                var randFinalStrike = GetRandomFinalStrike(netFStrikeList);
                if (randFinalStrike.HasValue)
                {
                    fStrikeList[i] = randFinalStrike.Value;
                }
            }
        }

        netInfo.MainWeaponFSID = fStrikeList[0];
        netInfo.SubWeaponFSID = fStrikeList[1];
    }

    private static int? GetRandomCharacter()
    {
        var charaIdList = new List<int>();
        foreach (var item in PlayerNetManager.Instance.dicCharacter)
        {
            // Only add unlocked characters
            if (item.Value.netInfo?.State == 1 && item.Key != RAND_CHARA_ID)
            {
                charaIdList.Add(item.Key);
            }
        }

        if (GetRandom(charaIdList, out var charaId))
        {
            Log.LogInfo($"Random character ID: {charaId}");
            return charaId;
        }

        return null;
    }

    private static int? GetRandomCharacterSkin(int charaId)
    {
        if (GetRandom(PlayerNetManager.Instance.dicCharacter[charaId].netSkinList, out var skinId))
        {
            Log.LogInfo($"Random skin ID: {skinId}");
            return skinId;
        }

        return null;
    }

    private static int? GetRandomWeapon(Dictionary<int, WEAPON_TABLE> weaponTableDict, WeaponType weaponType)
    {
        List<int> weaponIdList = (
            weaponTableDict.Where(pair => (pair.Value.n_TYPE & (int)weaponType) != 0))
            .Select(pair => pair.Key).ToList();

        if (weaponIdList.Count != 0 && GetRandom(weaponIdList, out var weaponId))
        {
            Log.LogInfo($"Random weapon ID: {weaponId}");
            weaponTableDict.Remove(weaponId);
            return weaponId;
        }

        return null;
    }

    private static int? GetRandomChip()
    {
        if (GetRandom(PlayerNetManager.Instance.dicChip.Keys.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>().ToList(), out var chipId))
        {
            Log.LogInfo($"Random chip ID: {chipId}");
            return chipId;
        }

        return null;
    }

    private static int? GetRandomFinalStrike(Il2CppSystem.Collections.Generic.List<int> strikeList)
    {
        if (GetRandom(strikeList, out var strikeId))
        {
            Log.LogInfo($"Random DiVE Trigger ID: {strikeId}");
            strikeList.Remove(strikeId);
            return strikeId;
        }

        return null;
    }

    private static bool GetRandom<T>(List<T> list, out T value)
    {
        value = default;

        if (list.Count > 0)
        {
            value = list[_random.Next(list.Count)];
            return true;
        }

        return false;
    }

    private static bool GetRandom<T>(Il2CppSystem.Collections.Generic.List<T> list, out T value)
    {
        value = default;

        if (list.Count > 0)
        {
            value = list[_random.Next(list.Count)];
            return true;
        }

        return false;
    }

    internal static bool IsRandWeapon(int id)
    {
        return id >= RAND_WEAPON_ID_MIN && id < RAND_WEAPON_ID_MAX;
    }

    internal static WeaponType? GetWeaponType(int id)
    {
        if (IsRandWeapon(id))
        {
            var typeVal = (int)Math.Pow(2, id - RAND_WEAPON_ID_MIN);

            if (typeVal == 256)
            {
                // All weapon types
                typeVal -= 1;
            }

            if (typeVal >= 0 && typeVal < 256)
            {
                switch ((WeaponType)typeVal)
                {
                    case WeaponType.Spray:
                    case WeaponType.SprayHeavy:
                        return WeaponType.Spray | WeaponType.SprayHeavy;
                    case WeaponType.MGun:
                    case WeaponType.Gatling:
                        return WeaponType.MGun | WeaponType.Gatling;
                    default:
                        return (WeaponType)typeVal;
                }
            }

            Log.LogWarning($"Unexpected weapon type: {typeVal}");
        }

        return null;
    }
}
