using System;
using System.Collections.Generic;
using Augments.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Tiles
{
    // Shared numbers and banner lookups for the Banner Station family.
    public static class BannerStationData
    {
        public const int TierCount = 4;
        public const string AnyBannerGroup = "Augments:AnyBanner";

        public static readonly int[] SlotsByTier = { 10, 25, 50, 100 };
        public static readonly float[] RangeScaleByTier = { 1f, 1.25f, 1.5f, 2f };
        public static readonly string[] TierNames = { "Mk I", "Mk II", "Mk III", "Mk IV" };

        // Mk I covers the area vanilla banners cover; every tier above widens it.
        private const int BaseRangeTilesX = 84;
        private const int BaseRangeTilesY = 62;

        private static Dictionary<int, int> bannerIdByItem;

        public static int RangeTilesX(int tier) => (int)Math.Round(BaseRangeTilesX * RangeScaleByTier[Math.Clamp(tier, 0, TierCount - 1)]);

        public static int RangeTilesY(int tier) => (int)Math.Round(BaseRangeTilesY * RangeScaleByTier[Math.Clamp(tier, 0, TierCount - 1)]);

        public static int StationItemType(int tier)
        {
            switch (Math.Clamp(tier, 0, TierCount - 1))
            {
                case 1: return ModContent.ItemType<BannerStationMk2Item>();
                case 2: return ModContent.ItemType<BannerStationMk3Item>();
                case 3: return ModContent.ItemType<BannerStationMk4Item>();
                default: return ModContent.ItemType<BannerStationMk1Item>();
            }
        }

        // Banner item type -> the banner id vanilla uses for NPCBannerBuff.
        // Built from the NPCs themselves with the same lookups vanilla's own banner buff uses, so modded banners (Calamity included) are covered.
        public static Dictionary<int, int> BannerIdByItem
        {
            get
            {
                if (bannerIdByItem == null)
                    Build();
                return bannerIdByItem;
            }
        }

        public static void Reset() => bannerIdByItem = null;

        public static bool IsBannerItem(int itemType) => itemType > 0 && BannerIdByItem.ContainsKey(itemType);

        private static void Build()
        {
            var map = new Dictionary<int, int>();

            foreach (NPC sample in ContentSamples.NpcsByNetId.Values)
            {
                try
                {
                    int banner = Item.NPCtoBanner(sample.netID);
                    int item = banner > 0 ? Item.BannerToItem(banner) : 0;

                    if ((banner <= 0 || item <= 0) && sample.ModNPC != null && sample.ModNPC.Banner > 0 && sample.ModNPC.BannerItem > 0)
                    {
                        banner = sample.ModNPC.Banner;
                        item = sample.ModNPC.BannerItem;
                    }

                    if (banner > 0 && item > 0 && item < ItemLoader.ItemCount)
                        map[item] = banner;
                }
                catch (Exception)
                {
                    // A single odd NPC must not break the whole table.
                }
            }

            bannerIdByItem = map;
        }

        // Banners are saved by name so they survive other mods changing the item order.
        public static string Encode(List<int> itemTypes)
        {
            var names = new List<string>();
            foreach (int type in itemTypes)
            {
                if (type > 0 && type < ItemLoader.ItemCount)
                    names.Add(ItemID.Search.GetName(type));
            }
            return string.Join(";", names);
        }

        public static List<int> Decode(string data)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(data))
                return result;

            foreach (string name in data.Split(';'))
            {
                if (name.Length > 0 && ItemID.Search.TryGetId(name, out int type) && !result.Contains(type))
                    result.Add(type);
            }
            return result;
        }
    }
}
