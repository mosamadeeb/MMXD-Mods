using Cinemachine.PostFX;
using enums;
using HarmonyLib;
using OrangeConsoleService;
using System;
using System.Collections.Generic;
using static PlayerRandomizer.Plugin;

namespace PlayerRandomizer
{
    internal static class WeaponInfoPatches
    {

        private static readonly Dictionary<int, WEAPON_TABLE> _randWeaponTables = new();
        private static readonly Dictionary<int, WeaponInfo> _randWeaponInfos = new();

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.Setup), new Type[] { typeof(StageType), typeof(StageMode) })]
        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.CheckUIReFocus))]
        [HarmonyPrefix]
        private static void GoCheckUISetupPrefix()
        {
            foreach (var randWeaponId in Plugin.RandWeaponIds)
            {
                if (!OrangeDataManager.Instance.WEAPON_TABLE_DICT.ContainsKey(randWeaponId))
                {
                    if (!_randWeaponTables.ContainsKey(randWeaponId))
                    {
                        Plugin.Log.LogError($"Could not find random weapon ID in _randWeaponTables: {randWeaponId}");
                        continue;
                    }
                    OrangeDataManager.Instance.WEAPON_TABLE_DICT[randWeaponId] = _randWeaponTables[randWeaponId];
                }

                if (!PlayerNetManager.Instance.dicWeapon.ContainsKey(randWeaponId))
                {
                    if (!_randWeaponInfos.ContainsKey(randWeaponId))
                    {
                        // Placeholder until it gets added
                        _randWeaponInfos[randWeaponId] = new WeaponInfo()
                        {
                            netDiveSkillInfo = null,
                            netInfo = new NetWeaponInfo
                            {
                                WeaponID = randWeaponId,
                                Star = 0,
                                Skin = 0,
                                Prof = 0,
                                Exp = 0,
                                Chip = 0,
                                PveCount = 0,
                                PvpCount = 0,
                                Favorite = 0,
                            },
                            netExpertInfos = new Il2CppSystem.Collections.Generic.List<NetWeaponExpertInfo>(),
                            netSkillInfos = new Il2CppSystem.Collections.Generic.List<NetWeaponSkillInfo>(),
                        };

                        PlayerNetManager.Instance.dicWeapon[randWeaponId] = _randWeaponInfos[randWeaponId];
                        WeaponService.Instance.AddWeapon(randWeaponId);
                    }
                    else
                    {
                        PlayerNetManager.Instance.dicWeapon[randWeaponId] = _randWeaponInfos[randWeaponId];
                    }
                }
            }

            // Check if we should equip the random weapons
            if (ConfigManager.InitialRandomMainWeapon.Value != RandomWeaponType.None)
            {
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID = (int)ConfigManager.InitialRandomMainWeapon.Value;
            }

            if (ConfigManager.InitialRandomSubWeapon.Value != RandomWeaponType.None)
            {
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.SubWeaponID = (int)ConfigManager.InitialRandomSubWeapon.Value;
            }
        }

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.OnSelectMainWeapon))]
        [HarmonyPrefix]
        private static bool OnSelectMainWeaponPrefix(GoCheckUI __instance)
        {
            // Prevent loading the weapon info UI for the random weapon
            // Skip original if the selected weapon is the random weapon
            return !(__instance.NowSelectMode == 1 && Plugin.IsRandWeapon(__instance.nMainWeaponID));
        }

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.OnSelectSubWeapon))]
        [HarmonyPrefix]
        private static bool OnSelectSubWeaponPrefix(GoCheckUI __instance)
        {
            return !(__instance.NowSelectMode == 2 && Plugin.IsRandWeapon(__instance.nSubWeaponID));
        }

        [HarmonyPatch(typeof(WeaponMainUI), nameof(WeaponMainUI.Start))]
        [HarmonyPatch(typeof(WeaponInfoUI), nameof(WeaponInfoUI.initalization_data))]
        [HarmonyPrefix]
        private static void WeaponInfoUISetupPrefix()
        {
            if (Plugin.IsRandWeapon(PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID))
            {
                // Set to Buster
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID =
                    (PlayerNetManager.Instance.playerInfo.netPlayerInfo.SubWeaponID == Plugin.DEFAULT_BUSTER_ID)
                    ? Plugin.DEFAULT_SABER_ID
                    : Plugin.DEFAULT_BUSTER_ID;
            }

            if (Plugin.IsRandWeapon(PlayerNetManager.Instance.playerInfo.netPlayerInfo.SubWeaponID))
            {
                // Set to Saber
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.SubWeaponID =
                    (PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID == Plugin.DEFAULT_SABER_ID)
                    ? Plugin.DEFAULT_BUSTER_ID
                    : Plugin.DEFAULT_SABER_ID;
            }


            foreach (var randWeaponId in Plugin.RandWeaponIds)
            {
                if (PlayerNetManager.Instance.dicWeapon.ContainsKey(randWeaponId))
                {
                    PlayerNetManager.Instance.dicWeapon.Remove(randWeaponId);
                }

                if (OrangeDataManager.Instance.WEAPON_TABLE_DICT.TryGetValue(randWeaponId, out var table))
                {
                    // Store table
                    _randWeaponTables[randWeaponId] = table;
                    OrangeDataManager.Instance.WEAPON_TABLE_DICT.Remove(randWeaponId);
                }

                EquipHelper.Instance.SortWeaponList();
            }
        }

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.SetSelectWeapon))]
        [HarmonyPrefix]
        static bool SetSelectWeaponPrefix(int nID, GoCheckUI __instance)
        {
            if (__instance.bLockNet || !Plugin.IsRandWeapon(nID))
            {
                return true;
            }

            int netId;
            bool isMain = __instance.NowSelectMode == 1;
            if (__instance.NowSelectMode == 1 && __instance.nSubWeaponID == nID)
            {
                // Setting main weapon same as sub weapon
                __instance.nMainWeaponID = nID;
                netId = PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID;
            }
            else if (__instance.NowSelectMode == 2 && __instance.nMainWeaponID == nID)
            {
                // Setting sub weapon same as main weapon
                __instance.nSubWeaponID = nID;
                netId = PlayerNetManager.Instance.playerInfo.netPlayerInfo.SubWeaponID;
            }
            else
            {
                return true;
            }

            AudioManager.Instance.PlaySystemSE(SystemSE.CRI_SYSTEMSE_SYS_OK14);
            var action = new Action(() =>
            {
                __instance.SetMainSubWeapon(true);
                GenericEventManager.Instance.NotifyEvent(EventManager.ID.UPDATE_RENDER_WEAPON, nID);
            });

            if (PlayerNetManager.Instance.playerInfo.netPlayerInfo.MainWeaponID == nID)
            {
                action.Invoke();
            }
            else
            {
                OrangeGameManager.Instance.WeaponWield(nID, isMain ? WeaponWieldType.MainWeapon : WeaponWieldType.SubWeapon, action, false);
            }

            return false;
        }
    }
}
