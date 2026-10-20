using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments.Tiles
{
    // The storage behind a placed Banner Station: its tier and the banners inside.
    // Banners are kept as item types; the banner id is looked up when the buff is applied.
    public class BannerStationEntity : ModTileEntity
    {
        public int Tier;

        // False until the item's stored contents (or an upgrade) have been written in.
        public bool Initialized;

        public readonly List<int> Banners = new List<int>();

        public int GetTier() => Initialized ? Math.Clamp(Tier, 0, BannerStationData.TierCount - 1) : TierFromFrame();

        public int SlotCount => BannerStationData.SlotsByTier[GetTier()];

        public Vector2 Center => CenterOf(this);

        public static Vector2 CenterOf(BannerStationEntity te) => new Vector2(te.Position.X * 16f + 16f, te.Position.Y * 16f + 24f);

        // Before the station is initialised the tile's own frame says which tier it is.
        private int TierFromFrame()
        {
            if (!WorldGen.InWorld(Position.X, Position.Y, 2))
                return 0;

            Tile tile = Main.tile[Position.X, Position.Y];
            if (!tile.HasTile || tile.TileType != ModContent.TileType<BannerStationTile>())
                return 0;

            return Math.Clamp(tile.TileFrameX / 36, 0, BannerStationData.TierCount - 1);
        }

        public static bool TryGetAt(int left, int top, out BannerStationEntity station)
        {
            if (TileEntity.ByPosition.TryGetValue(new Point16(left, top), out TileEntity entity) && entity is BannerStationEntity found)
            {
                station = found;
                return true;
            }

            station = null;
            return false;
        }

        public bool CanAdd(int itemType) =>
            BannerStationData.IsBannerItem(itemType) && !Banners.Contains(itemType) && Banners.Count < SlotCount;

        // Writes the contents of a freshly placed station, dropping anything invalid or over capacity.
        public void ApplyInit(int tier, List<int> banners)
        {
            Tier = Math.Clamp(tier, 0, BannerStationData.TierCount - 1);
            Initialized = true;
            Banners.Clear();

            foreach (int type in banners)
            {
                if (Banners.Count >= SlotCount)
                    break;
                if (CanAdd(type))
                    Banners.Add(type);
            }
        }

        public bool InRange(Player player)
        {
            int tier = GetTier();
            Vector2 center = Center;
            return Math.Abs(player.Center.X - center.X) <= BannerStationData.RangeTilesX(tier) * 16f
                && Math.Abs(player.Center.Y - center.Y) <= BannerStationData.RangeTilesY(tier) * 16f;
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<BannerStationTile>();
        }

        public void WriteState(BinaryWriter writer)
        {
            writer.Write((byte)Math.Clamp(Tier, 0, BannerStationData.TierCount - 1));
            writer.Write(Initialized);
            writer.Write((short)Banners.Count);
            foreach (int type in Banners)
                writer.Write(type);
        }

        public void ReadState(BinaryReader reader)
        {
            Tier = reader.ReadByte();
            Initialized = reader.ReadBoolean();
            int count = reader.ReadInt16();
            Banners.Clear();
            for (int i = 0; i < count; i++)
                Banners.Add(reader.ReadInt32());
        }

        public override void NetSend(BinaryWriter writer) => WriteState(writer);

        public override void NetReceive(BinaryReader reader) => ReadState(reader);

        public override void SaveData(TagCompound tag)
        {
            tag["tier"] = Tier;
            tag["banners"] = BannerStationData.Encode(Banners);
        }

        public override void LoadData(TagCompound tag)
        {
            Tier = Math.Clamp(tag.GetInt("tier"), 0, BannerStationData.TierCount - 1);
            Initialized = true;
            Banners.Clear();
            Banners.AddRange(BannerStationData.Decode(tag.GetString("banners")));
        }
    }
}
