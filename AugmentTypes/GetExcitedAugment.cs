using Terraria;

namespace Augments
{
    public class GetExcitedAugment : Augment
    {
        public override string Id => "get_excited";
        public override string DisplayName => "Get Excited!";
        public override string Description =>
            $"After killing an enemy, gain {AugmentText.MovementSpeed("+8% movement speed")} for {AugmentText.Duration("2 seconds")}, " +
            $"stacking up to {AugmentText.MovementSpeed("+24% movement speed")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int DurationTicks = 120;
        private const int MaxStacks = 3;
        private const float MovementSpeedPerStack = 0.08f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (target.life <= 0 && PassesHitEffectivenessRoll())
                TriggerEffect(player);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (target.life <= 0 && PassesHitEffectivenessRoll())
                TriggerEffect(player);
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.GetExcitedTimer <= 0)
                return;

            ap.GetExcitedTimer--;

            if (ap.GetExcitedTimer == 0)
                ap.GetExcitedStacks = 0;
        }

        public override void PostUpdateRunSpeeds(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.GetExcitedTimer <= 0)
                return;

            float bonus = MovementSpeedPerStack * ap.GetExcitedStacks;
            player.maxRunSpeed *= 1f + bonus;
            player.accRunSpeed *= 1f + bonus;
            player.runAcceleration *= 1f + bonus;
        }

        private static void TriggerEffect(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.GetExcitedStacks < MaxStacks)
                ap.GetExcitedStacks++;

            ap.GetExcitedTimer = DurationTicks;
        }
    }
}
