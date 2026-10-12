using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class BloodletterAugment : Augment
    {
        public override string Id => "bloodletter";
        public override string DisplayName => "Bloodletter";
        public override string Description =>
            $"Melee {AugmentText.Crit("crits")} make enemies {AugmentText.Bleed("bleed")}, dealing {AugmentText.BonusDamage("3 damage per second")} for {AugmentText.Duration("5s")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Melee;
        public override string FamilyId => AugmentFamilyRegistry.BloodhunterId;

        private const int BleedDurationTicks = 300;
        private const int DamagePerSecond = 3;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Bleed per second", DamagePerSecond) };
        }


        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && item.CountsAsClass(DamageClass.Melee))
                ApplyBleed(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && proj.CountsAsClass(DamageClass.Melee))
                ApplyBleed(player, target);
        }

        private static void ApplyBleed(Player player, NPC target)
        {
            int dps = AugmentScaling.Scale(DamagePerSecond);
            if (player != null && AugmentFamilyRegistry.GetOwnedCount(player.GetModPlayer<AugmentPlayer>(), AugmentFamilyRegistry.BloodhunterId) >= 2)
            {
                dps = AugmentScaling.Scale(5);
            }

            target.GetGlobalNPC<AugmentBleedNPC>().ApplyBleed(BleedDurationTicks, dps);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                AugmentNet.SendApplyNPCEffectBleed(target.whoAmI, BleedDurationTicks, dps);
        }
    }
}
