using Terraria;

namespace Augments
{
    // A Pod upgrade. It upgrades the Pod already floating at your shoulder to Pod 077, which senses traps near you and warns you before you
    // step on one. The behaviour lives in PodTrapSense (ticked by PodPetPlayer, drawn by AugmentUISystem) and it only shows on the owner's own
    // screen, so nothing is sent over the network. See PodUpgrades for how upgrades work.
    public class WatchYourStepAugment : Augment
    {
        public const string ChipId = "pod_trap";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Watch Your Step";
        public override string Description =>
            "Your Pod becomes Pod 077 and flashes red when a trap is near, telling you which way it is.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Senses traps within about 15 tiles, even inside walls: dart, spear, flame and spiky ball traps, pressure plates, spikes and geyser traps. It sees explosives from about 25 tiles and boulders from about 30 tiles. A small chip by the Pod points toward the closest one and says what it is and which way, like \"Trap nearby, below and left\" or \"Boulder nearby, above\". It is automatic and quiet when nothing is around. It also counts traps you placed yourself. Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. It still works while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
    }
}