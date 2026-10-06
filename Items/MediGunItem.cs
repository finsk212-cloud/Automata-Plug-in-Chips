using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Items
{
    public class MediGunItem : ModItem
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
            Item.value = Item.buyPrice(gold: 1, silver: 50);
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<Projectiles.MediGunBeamProjectile>();
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
                Projectile.NewProjectile(source, position, velocity, siphonType, damage, knockback, player.whoAmI, ai0: -1, ai1: 1);
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
            // Gold Bar recipe
            Recipe recipeGold = CreateRecipe();
            recipeGold.AddIngredient(ItemID.LifeCrystal, 1);
            recipeGold.AddIngredient(ItemID.GoldBar, 10);
            recipeGold.AddIngredient(ItemID.FallenStar, 5);
            recipeGold.AddTile(TileID.Anvils);
            recipeGold.Register();

            // Platinum Bar recipe
            Recipe recipePlatinum = CreateRecipe();
            recipePlatinum.AddIngredient(ItemID.LifeCrystal, 1);
            recipePlatinum.AddIngredient(ItemID.PlatinumBar, 10);
            recipePlatinum.AddIngredient(ItemID.FallenStar, 5);
            recipePlatinum.AddTile(TileID.Anvils);
            recipePlatinum.Register();
        }
    }
}
