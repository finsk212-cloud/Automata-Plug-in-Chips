using Augments.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Items
{
    // Right-click a placed Banner Station while holding one of these to raise it to the next tier. Contents stay put.
    public abstract class BannerStationKitItem : ModItem
    {
        // The tier the station becomes (1 = Mk II, 2 = Mk III, 3 = Mk IV).
        public abstract int TargetTier { get; }

        protected virtual int Rarity => ItemRarityID.LightRed;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9;
            Item.rare = Rarity;
            Item.value = Item.buyPrice(gold: 3 * TargetTier);
        }
    }

    public class BannerStationKitMk2Item : BannerStationKitItem
    {
        public override int TargetTier => 1;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 12)
                .AddRecipeGroup(BannerStationData.AnyBannerGroup, 10)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class BannerStationKitMk3Item : BannerStationKitItem
    {
        public override int TargetTier => 2;
        protected override int Rarity => ItemRarityID.Lime;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.ChlorophyteBar, 14)
                .AddRecipeGroup(BannerStationData.AnyBannerGroup, 15)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    public class BannerStationKitMk4Item : BannerStationKitItem
    {
        public override int TargetTier => 3;
        protected override int Rarity => ItemRarityID.Cyan;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 16)
                .AddRecipeGroup(BannerStationData.AnyBannerGroup, 20)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
