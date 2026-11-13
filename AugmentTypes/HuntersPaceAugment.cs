using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class HuntersPaceAugment : Augment
    {
        public override string Id => "hunters_pace";
        public override string DisplayName => "Hunter's Pace";
        public override string Description =>
            $"Ranged hits give {AugmentText.MovementSpeed("+15% movement speed")} for {AugmentText.Duration("2 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const int SpeedDurationTicks = 120;
        private const float SpeedBonus = 0.15f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Ranged)
                RefreshSpeedBonus(player);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Ranged)
                RefreshSpeedBonus(player);
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.HuntersPaceSpeedTimer <= 0)
                return;

            ap.HuntersPaceSpeedTimer--;
            if (ap.HuntersPaceSpeedTimer == 0)
                ap.HuntersPaceCurrentBonus = 0f;
        }

        public override void PostUpdateRunSpeeds(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.HuntersPaceSpeedTimer <= 0)
                return;

            player.maxRunSpeed *= 1f + ap.HuntersPaceCurrentBonus;
            player.accRunSpeed *= 1f + ap.HuntersPaceCurrentBonus;
            player.runAcceleration *= 1f + ap.HuntersPaceCurrentBonus;
        }

        private void RefreshSpeedBonus(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.HuntersPaceSpeedTimer = SpeedDurationTicks;
            ap.HuntersPaceCurrentBonus = SpeedBonus * HitEffectiveness;
        }
    }
}
