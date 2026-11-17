using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    // A Pod upgrade. It upgrades the Pod already floating at your shoulder to Pod 311, which sniffs out the nearest ore when you press its
    // button, shows a riddle about it and points the way with an arrow. All of the behaviour lives in PodOreSniffer (ticked by PodPetPlayer,
    // drawn by AugmentUISystem), and it only shows on the owner's own screen, so nothing is sent over the network. See PodUpgrades.
    public class OreSnifferAugment : Augment
    {
        public const string ChipId = "pod_ore";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Ore Sniffer";
        public override string Description =>
            $"Your Pod becomes Pod 311. {AugmentText.Active("Active:")} Press [{AugmentText.Key(Augments.SniffOreKeybind)}] and your Pod sniffs out the nearest ore, giving you a riddle about it and an arrow to follow. {AugmentText.Cooldown("6 seconds cooldown")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(The Pod sniffs for 2 seconds, then the riddle and arrow stay up for 5 seconds. It looks about 60 tiles around you, through walls and dirt, and ignores plain stone. The riddle never names the ore, but it also tells you how close you are and a direction like \"below and left\". Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. It still works while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
        public override ModKeybind ActiveModKeybind => Augments.SniffOreKeybind;
        public override int CooldownRemaining => Main.LocalPlayer.GetModPlayer<PodPetPlayer>().Sniffer.CooldownRemaining;
    }
}