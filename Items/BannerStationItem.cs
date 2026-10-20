using System.Collections.Generic;
using System.IO;
using Augments.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments.Items
{
    // The carried form of a Banner Station. It remembers the banners inside, so a station can be broken and placed again anywhere.
    public abstract class BannerStationItem : ModItem
    {
        public abstract int Tier { get; }

        protected virtual int Rarity => ItemRarityID.Green;

        protected virtual int Value => Item.buyPrice(gold: 1);

        // Banner item names, joined with ';' (see BannerStationData.Encode).
        public string Data = "";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 48;
            Item.maxStack = 1;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.rare = Rarity;
            Item.value = Value;
            Item.createTile = ModContent.TileType<BannerStationTile>();
            Item.placeStyle = Tier;
        }

        // Placement happens through the tile, so the contents are remembered while the item is in hand.
        public override void HoldItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                BannerStationPlayer.RememberHeld(Tier, Data);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            int stored = BannerStationData.Decode(Data).Count;
            tooltips.Add(new TooltipLine(Mod, "BannerStationContents", $"Holds {stored}/{BannerStationData.SlotsByTier[Tier]} banners"));
        }

        public override void SaveData(TagCompound tag)
        {
            if (!string.IsNullOrEmpty(Data))
                tag["banners"] = Data;
        }

        public override void LoadData(TagCompound tag)
        {
            Data = tag.GetString("banners") ?? "";
        }

        public override void NetSend(BinaryWriter writer) => writer.Write(Data ?? "");

        public override void NetReceive(BinaryReader reader) => Data = reader.ReadString();

        // Spawns a station item carrying the given banners at a tile position (server or singleplayer only).
        public static void SpawnDrop(int tileX, int tileY, int tier, List<int> banners)
        {
            int type = BannerStationData.StationItemType(tier);
            int index = Item.NewItem(new Terraria.DataStructures.EntitySource_TileBreak(tileX, tileY), tileX * 16, tileY * 16, 32, 48, type, 1, true);
            if (index < 0 || index >= Main.maxItems)
                return;

            if (Main.item[index].ModItem is BannerStationItem station)
                station.Data = BannerStationData.Encode(banners);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index, 1f);
        }
    }

    public class BannerStationMk1Item : BannerStationItem
    {
        public override int Tier => 0;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 15)
                .AddRecipeGroup(RecipeGroupID.IronBar, 8)
                .AddRecipeGroup(BannerStationData.AnyBannerGroup, 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    public class BannerStationMk2Item : BannerStationItem
    {
        public override int Tier => 1;
        protected override int Rarity => ItemRarityID.LightRed;
        protected override int Value => Item.buyPrice(gold: 5);
    }

    public class BannerStationMk3Item : BannerStationItem
    {
        public override int Tier => 2;
        protected override int Rarity => ItemRarityID.Lime;
        protected override int Value => Item.buyPrice(gold: 15);
    }

    public class BannerStationMk4Item : BannerStationItem
    {
        public override int Tier => 3;
        protected override int Rarity => ItemRarityID.Cyan;
        protected override int Value => Item.buyPrice(gold: 40);
    }
}
