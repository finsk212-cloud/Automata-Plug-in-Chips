using Terraria;

namespace Augments
{
    // A Pod upgrade. It upgrades the Pod already floating at your shoulder to Pod 311, which sniffs out the nearest ore, shows what and how far,
    // and points the way with an arrow. All of the behaviour lives in PodOreSniffer (ticked by PodPetPlayer, drawn by AugmentUISystem), and it
    // only shows on the owner's own screen, so nothing is sent over the network. See PodUpgrades for how upgrades work.
    public class OreSnifferAugment : Augment
    {
        public const string ChipId = "pod_ore";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Ore Sniffer";
        public override string Description =>
            "Your Pod becomes Pod 311 and sniffs out the nearest ore, giving you a riddle about it and an arrow to follow.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Sniffs for 5 seconds, says its riddle, then rests for 6 seconds before it sniffs again. Looks about 60 tiles around you, through walls and dirt, and ignores plain stone. An arrow by the Pod points toward the ore. The Pod never names it, it hints with a riddle, plus how close you are and a direction like \"below and left\". Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. It still works while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
    }
}