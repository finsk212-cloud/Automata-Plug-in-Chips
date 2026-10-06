using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // GlobalItem.GrabRange runs as the very last step of Player.GetItemGrabRange,
    // after vanilla's own goldRing/lifeMagnet/manaMagnet bonuses are already added
    // in - so adding to grabRange here genuinely stacks with potions/accessories,
    // unlike setting those shared boolean flags directly (which just gets OR'd,
    // not summed, with whatever else already set them true that tick).
    public class TreasureHunterGrabRange : GlobalItem
    {
        public override void GrabRange(Item item, Player player, ref int grabRange)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();

            if (ap.HasAugment("treasure_hunter"))
            {
                if (item.IsACoin)
                    grabRange += TreasureHunterAugment.CoinGrabRangeBonus;

                if (item.type == ItemID.Heart || item.type == ItemID.CandyApple || item.type == ItemID.CandyCane)
                    grabRange += TreasureHunterAugment.HeartGrabRangeBonus;

                if (item.type == ItemID.Star || item.type == ItemID.SoulCake || item.type == ItemID.SugarPlum)
                    grabRange += TreasureHunterAugment.ManaStarGrabRangeBonus;
            }

            if (ap.HasAugment("celestial_pull"))
            {
                if (item.type == ItemID.Star || item.type == ItemID.SoulCake || item.type == ItemID.SugarPlum)
                    grabRange += CelestialPullAugment.StarGrabRangeBonus;
            }
        }
    }
}
