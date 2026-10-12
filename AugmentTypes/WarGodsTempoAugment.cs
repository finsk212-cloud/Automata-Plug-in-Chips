using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class WarGodsTempoAugment : Augment
    {
        public override string Id => "war_gods_tempo";
        public override string DisplayName => "War God's Tempo";
        public override string Description =>
            $"Melee hits build Tempo stacks. At {AugmentText.Trigger("8 stacks")}, your next melee hit releases a shockwave dealing {AugmentText.BonusDamage("90 damage")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;

        private const int MaxTempo = 8;
        private const int ResetDelayTicks = 300;
        private const int ShockwaveDamage = 90;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Shockwave damage", ShockwaveDamage) };
        }


        public override void OnHitNPCWithItem(
            Player player,
            Item item,
            NPC target,
            NPC.HitInfo hit,
            AugmentHitSource source,
            float effectiveness)
        {
            if (source == AugmentHitSource.NormalAttack && item.CountsAsClass(DamageClass.Melee))
                RegisterMeleeHit(player, target);
        }

        public override void OnHitNPCWithProj(
            Player player,
            Projectile proj,
            NPC target,
            NPC.HitInfo hit,
            AugmentHitSource source,
            float effectiveness)
        {
            if (source == AugmentHitSource.NormalAttack && proj.CountsAsClass(DamageClass.Melee))
                RegisterMeleeHit(player, target);
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.WarGodsTempoResetTimer <= 0)
                return;

            ap.WarGodsTempoResetTimer--;
            if (ap.WarGodsTempoResetTimer == 0)
                ap.WarGodsTempoStacks = 0;
        }

        private static void RegisterMeleeHit(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.WarGodsTempoResetTimer = ResetDelayTicks;

            if (ap.WarGodsTempoStacks >= MaxTempo)
            {
                ap.WarGodsTempoStacks = 0;
                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<WarGodShockwaveProjectile>(),
                    AugmentScaling.Scale(ShockwaveDamage),
                    0f,
                    player.whoAmI
                );
                return;
            }

            ap.WarGodsTempoStacks++;
        }
    }
}
