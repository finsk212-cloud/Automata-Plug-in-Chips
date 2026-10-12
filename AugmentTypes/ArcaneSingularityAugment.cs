using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class ArcaneSingularityAugment : Augment
    {
        public override string Id => "arcane_singularity";
        public override string DisplayName => "Arcane Singularity";
        public override string Description =>
            $"Magic hits build up to {AugmentText.Trigger("8 charges")}. At full charge, your next magic hit opens a vortex " +
            $"for {AugmentText.Duration("3s")}, pulling enemies within 300 pixels toward its core and dealing " +
            $"{AugmentText.BonusDamage("20 magic damage")} every {AugmentText.Duration("0.5s")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;

        private const int HitsToCharge = 8;
        private const int VortexDamage = 20;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Vortex damage", VortexDamage) };
        }


        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Magic)
                RegisterMagicHit(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Magic)
                RegisterMagicHit(player, target);
        }

        private void RegisterMagicHit(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.ArcaneSingularityCharge >= HitsToCharge)
            {
                ap.ArcaneSingularityCharge = 0;
                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<ArcaneSingularityVortexProjectile>(),
                    AugmentScaling.Scale(VortexDamage),
                    0f,
                    player.whoAmI
                );
                return;
            }

            ap.ArcaneSingularityCharge += HitEffectiveness;
        }
    }
}
