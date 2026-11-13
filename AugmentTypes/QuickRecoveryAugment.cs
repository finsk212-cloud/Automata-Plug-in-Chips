using System;
using Terraria;

namespace Augments
{
    public class QuickRecoveryAugment : Augment
    {
        public override string Id => "quick_recovery";
        public override string DisplayName => "Quick Recovery";
        public override string Description =>
            $"After taking damage, {AugmentText.Healing("regenerate 6 HP")} over {AugmentText.Duration("3 seconds")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int RegenDurationTicks = 180;
        private const int HealIntervalTicks = 60;
        private const int HealAmount = 2;

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            player.GetModPlayer<AugmentPlayer>().QuickRecoveryRegenTimer = RegenDurationTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.QuickRecoveryRegenTimer <= 0)
                return;

            ap.QuickRecoveryRegenTimer--;

            if (ap.QuickRecoveryRegenTimer % HealIntervalTicks == 0)
            {
                player.statLife = Math.Min(player.statLife + HealAmount, player.statLifeMax2);
                player.HealEffect(HealAmount);
            }
        }
    }
}
