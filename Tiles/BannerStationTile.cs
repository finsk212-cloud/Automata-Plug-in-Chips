using Augments.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Augments.Tiles
{
    // The placed Banner Station: 2 wide, 3 tall, one tier per style column in the sheet.
    public class BannerStationTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.Origin = new Point16(0, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, 2, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance<BannerStationEntity>().Generic_HookPostPlaceMyPlayer;
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(70, 120, 150));
            DustType = DustID.Iron;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        // Any tile of the 2x3 body -> the top-left tile, which is where the entity lives.
        public static Point16 TopLeft(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            return new Point16(i - tile.TileFrameX % 36 / 18, j - tile.TileFrameY / 18);
        }

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            Point16 origin = TopLeft(i, j);
            int tier = BannerStationEntity.TryGetAt(origin.X, origin.Y, out BannerStationEntity station) ? station.GetTier() : 0;

            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = BannerStationData.StationItemType(tier);
        }

        public override bool RightClick(int i, int j)
        {
            Player player = Main.LocalPlayer;
            Point16 origin = TopLeft(i, j);

            if (!BannerStationEntity.TryGetAt(origin.X, origin.Y, out BannerStationEntity station))
                return false;

            if (player.HeldItem.ModItem is BannerStationKitItem kit)
            {
                BannerStationNet.RequestUpgrade(station, kit, player);
                return true;
            }

            BannerStationNet.ToggleUI(origin.X, origin.Y);
            return true;
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            int tier = frameX / 36;
            var banners = new System.Collections.Generic.List<int>();

            if (BannerStationEntity.TryGetAt(i, j, out BannerStationEntity station))
            {
                tier = station.GetTier();
                banners.AddRange(station.Banners);
            }

            // The server (or a singleplayer game) spawns the drop; clients would only duplicate it.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                BannerStationItem.SpawnDrop(i, j, tier, banners);

            ModContent.GetInstance<BannerStationEntity>().Kill(i, j);
        }
    }
}
