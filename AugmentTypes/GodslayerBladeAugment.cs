using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class GodslayerBladeAugment : Augment
    {
        public override string Id => "godslayer_blade";
        public override string DisplayName => "Godslayer Blade";
        public override string Description =>
            $"Melee {AugmentText.Crit("crits")} summon a falling sword that deals {AugmentText.BonusDamage("75% of base weapon damage")} to the target and nearby enemies. " +
            $"Enemies struck are inflicted with {AugmentText.Ichor("Ichor")} for 2 seconds. " +
            $"{AugmentText.Cooldown("2 seconds cooldown")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;
        public override int CooldownRemaining => LocalPlayerState.GodslayerBladeCooldown;

        private const int CooldownTicks = 120;
        private const float SwordDamageMultiplier = 0.75f;
        private const float SpawnHeight = 600f;
        private const float FallSpeed = 18f;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.GodslayerBladeCooldown > 0)
                ap.GodslayerBladeCooldown--;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && item.CountsAsClass(DamageClass.Melee))
                TrySummonSword(player, target, item.damage);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && proj.CountsAsClass(DamageClass.Melee))
                TrySummonSword(player, target, proj.originalDamage);
        }

        private void TrySummonSword(Player player, NPC target, int baseDamage)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.GodslayerBladeCooldown > 0)
                return;

            ap.GodslayerBladeCooldown = CooldownTicks;
            Vector2 spawnPosition = target.Center - new Vector2(0f, SpawnHeight);
            int swordDamage = Math.Max(1, (int)(baseDamage * SwordDamageMultiplier));

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                spawnPosition,
                new Vector2(0f, FallSpeed),
                ModContent.ProjectileType<GodslayerSwordProjectile>(),
                swordDamage,
                0f,
                player.whoAmI,
                target.whoAmI
            );
        }
    }
}
