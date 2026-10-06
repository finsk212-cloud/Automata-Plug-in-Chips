using Terraria;

namespace Augments
{
    public class CelestialPullAugment : Augment
    {
        public override string Id => "celestial_pull";
        public override string DisplayName => "Celestial Pull";
        public override string Description =>
            $"Increases pickup range for Mana Stars by {AugmentText.Duration("15 tiles")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Magic;

        // 15 tiles * 16 px/tile = 240 px
        public const int StarGrabRangeBonus = 240;
    }
}
