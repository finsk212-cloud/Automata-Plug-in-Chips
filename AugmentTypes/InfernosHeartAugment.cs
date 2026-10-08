using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class InfernosHeartAugment : Augment
    {
        public override string Id => "infernos_heart";
        public override string DisplayName => "Inferno's Heart";
        public override string Description =>
            $"8 consecutive magic hits within {AugmentText.Duration("0.5 seconds")} of each other charge a burst; " +
            $"the next hit after reaching full charge releases an AoE explosion dealing " +
            $"{AugmentText.BonusDamage("100 damage")} to every nearby enemy, then the charge resets.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Magic;

        private const int MaxChargeStacks = 8;
        private const int ResetWindowTicks = 30;
        private const float BurstRange = 200f;
        private const int BurstDamage = 100;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Magic)
                HandleHit(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Magic)
                HandleHit(player, target);
        }

        // Same "next hit after reaching the cap triggers, then resets" shape
        // as ApexHunterAugment - check the threshold from BEFORE this hit
        // first, so the hit that brings the count to 8 just finishes
        // charging, and it's the hit after that releases the burst.
        private static void HandleHit(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            bool triggers = ap.InfernosHeartChargeStacks >= MaxChargeStacks;

            if (triggers)
            {
                ap.InfernosHeartChargeStacks = 0;
            }
            else if (ap.InfernosHeartResetTimer > 0)
            {
                if (ap.InfernosHeartChargeStacks < MaxChargeStacks)
                    ap.InfernosHeartChargeStacks++;
            }
            else
            {
                ap.InfernosHeartChargeStacks = 1;
            }

            ap.InfernosHeartResetTimer = ResetWindowTicks;

            if (triggers)
                Burst(player, target);
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.InfernosHeartResetTimer <= 0)
                return;

            ap.InfernosHeartResetTimer--;
            if (ap.InfernosHeartResetTimer == 0)
                ap.InfernosHeartChargeStacks = 0;
        }

        // Uncapped nearby-enemy search - same shape as
        // TimeWarpAugment.SlowNearbyTargets, just dealing damage instead of
        // applying a slow.
        private static void Burst(Player player, NPC origin)
        {
            foreach (NPC npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.townNPC)
                    continue;

                if (npc.Distance(origin.Center) <= BurstRange)
                {
                    int direction = npc.Center.X >= origin.Center.X ? 1 : -1;
                    npc.SimpleStrikeNPC(BurstDamage, direction);
                    if (player.whoAmI == Main.myPlayer)
                        AugmentDamageTracker.RecordChipHit("infernos_heart", BurstDamage, false);
                }
            }
        }
    }
}
