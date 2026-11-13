namespace Augments
{
    public class TreasureHunterAugment : Augment
    {
        public override string Id => "treasure_hunter";
        public override string DisplayName => "Treasure Hunter";
        public override string Description =>
            $"Increases {AugmentText.Trigger("pickup range")} for coins, hearts, and mana stars.";

        public override string GetDetailedDescription(Terraria.Player player) =>
            Description + "\n" +
            AugmentText.Note("(Stacks additively with potions and accessories.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        // The actual range bonus is applied in TreasureHunterGrabRange (a GlobalItem),
        // since GlobalItem.GrabRange runs after vanilla's own grab-range bonuses and
        // is the only hook that adds on top of them instead of overwriting a shared
        // flag - see TreasureHunterGrabRange.cs for the implementation.
        public const int CoinGrabRangeBonus = 350;
        public const int HeartGrabRangeBonus = 250;
        public const int ManaStarGrabRangeBonus = 300;
    }
}
