using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Items
{
    public class MediGunMK3Item : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 56;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.noMelee = true;
            Item.channel = true;
            Item.damage = 0;
            Item.knockBack = 0f;
            Item.DamageType = DamageClass.Generic;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<Projectiles.MediGunMK3BeamProjectile>();
            Item.shootSpeed = 1f;
            Item.autoReuse = false;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
        {
            if (Main.playerInventory)
                return false;

            // If tethered to an ally at 100% Overclock, right-click triggers Overclock instead of Siphon Beam
            if (player.altFunctionUse == 2)
            {
                var mediPlayer = player.GetModPlayer<MediGunPlayer>();
                if (mediPlayer.OverclockCharge >= 100f && (mediPlayer.CurrentPatientWhoAmI >= 0 || mediPlayer.CurrentTargetNPCWhoAmI >= 0))
                {
                    return false;
                }
            }

            int siphonType = ModContent.ProjectileType<Projectiles.MediGunSiphonBeamProjectile>();
            int mk1Type = ModContent.ProjectileType<Projectiles.MediGunBeamProjectile>();
            int mk2Type = ModContent.ProjectileType<Projectiles.MediGunMK2BeamProjectile>();
            int mk3Type = ModContent.ProjectileType<Projectiles.MediGunMK3BeamProjectile>();
            int mk4Type = ModContent.ProjectileType<Projectiles.MediGunMK4BeamProjectile>();

            return player.ownedProjectileCounts[siphonType] <= 0
                && player.ownedProjectileCounts[mk1Type] <= 0
                && player.ownedProjectileCounts[mk2Type] <= 0
                && player.ownedProjectileCounts[mk3Type] <= 0
                && player.ownedProjectileCounts[mk4Type] <= 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            KillActiveBeams(player);

            if (player.altFunctionUse == 2)
            {
                int siphonType = ModContent.ProjectileType<Projectiles.MediGunSiphonBeamProjectile>();
                Projectile.NewProjectile(source, position, velocity, siphonType, damage, knockback, player.whoAmI, ai0: -1, ai1: 3);
                return false;
            }
            return true;
        }

        private static void KillActiveBeams(Player player)
        {
            int siphonType = ModContent.ProjectileType<Projectiles.MediGunSiphonBeamProjectile>();
            int mk1Type = ModContent.ProjectileType<Projectiles.MediGunBeamProjectile>();
            int mk2Type = ModContent.ProjectileType<Projectiles.MediGunMK2BeamProjectile>();
            int mk3Type = ModContent.ProjectileType<Projectiles.MediGunMK3BeamProjectile>();
            int mk4Type = ModContent.ProjectileType<Projectiles.MediGunMK4BeamProjectile>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && (p.type == siphonType || p.type == mk1Type || p.type == mk2Type || p.type == mk3Type || p.type == mk4Type))
                {
                    p.Kill();
                }
            }
        }

        public override void AddRecipes()
        {
            // Spectre Bar recipe
            Recipe recipeSpectre = CreateRecipe();
            recipeSpectre.AddIngredient(ModContent.ItemType<MediGunMK2Item>(), 1);
            recipeSpectre.AddIngredient(ItemID.SpectreBar, 10);
            recipeSpectre.AddIngredient(ItemID.Ectoplasm, 5);
            recipeSpectre.AddIngredient(ItemID.LifeFruit, 2);
            recipeSpectre.AddTile(TileID.MythrilAnvil);
            recipeSpectre.Register();

            // Chlorophyte Bar recipe (alternative)
            Recipe recipeChlorophyte = CreateRecipe();
            recipeChlorophyte.AddIngredient(ModContent.ItemType<MediGunMK2Item>(), 1);
            recipeChlorophyte.AddIngredient(ItemID.ChlorophyteBar, 10);
            recipeChlorophyte.AddIngredient(ItemID.Ectoplasm, 5);
            recipeChlorophyte.AddIngredient(ItemID.LifeFruit, 2);
            recipeChlorophyte.AddTile(TileID.MythrilAnvil);
            recipeChlorophyte.Register();
        }
    }
}
