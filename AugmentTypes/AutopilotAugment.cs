using Terraria;

namespace Augments
{
    // A Pod upgrade. It adds nothing next to you: it upgrades the Pod already floating at your shoulder to Pod 153, which heals the
    // most injured nearby ally. All of the behaviour lives in PodAutopilot (ticked by PodPetPlayer); this class only describes the chip.
    // See PodUpgrades for how upgrades work and how to add another.
    public class AutopilotAugment : Augment
    {
        public const string ChipId = "autopilot";

        public override string Id => ChipId;
        public override string DisplayName => "Autopilot";
        public override string Description =>
            $"Pod upgrade: your Pod becomes Pod 153 and heals the lowest-health nearby player for {AugmentText.Healing("5 HP")} per second.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Heals 1 HP every 0.2 seconds. Doesn't heal NPCs. Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. The healing works even while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Support;
    }
}
