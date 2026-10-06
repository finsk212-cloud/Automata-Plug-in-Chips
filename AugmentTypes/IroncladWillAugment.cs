using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class IroncladWillAugment : Augment
    {
        public override string Id => "ironclad_will";
        public override string DisplayName => "Ironclad Will";
        public override string Description =>
            $"Melee hits have a {FormatCurrentChance(ProcChance)} to {AugmentText.BonusDamage("ignore 100% of enemy defense")}.\n" +
            AugmentText.Note("(Deals full unmitigated damage for that hit.)");

        public override string GetDetailedDescription(Player player) =>
            $"Melee hits have a {FormatFortuneChance(ProcChance, player)} to {AugmentText.BonusDamage("ignore 100% of enemy defense")}.\n" +
            AugmentText.Note("(Deals full unmitigated damage for that hit.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Melee;

        private const float ProcChance = 0.2f;

        // ScalingArmorPenetration is a fraction of the target's defense to
        // ignore - at 1f, the hit completely ignores all of it. This is a
        // genuinely different mechanism from Ichor (which lowers Defense
        // itself, a reduction other hits also benefit from) - this only
        // bypasses defense for the one hit that procs it.
        public override void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (item.CountsAsClass(DamageClass.Melee) && RollFortuneChance(player, ProcChance))
                modifiers.ScalingArmorPenetration += 1f;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (proj.CountsAsClass(DamageClass.Melee) && RollFortuneChance(player, ProcChance))
                modifiers.ScalingArmorPenetration += 1f;
        }
    }
}
