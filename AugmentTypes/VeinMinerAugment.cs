using Terraria;

namespace Augments
{
    // A Pod upgrade. It upgrades the Pod already floating at your shoulder to Pod 360, which mines the rest of an ore vein for you: when you
    // mine an ore, the ore tiles of the same kind touching it are mined too. The behaviour lives in PodVeinMiner (a GlobalTile that notices
    // your mining and a ModSystem that breaks the extra tiles a few at a time). See PodUpgrades for how upgrades work.
    public class VeinMinerAugment : Augment
    {
        public const string ChipId = "pod_vein";

        public override string Id => ChipId;
        public override string DisplayName => "Upgrade Pod: Vein Miner";
        public override string Description =>
            "Your Pod becomes Pod 360. When you mine an ore, your Pod also mines up to " + PodVeinMiner.MaxExtraTiles + " more ore tiles of the same kind touching it.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "[c/94A3B8:(Works with any pickaxe or drill, on ores and gems, and only on tiles your pickaxe is strong enough to mine. The extra tiles break one after another, nearest first, and drop their loot as normal. It does not chain from the extra tiles, so one swing never eats the whole cave. Upgrades the Pod you already have instead of adding another, and works alongside other Pod upgrades. It still works while the Pod is hidden.)]";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
    }
}