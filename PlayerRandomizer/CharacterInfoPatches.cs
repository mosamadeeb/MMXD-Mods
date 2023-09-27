using enums;
using HarmonyLib;
using OrangeConsoleService;

namespace PlayerRandomizer
{
    internal static class CharacterInfoPatches
    {
        private static int RAND_CHARA_ID => Plugin.RAND_CHARA_ID;

        private static CHARACTER_TABLE _randCharacterTable = null;
        private static CharacterInfo _randCharacterInfo = null;

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.CheckUIReFocus))]
        [HarmonyPrefix]
        private static void GoCheckUISetupPrefix()
        {
            if (!OrangeDataManager.Instance.CHARACTER_TABLE_DICT.ContainsKey(RAND_CHARA_ID))
            {
                if (_randCharacterTable == null)
                {
                    Plugin.Log.LogError($"Could not find random character stored table");
                    return;
                }
                OrangeDataManager.Instance.CHARACTER_TABLE_DICT[RAND_CHARA_ID] = _randCharacterTable;
            }    

            if (!PlayerNetManager.Instance.dicCharacter.ContainsKey(RAND_CHARA_ID))
            {
                if (_randCharacterInfo == null)
                {
                    // Placeholder until it gets added
                    _randCharacterInfo ??= new CharacterInfo()
                    {
                        netDNAInfoDic = new Better.Dictionary<int, NetCharacterDNAInfo>(),
                        netDNALinkInfo = null,
                        netInfo = new NetCharacterInfo
                        {
                            CharacterID = RAND_CHARA_ID,
                            Star = 0,
                            Skin = 0,
                            Accessory = 0,
                            State = 1,
                            ExpireTime = 0,
                            PveCount = 0,
                            PvpCount = 0,
                            FatiguedValue = 0,
                            Favorite = 0,
                        },
                        netSkillDic = new Better.Dictionary<CharacterSkillSlot, NetCharacterSkillInfo>(),
                        netSkinList = new Il2CppSystem.Collections.Generic.List<int>(),
                    };

                    PlayerNetManager.Instance.dicCharacter[RAND_CHARA_ID] = _randCharacterInfo;

                    CharacterService.Instance.AddCharacter(RAND_CHARA_ID, 1, 0);
                    _randCharacterInfo = PlayerNetManager.Instance.dicCharacter[RAND_CHARA_ID];
                    _randCharacterInfo.netInfo.State = 1;
                }
                else
                {
                    PlayerNetManager.Instance.dicCharacter[RAND_CHARA_ID] = _randCharacterInfo;
                }
            }

            // Check if we should equip the random char
            if (ConfigManager.InitialRandomChar.Value)
            {
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.StandbyChara = RAND_CHARA_ID;
            }
        }

        [HarmonyPatch(typeof(GoCheckUI), nameof(GoCheckUI.OnSelectCharacter))]
        [HarmonyPrefix]
        private static bool OnSelectCharacterPrefix(GoCheckUI __instance)
        {
            // Prevent loading the character info UI for the random character
            // Skip original if the selected character is the random char
            return !(__instance.NowSelectMode == 0 && __instance.refSelectCharacterID == RAND_CHARA_ID);
        }

        [HarmonyPatch(typeof(CharacterInfoSelect), nameof(CharacterInfoSelect.RefreshMenu))]
        [HarmonyPatch(typeof(CharacterInfoUI), nameof(CharacterInfoUI.Setup))]
        [HarmonyPrefix]
        private static void CharacterInfoUISetupPrefix()
        {
            if (PlayerNetManager.Instance.playerInfo.netPlayerInfo.StandbyChara == RAND_CHARA_ID)
            {
                // Set to X
                PlayerNetManager.Instance.playerInfo.netPlayerInfo.StandbyChara = 1;
            }

            if (PlayerNetManager.Instance.dicCharacter.ContainsKey(RAND_CHARA_ID))
            {
                PlayerNetManager.Instance.dicCharacter.Remove(RAND_CHARA_ID);
            }

            if (OrangeDataManager.Instance.CHARACTER_TABLE_DICT.TryGetValue(RAND_CHARA_ID, out var table))
            {
                // Store table
                _randCharacterTable = table;

                OrangeDataManager.Instance.CHARACTER_TABLE_DICT.Remove(RAND_CHARA_ID);
                CharacterHelper.Instance.SortCharacterList();
            }
        }
    }
}
