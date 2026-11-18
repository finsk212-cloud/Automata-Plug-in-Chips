using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Common, Universal. Every ore you mine has a 30% chance (fortune-scaled, like the other chance plugins) to drop a
    // second copy. "Ore" means the same thing as for the Ore Sniffer and Vein Miner Pod upgrades (PodOreSniffer.IsOre),
    // so gems are not ores, and the tiles Vein Miner breaks for you count too.
    // The drop is made where the game makes drops: the server in multiplayer (so the extra item is a normal world item
    // everyone sees), the game itself in singleplayer.
    // Plugin id stays neutral ("ore_bonus") because ids are saved with characters.
    public class OreThanEnoughAugment : Augment
    {
        public const string ChipId = "ore_bonus";
        public const float DoubleChance = 0.30f;

        public override string Id => ChipId;
        public override string DisplayName => "Ore Than Enough";
        public override string Description =>
            $"Every ore you mine has a {FormatCurrentChance(DoubleChance)} to drop {AugmentText.BonusDamage("twice")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Every ore you mine has a {FormatFortuneChance(DoubleChance, player)} to drop {AugmentText.BonusDamage("twice")}.\n" +
            AugmentText.Note("(Ores that Upgrade Pod: Vein Miner breaks for you count too. Gems are not ores. You need a pickaxe in your hand.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
    }

    // Spawns the extra ore when a mined ore drops its item.
    internal sealed class OreThanEnoughTile : GlobalTile
    {
        // The miner has to be this close to the ore (in pixels), which keeps a far-away player from getting credit.
        private const float MaxMinerDistance = 700f;

        public override void Drop(int i, int j, int type)
        {
            // Drops are made by the server (multiplayer) or the game itself (singleplayer), never by a client.
            if (Main.netMode == NetmodeID.MultiplayerClient || !PodOreSniffer.IsOre(type))
                return;

            // Tile breaks do not say who caused them, so the closest player is taken as the miner.
            Player miner = WorldGen.GetPlayerForTile(i, j);
            if (miner == null || !miner.active || miner.dead)
                return;
            if (!miner.GetModPlayer<AugmentPlayer>().HasAugment(OreThanEnoughAugment.ChipId))
                return;
            if (miner.HeldItem == null || miner.HeldItem.pick <= 0)
                return;
            if (Vector2.DistanceSquared(miner.Center, new Vector2(i * 16 + 8, j * 16 + 8)) > MaxMinerDistance * MaxMinerDistance)
                return;

            if (!Augment.RollFortuneChance(miner, OreThanEnoughAugment.DoubleChance))
                return;

            // The same item the ore tile normally gives.
            int itemType = TileLoader.GetItemDropFromTypeAndStyle(type, 0);
            if (itemType <= 0)
                return;

            int index = Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 16, 16, itemType, 1);
            if (index >= 0 && index < Main.maxItems)
                Main.item[index].TryCombiningIntoNearbyItems(index);
        }
    }
}
