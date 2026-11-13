using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class MomentumSwingAugment : Augment
    {
        public override string Id => "momentum_swing";
        public override string DisplayName => "Momentum Swing";
        public override string Description =>
            $"Hitting the same enemy repeatedly within {AugmentText.Duration("1.5 seconds")} builds " +
            $"{AugmentText.BonusDamage("+3% bonus damage")} per stack, up to " +
            $"{AugmentText.BonusDamage("+15% bonus damage")} at 5 stacks. Switching targets or waiting too long resets the stacks.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;
        public override string FamilyId => AugmentFamilyRegistry.KineticId;

        private const int ResetWindowTicks = 90;
        private const int MaxStacks = 5;
        private const float BonusPerStackPercent = 0.03f;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.MomentumSwingResetTimer <= 0)
                return;

            ap.MomentumSwingResetTimer--;
            if (ap.MomentumSwingResetTimer == 0)
            {
                ap.MomentumSwingStacks = 0;
                ap.MomentumSwingLastTargetWhoAmI = -1;
            }
        }

        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (item.CountsAsClass(DamageClass.Melee))
                ApplyStack(player, ref modifiers, target, item.damage);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (proj.CountsAsClass(DamageClass.Melee))
                ApplyStack(player, ref modifiers, target, proj.damage);
        }

        private static void ApplyStack(Player player, ref NPC.HitModifiers modifiers, NPC target, int baseDamage)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (target.whoAmI == ap.MomentumSwingLastTargetWhoAmI && ap.MomentumSwingResetTimer > 0)
            {
                if (ap.MomentumSwingStacks < MaxStacks)
                    ap.MomentumSwingStacks++;
            }
            else
            {
                ap.MomentumSwingStacks = 1;
                ap.MomentumSwingLastTargetWhoAmI = target.whoAmI;
            }

            ap.MomentumSwingResetTimer = ResetWindowTicks;

            float bonusPercent = ap.MomentumSwingStacks * BonusPerStackPercent;
            modifiers.FlatBonusDamage += (int)(baseDamage * bonusPercent);
        }
    }
}
