using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Augments.Core;

namespace Augments
{
    // Epic, Universal. Every tombstone near the player adds a little damage to everything they hit.
    // Plugin id stays neutral ("tombstone_boost") because ids are saved with characters.
    public class GraveDangerAugment : Augment
    {
        public override string Id => "tombstone_boost";
        public override string DisplayName => "Grave Danger";
        public override string Description =>
            $"Every {AugmentText.Trigger("tombstone")} within {AugmentText.Duration("30 tiles")} of you gives {AugmentText.BonusDamage("+3% damage")} " +
            $"to all your attacks, up to {AugmentText.BonusDamage("+30% damage")} from 10 tombstones.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Any tombstone counts: the ones left when a player dies and the ones you place yourself. Each one counts once.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float DamagePerGrave = 0.03f;
        private const int MaxGraves = 10;
        private const int RadiusTiles = 30;
        private const int RefreshTicks = 30; // how often the nearby graves are counted again

        // Counted on the attacker's own game (world tiles are synced, so every client sees the same graves).
        private static readonly int[] graveCount = new int[Main.maxPlayers + 1];
        private static readonly uint[] countedAt = new uint[Main.maxPlayers + 1];

        // How many tombstones are near this player right now (recounted every half second at most).
        public static int GetGraveCount(Player player)
        {
            int who = player.whoAmI;
            if (who < 0 || who >= graveCount.Length)
                return 0;

            if (Main.GameUpdateCount - countedAt[who] >= RefreshTicks || countedAt[who] > Main.GameUpdateCount)
            {
                graveCount[who] = CountGraves(player);
                countedAt[who] = Main.GameUpdateCount;
            }

            return graveCount[who];
        }

        private static int CountGraves(Player player)
        {
            Point center = player.Center.ToTileCoordinates();
            int minX = System.Math.Max(1, center.X - RadiusTiles);
            int maxX = System.Math.Min(Main.maxTilesX - 2, center.X + RadiusTiles);
            int minY = System.Math.Max(1, center.Y - RadiusTiles);
            int maxY = System.Math.Min(Main.maxTilesY - 2, center.Y + RadiusTiles);

            int count = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.TileType != TileID.Tombstones)
                        continue;

                    // A tombstone is several tiles; count it once, at its top-left tile.
                    TileObjectData data = TileObjectData.GetTileData(tile);
                    int fullWidth = data != null ? data.CoordinateFullWidth : 36;
                    int fullHeight = data != null ? data.CoordinateFullHeight : 36;
                    if (tile.TileFrameX % fullWidth == 0 && tile.TileFrameY % fullHeight == 0)
                        count++;
                }
            }

            return count;
        }

        private static float Bonus(Player player) => DamagePerGrave * System.Math.Min(GetGraveCount(player), MaxGraves);

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage += Bonus(player);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage += Bonus(player);
        }

        // Shows the number of graves currently counted, next to the plugin's other status icons.
        public override int? StatusValue
        {
            get
            {
                Player p = LocalPlayerState.Player;
                if (p == null || !p.active)
                    return null;

                int graves = System.Math.Min(GetGraveCount(p), MaxGraves);
                return graves > 0 ? graves : null;
            }
        }
    }
}
