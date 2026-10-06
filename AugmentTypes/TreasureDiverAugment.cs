using Terraria;
using Terraria.ID;

namespace Augments
{
    public class TreasureDiverAugment : Augment
    {
        public override string Id => "treasure_diver";
        public override string DisplayName => "Treasure Diver";
        public override string Description =>
            $"While submerged, {AugmentText.Defense("breath capacity is increased")} and kills have a {FormatCurrentChance(DropChance)} to drop bonus coins.";

        public override string GetDetailedDescription(Player player) =>
            $"While submerged, {AugmentText.Defense("breath capacity is increased")} and kills have a {FormatFortuneChance(DropChance, player)} to drop bonus coins.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int BreathBonus = 120;
        private const float DropChance = 0.3f;
        private const int MinCoins = 1;
        private const int MaxCoins = 3;

        // player.breathMax resets to its base value every frame before
        // equips/buffs re-add their bonuses, same as every other persistent
        // stat bonus in this mod (maxMinions, statDefense, etc) - re-applied
        // here every tick instead of once. Gated on player.wet (true while
        // touching any liquid - water, honey, or lava), the standard
        // "currently submerged" check.
        public override void OnUpdate(Player player)
        {
            if (player.wet)
                player.breathMax += BreathBonus;
        }

        // Fires on kill credit (see AugmentGlobalNPC.OnKill, keyed off
        // npc.lastInteraction) - same shared kill-detection system Swarm
        // Tactics/Plague Bearer use, so DoT-finished kills count too.
        // Reuses Lucky Find's coin-spawning mechanism (Item.NewItem with
        // ItemID.SilverCoin), but intentionally does NOT set
        // AugmentBonusCoinItem.IsLuckyFindBonus - that flag specifically
        // credits Lucky Find's own running coin counter, and these coins
        // aren't from Lucky Find.
        public override void OnKillNPC(Player player, NPC npc)
        {
            if (!player.wet)
                return;

            if (!RollFortuneChance(player, DropChance))
                return;

            int coinCount = Main.rand.Next(MinCoins, MaxCoins + 1);
            int index = Item.NewItem(npc.GetSource_Loot(), npc.Hitbox, ItemID.SilverCoin, coinCount);
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendData(MessageID.SyncItem, number: index);
        }
    }
}
