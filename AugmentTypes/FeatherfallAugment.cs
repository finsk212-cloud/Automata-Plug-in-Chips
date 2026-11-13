using Terraria;

namespace Augments
{
    public class FeatherfallAugment : Augment
    {
        public override string Id => "featherfall";
        public override string DisplayName => "Featherfall";
        public override string Description =>
            $"Fall damage is reduced by 50%. Landing from a significant fall grants " +
            $"{AugmentText.MovementSpeed("+30% movement speed")} for {AugmentText.Duration("2 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float FallDamageReductionPercent = 0.5f;
        private const float FallDistanceThreshold = 60f; // roughly 4 tiles
        private const int SpeedBurstDurationTicks = 120;
        private const float SpeedBurstBonus = 0.3f;

        // No dedicated "this damage was fall damage" flag exists anywhere -
        // confirmed by inspecting both PlayerDeathReason and HurtModifiers
        // directly. PlayerDeathReason only exposes specific Player/NPC/
        // Projectile/Item source indices (all default -1/null when unset),
        // with no fall-specific factory method alongside ByNPC/ByProjectile/
        // ByOther/etc. This is the closest verifiable substitute: no NPC,
        // projectile, or other player dealt the hit, AND player.fallStart
        // (the same field Cloud in a Bottle/Lucky Horseshoe read to negate
        // fall damage) shows a significant unresolved fall. Not an absolute
        // guarantee - an untracked/environmental hit landing the exact same
        // tick as a big fall could theoretically also pass this check - but
        // it's the best signal available without a dedicated flag.
        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            var source = modifiers.DamageSource;
            bool noEntitySource = source.SourceNPCIndex < 0 && source.SourceProjectileLocalIndex < 0 && !modifiers.PvP;
            float fallDistance = player.position.Y - player.fallStart;

            if (!noEntitySource || fallDistance < FallDistanceThreshold)
                return;

            modifiers.FinalDamage *= 1f - FallDamageReductionPercent;
            player.GetModPlayer<AugmentPlayer>().FeatherfallSpeedBurstTicks = SpeedBurstDurationTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.FeatherfallSpeedBurstTicks > 0)
                ap.FeatherfallSpeedBurstTicks--;
        }

        // Same PostUpdateRunSpeeds pattern Fight or Flight already uses -
        // maxRunSpeed/accRunSpeed/runAcceleration get recalculated from
        // scratch every frame and have to be multiplied here to actually stick.
        public override void PostUpdateRunSpeeds(Player player)
        {
            if (player.GetModPlayer<AugmentPlayer>().FeatherfallSpeedBurstTicks <= 0)
                return;

            player.maxRunSpeed *= 1f + SpeedBurstBonus;
            player.accRunSpeed *= 1f + SpeedBurstBonus;
            player.runAcceleration *= 1f + SpeedBurstBonus;
        }
    }
}
