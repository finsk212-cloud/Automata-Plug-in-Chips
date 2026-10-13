using Terraria;

namespace Augments
{
    public class SharedBrewAugment : Augment
    {
        public override string Id => "shared_brew";
        public override string DisplayName => "Shared Brew";
        public override string Description =>
            $"Nearby teammates also get the {AugmentText.Trigger("potion buffs")} you have active.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            "Only drinkable potions are shared (not food). Teammates get a 5s copy that keeps refreshing while they stay in range. " +
            "[c/94A3B8:(The last 5s of your own potion are not shared.)]";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Support;
        public override bool HasAuraEffect => true;
        // No hooks - pull-based architecture (AugmentPlayer.ApplySharedBrew)
    }
}
