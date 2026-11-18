using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Common, Universal. Chopping a tree with an axe:
    //   1. Each wood item the tree drops has a 30% chance (fortune-scaled) to drop a second copy.
    //   2. Each chopped log has a 10% chance to drop a little extra woodland loot: acorns, coins, fruit, or rarely a gem.
    // Both are done where the game makes drops: the server in multiplayer, the game itself in singleplayer, so the
    // extra items are normal world items everyone sees. Tile breaks do not say who caused them, so the closest player
    // holding an axe is taken as the chopper.
    // Plugin id stays neutral ("tree_bonus") because ids are saved with characters.
    public class LumberjackAugment : Augment
    {
        public const string ChipId = "tree_bonus";
        public const float DoubleChance = 0.30f;
        public const float LootChance = 0.10f;

        public override string Id => ChipId;
        public override string DisplayName => "Lumberjack";
        public override string Description =>
            $"Wood from the trees you chop has a {FormatCurrentChance(DoubleChance)} to drop {AugmentText.BonusDamage("twice")}. " +
            "Trees might be hiding a little something for you.";

        public override string GetDetailedDescription(Player player) =>
            $"Wood from the trees you chop has a {FormatFortuneChance(DoubleChance, player)} to drop {AugmentText.BonusDamage("twice")}. " +
            "Trees might be hiding a little something for you.\n" +
            $"Each chopped log has a {FormatFortuneChance(LootChance, player)} to hide a surprise.\n" +
            AugmentText.Note("(You need an axe in your hand. What falls out is a surprise. Wood blocks you broke yourself do not count.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        // The chopper may be this far from the log (in pixels). A tall tree's top is well within it.
        private const float MaxChopperDistance = 900f;

        private static readonly int[] Fruits =
        {
            ItemID.Apple, ItemID.Banana, ItemID.Cherry, ItemID.Lemon, ItemID.Peach,
            ItemID.Pomegranate, ItemID.Grapefruit, ItemID.Plum, ItemID.Mango, ItemID.Pineapple
        };

        private static readonly int[] Gems =
        {
            ItemID.Amethyst, ItemID.Topaz, ItemID.Sapphire, ItemID.Emerald, ItemID.Ruby, ItemID.Diamond
        };

        // Items that count as wood, including modded woods added to the game's wood recipe group.
        internal static bool IsWood(int itemType)
        {
            switch (itemType)
            {
                case ItemID.Wood:
                case ItemID.BorealWood:
                case ItemID.RichMahogany:
                case ItemID.Ebonwood:
                case ItemID.Shadewood:
                case ItemID.PalmWood:
                case ItemID.Pearlwood:
                case ItemID.SpookyWood:
                case ItemID.AshWood:
                    return true;
            }

            return RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out RecipeGroup group) && group.ContainsItem(itemType);
        }

        // The player who is chopping at this tile: the closest one with this plugin and an axe in hand.
        internal static Player FindChopper(int tileX, int tileY)
        {
            Player player = WorldGen.GetPlayerForTile(tileX, tileY);
            if (player == null || !player.active || player.dead)
                return null;
            if (!player.GetModPlayer<AugmentPlayer>().HasAugment(ChipId))
                return null;
            if (player.HeldItem == null || player.HeldItem.axe <= 0)
                return null;
            if (Vector2.DistanceSquared(player.Center, new Vector2(tileX * 16 + 8, tileY * 16 + 8)) > MaxChopperDistance * MaxChopperDistance)
                return null;

            return player;
        }

        internal static bool IsTreeTrunk(int tileX, int tileY)
        {
            if (!WorldGen.InWorld(tileX, tileY, 2))
                return false;

            Tile tile = Main.tile[tileX, tileY];
            return tile.HasTile && tile.TileType < TileID.Sets.IsATreeTrunk.Length && TileID.Sets.IsATreeTrunk[tile.TileType];
        }

        private static void Give(int tileX, int tileY, int itemType, int stack)
        {
            if (itemType <= 0 || stack <= 0)
                return;

            int index = Item.NewItem(new EntitySource_TileBreak(tileX, tileY), tileX * 16, tileY * 16, 16, 16, itemType, stack);
            if (index >= 0 && index < Main.maxItems)
                Main.item[index].TryCombiningIntoNearbyItems(index);
        }

        // One small surprise from a chopped log: mostly acorns and coins, sometimes fruit, rarely a gem.
        internal static void SpawnLoot(int tileX, int tileY)
        {
            float roll = Main.rand.NextFloat();
            if (roll < 0.40f)
            {
                Give(tileX, tileY, ItemID.Acorn, Main.rand.Next(1, 4));
            }
            else if (roll < 0.70f)
            {
                // A little money that grows with game progression, handed out as silver and copper coins.
                int copper = AugmentScaling.Scale(40);
                Give(tileX, tileY, ItemID.SilverCoin, copper / 100);
                Give(tileX, tileY, ItemID.CopperCoin, copper % 100);
            }
            else if (roll < 0.92f)
            {
                Give(tileX, tileY, Fruits[Main.rand.Next(Fruits.Length)], 1);
            }
            else
            {
                Give(tileX, tileY, Gems[Main.rand.Next(Gems.Length)], 1);
            }
        }
    }

    // The extra wood: when a tree drops a wood item, it sometimes drops a second one.
    internal sealed class LumberjackItem : GlobalItem
    {
        // True while the bonus wood is being spawned, so that item is not doubled again.
        private static bool spawningBonus;

        public override void OnSpawn(Item item, IEntitySource source)
        {
            // Drops are made by the server (multiplayer) or the game itself (singleplayer), never by a client.
            if (spawningBonus || Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (!(source is EntitySource_TileBreak tileBreak) || !LumberjackAugment.IsWood(item.type))
                return;

            // Only wood that came out of a tree, not blocks of wood someone placed and broke.
            Point at = tileBreak.TileCoords;
            if (!LumberjackAugment.IsTreeTrunk(at.X, at.Y))
                return;

            Player chopper = LumberjackAugment.FindChopper(at.X, at.Y);
            if (chopper == null || !Augment.RollFortuneChance(chopper, LumberjackAugment.DoubleChance))
                return;

            spawningBonus = true;
            try
            {
                int index = Item.NewItem(source, at.X * 16, at.Y * 16, 16, 16, item.type, item.stack);
                if (index >= 0 && index < Main.maxItems)
                    Main.item[index].TryCombiningIntoNearbyItems(index);
            }
            finally
            {
                spawningBonus = false;
            }
        }
    }

    // The extra loot: each chopped log sometimes gives a small surprise.
    internal sealed class LumberjackTile : GlobalTile
    {
        public override void Drop(int i, int j, int type)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || type < 0 || type >= TileID.Sets.IsATreeTrunk.Length || !TileID.Sets.IsATreeTrunk[type])
                return;

            Player chopper = LumberjackAugment.FindChopper(i, j);
            if (chopper == null || !Augment.RollFortuneChance(chopper, LumberjackAugment.LootChance))
                return;

            LumberjackAugment.SpawnLoot(i, j);
        }
    }
}
