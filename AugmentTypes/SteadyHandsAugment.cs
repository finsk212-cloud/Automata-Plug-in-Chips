using Terraria;
using Terraria.ModLoader;

namespace Augments
{
    public class SteadyHandsAugment : Augment
    {
        public override string Id => "steady_hands";
        public override string DisplayName => "Steady Hands";
        public override string Description =>
            $"Standing still continuously builds ranged {AugmentText.Crit("crit chance")}, up to {AugmentText.Crit("+20%")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Ramps over a few seconds; resets instantly upon moving.)");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const int TicksPerPercent = 6;
        private const float MaxCritBonus = 20f;

        public override bool IsCharging => LocalPlayerState.SteadyHandsCurrentCritBonus > 0;
        public override int ChargeIndicatorPercent => (int)LocalPlayerState.SteadyHandsCurrentCritBonus;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            bool isStill = player.velocity.LengthSquared() < 0.25f;

            if (!isStill)
            {
                ap.SteadyHandsCurrentCritBonus = 0f;
                ap.SteadyHandsRampTicks = 0;
                return;
            }

            if (ap.SteadyHandsCurrentCritBonus >= MaxCritBonus)
                return;

            ap.SteadyHandsRampTicks++;
            if (ap.SteadyHandsRampTicks >= TicksPerPercent)
            {
                ap.SteadyHandsRampTicks = 0;
                ap.SteadyHandsCurrentCritBonus = System.Math.Min(ap.SteadyHandsCurrentCritBonus + 1f, MaxCritBonus);
            }
        }

        public override void ModifyWeaponCrit(Player player, Item item, ref float crit)
        {
            if (item.DamageType == DamageClass.Ranged)
                crit += player.GetModPlayer<AugmentPlayer>().SteadyHandsCurrentCritBonus;
        }
    }
}
