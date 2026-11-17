using Terraria;

namespace Augments
{
    // A Pod upgrade. It adds nothing next to you: it upgrades the Pod already floating at your shoulder to Pod 096, which shines a bright
    // warm light. The light itself is made in PodPetPlayer (every machine lights its own screen, so nothing is sent over the network), and
    // it works while the Pod is hidden too. See PodUpgrades for how upgrades work.
    public class TinyLighthouseAugment : Augment
    {
        public const string ChipId = "pod_light";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Lighthouse";
        public override string Description =>
            "Your Pod becomes Pod 096 and lights up the area around you with a warm glow.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Everyone near you sees by the light too. Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. The light works even while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Support;
    }
}