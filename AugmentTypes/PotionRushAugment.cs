using Terraria;

namespace Augments
{
    public class PotionRushAugment : Augment
    {
        public override string Id => "potion_rush";
        public override string DisplayName => "Potion Rush";
        public override string Description =>
            $"After using a healing potion, gain {AugmentText.MovementSpeed("+10% movement speed")} for {AugmentText.Duration("4 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int DurationTicks = 240;

        public override void OnConsumeItem(Player player, Item item)
        {
            if (item.healLife > 0)
            {
                player.GetModPlayer<AugmentPlayer>().PotionRushTimer = DurationTicks;
            }
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.PotionRushTimer <= 0)
                return;

            ap.PotionRushTimer--;
        }

        public override void PostUpdateRunSpeeds(Player player)
        {
            if (player.GetModPlayer<AugmentPlayer>().PotionRushTimer <= 0)
                return;

            player.maxRunSpeed *= 1.10f;
            player.accRunSpeed *= 1.10f;
            player.runAcceleration *= 1.10f;
        }
    }
}
