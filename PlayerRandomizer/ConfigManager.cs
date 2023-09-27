using BepInEx.Configuration;
using static PlayerRandomizer.Plugin;

namespace PlayerRandomizer
{
    internal static class ConfigManager
    {
        internal enum RandomGeneration
        {
            Disabled,
            RandomSelectionOnly,
            AlwaysEnabled,
        }

        private const string BattlePrepSection = "Battle Preparation";
        private const string RandomizationSection = "Randomization";

        // Battle Preparation
        public static ConfigEntry<bool> InitialRandomChar { get; set; }
        public static ConfigEntry<RandomWeaponType> InitialRandomMainWeapon { get; set; }
        public static ConfigEntry<RandomWeaponType> InitialRandomSubWeapon { get; set; }

        // Randomization
        public static ConfigEntry<RandomGeneration> RandomizeSkin { get; set; }
        public static ConfigEntry<RandomGeneration> RandomizeMainChip { get; set; }
        public static ConfigEntry<RandomGeneration> RandomizeSubChip { get; set; }
        public static ConfigEntry<bool> RandomizeMainFinalStrike { get; set; }
        public static ConfigEntry<bool> RandomizeSubFinalStrike { get; set; }

        public static void Initialize()
        {
            InitialRandomChar = Plugin.Config.Bind(BattlePrepSection, "Initially select Random character", false,
                new ConfigDescription($"If enabled, will select the Random character on opening the battle prep screen."));

            InitialRandomMainWeapon = Plugin.Config.Bind(BattlePrepSection, "Initially select random Main weapon", RandomWeaponType.None,
                new ConfigDescription($"Select the Random main weapon with the given weapon type on opening the battle prep screen. Set to \"None\" to disable."));

            InitialRandomSubWeapon = Plugin.Config.Bind(BattlePrepSection, "Initially select random Sub weapon", RandomWeaponType.None,
                new ConfigDescription($"Select the Random sub weapon with the given weapon type on opening the battle prep screen. Set to \"None\" to disable."));

            RandomizeSkin = Plugin.Config.Bind(RandomizationSection, "Randomize Skin", RandomGeneration.RandomSelectionOnly,
                new ConfigDescription($"Use a random unlocked skin for the character. This will unequip the current skin for the character. Can be enabled for Random character only, or always enabled."));

            RandomizeMainChip = Plugin.Config.Bind(RandomizationSection, "Randomize Chip for Main weapon", RandomGeneration.Disabled,
                new ConfigDescription($"Use a random unlocked chip for the main weapon. This will unequip the current chip for the weapon. Can be enabled for Random weapons only, or always enabled."));

            RandomizeSubChip = Plugin.Config.Bind(RandomizationSection, "Randomize Chip for Sub weapon", RandomGeneration.Disabled,
                new ConfigDescription($"Use a random unlocked chip for the sub weapon. This will unequip the current chip for the weapon. Can be enabled for Random weapons only, or always enabled."));

            RandomizeMainFinalStrike = Plugin.Config.Bind(RandomizationSection, "Randomize Main DiVE Trigger", false,
                new ConfigDescription($"Use a random DiVE Trigger in the main slot. This will unequip the current DiVE Trigger in that slot."));

            RandomizeSubFinalStrike = Plugin.Config.Bind(RandomizationSection, "Randomize Sub DiVE Trigger", false,
                new ConfigDescription($"Use a random DiVE Trigger in the sub slot. This will unequip the current DiVE Trigger in that slot."));
        }
    }
}
