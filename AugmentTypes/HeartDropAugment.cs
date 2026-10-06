using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class HeartDropAugment : Augment
    {
        public override string Id => "heart_drop";
        public override string DisplayName => "Heart Drop";
        public override string Description =>
            $"While {AugmentText.HP("missing HP")}, enemy kills have {AugmentText.Healing("+" + FormatCurrentChance(ExtraHeartDropChance) + " heart drop chance")}.\n" +
            AugmentText.Note("Vanilla Terraria chance: 8.33%. This augment is additive.");

        public override string GetDetailedDescription(Player player) =>
            $"While {AugmentText.HP("missing HP")}, enemy kills have {AugmentText.Healing("+" + FormatFortuneChance(ExtraHeartDropChance, player) + " heart drop chance")}.\n" +
            AugmentText.Note("Vanilla Terraria chance: 8.33%. This augment is additive.");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float ExtraHeartDropChance = 0.10f;

        public override void OnKillNPC(Player player, NPC npc)
        {
            TryDropHeart(player, npc, HitEffectiveness);
        }

        private static void TryDropHeart(Player player, NPC target, float effectiveness)
        {
            if (target.friendly || player.statLife >= player.statLifeMax2 ||
                !player.GetModPlayer<AugmentPlayer>().HasAugment("heart_drop"))
                return;

            if (RollFortuneChance(player, ExtraHeartDropChance * effectiveness))
            {
                int index = Item.NewItem(target.GetSource_Loot(), target.Hitbox, ItemID.Heart);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncItem, number: index);
            }
        }
    }
}
