using System;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class IronRhythmAugment : Augment
    {
        public override string Id => "iron_rhythm";
        public override string DisplayName => "Iron Rhythm";
        public override string Description =>
            $"Every 4th attack applies {AugmentText.SpecialDamage("15 damage")} {AugmentText.OnHit("on-hit")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int HitsRequired = 4;
        private const int BonusDamage = 15;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Bonus damage", BonusDamage) };
        }


        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            TryApplyBonus(player, ref modifiers, 1f);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            TryApplyBonus(player, ref modifiers, 1f);
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
        {
            TryApplyBonus(player, ref modifiers, effectiveness);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            TryShowSpecialDamageText(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            TryShowSpecialDamageText(player, target);
        }

        private static void TryApplyBonus(Player player, ref NPC.HitModifiers modifiers, float effectiveness)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.IronRhythmHitCounter += effectiveness;

            if (ap.IronRhythmHitCounter >= HitsRequired)
            {
                ap.IronRhythmHitCounter -= HitsRequired;
                ap.IronRhythmPendingSpecialDamage = Math.Max(1, (int)(AugmentScaling.Scale(BonusDamage) * effectiveness));
                modifiers.FlatBonusDamage += ap.IronRhythmPendingSpecialDamage;
                ap.RecordOnHitDamage(ap.IronRhythmPendingSpecialDamage);
            }
        }

        private static void TryShowSpecialDamageText(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.IronRhythmPendingSpecialDamage <= 0)
                return;

            CombatText.NewText(
                target.Hitbox,
                AugmentTextColors.SpecialDamage,
                $"{ap.IronRhythmPendingSpecialDamage}",
                true
            );

            ap.IronRhythmPendingSpecialDamage = 0;
        }
    }
}
