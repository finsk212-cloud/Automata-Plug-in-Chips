using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class WhiplashWaveAugment : Augment
    {
        public override string Id => "whiplash_wave";
        public override string DisplayName => "Whiplash Wave";
        public override string Description =>
            $"Whip swings also fire a wave for {AugmentText.BonusDamage("40% of the whip's damage")}. The wave pierces up to 3 enemies and flies far beyond the whip's reach. Wave hits trigger {AugmentText.OnHit("on-hit")} effects at 50% effectiveness.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(The wave flies about 30 tiles toward your cursor and disappears when it hits a wall. It does not apply Whip Cracker stacks or Focus Directive tags, and it does not inject Micro-Nanites.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        private const float DamageMultiplier = 0.4f;
        private const float WaveSpeed = 16f;
        private const float SpawnOffset = 24f;

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (projectile.owner != Main.myPlayer || projectile.DamageType != DamageClass.SummonMeleeSpeed)
                return;

            Vector2 direction = projectile.velocity;
            if (direction.LengthSquared() < 0.01f)
                direction = new Vector2(player.direction, 0f);
            direction.Normalize();

            int damage = Math.Max(1, (int)(projectile.damage * DamageMultiplier));

            int index = Projectile.NewProjectile(
                player.GetSource_FromThis(),
                player.MountedCenter + direction * SpawnOffset,
                direction * WaveSpeed,
                ModContent.ProjectileType<WhiplashWaveProjectile>(),
                damage,
                projectile.knockBack * 0.5f,
                player.whoAmI
            );

            if (index < 0 || index >= Main.maxProjectiles)
                return;

            Projectile wave = Main.projectile[index];
            wave.originalDamage = damage;
            wave.CritChance = projectile.CritChance;
            wave.ArmorPenetration = projectile.ArmorPenetration;
        }
    }
}
