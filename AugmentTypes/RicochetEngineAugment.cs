using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class RicochetEngineAugment : Augment
    {
        public override string Id => "ricochet_engine";
        public override string DisplayName => "Ricochet Engine";
        public override string Description =>
            $"Ranged hits have a {FormatCurrentChance(ProcChance)} to spawn a ricochet shot that bounces between nearby enemies. " +
            $"Ricochet hits trigger {AugmentText.OnHit("on-hit")} effects at 50% effectiveness.";

        public override string GetDetailedDescription(Player player) =>
            $"Ranged hits have a {FormatFortuneChance(ProcChance, player)} to spawn a ricochet shot that bounces between nearby enemies. " +
            $"Ricochet hits trigger {AugmentText.OnHit("on-hit")} effects at 50% effectiveness.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const float ProcChance = 0.4f;
        private const float DamageMultiplier = 0.35f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Ranged)
                TrySpawnRicochet(player, target, hit.Damage);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType != DamageClass.Ranged)
                return;

            if (proj.GetGlobalProjectile<AugmentProjectileTag>().PreventRicochetEngineCopy)
                return;

            TrySpawnRicochet(player, target, hit.Damage);
        }

        private void TrySpawnRicochet(Player player, NPC target, int hitDamage)
        {
            if (!RollFortuneChance(player, ProcChance * HitEffectiveness))
                return;

            int damage = Math.Max(1, (int)(hitDamage * DamageMultiplier));
            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<RicochetEngineProjectile>(),
                damage,
                0f,
                player.whoAmI,
                target.whoAmI
            );
        }
    }
}
