using System;
using Terraria;

namespace Augments
{
    public class SecondWindAugment : Augment
    {
        public override string Id => "second_wind";
        public override string DisplayName => "Second Wind";
        public override string Description =>
            $"When you fall below {AugmentText.HP("25% HP")}, {AugmentText.Healing("heal 40 HP")}. " +
            $"{AugmentText.Cooldown("60 seconds cooldown")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => "field_medic";

        private const int CooldownTicks = 3600;
        private const float TriggerThreshold = 0.25f;
        private const int HealAmount = 40;

        public override int CooldownRemaining => LocalPlayerState.SecondWindCooldown;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.SecondWindCooldown > 0)
            {
                ap.SecondWindCooldown--;
                return;
            }

            if (player.statLife > 0 && player.statLife < player.statLifeMax2 * TriggerThreshold)
            {
                player.statLife = Math.Min(player.statLife + HealAmount, player.statLifeMax2);
                player.HealEffect(HealAmount);
                ap.SecondWindCooldown = CooldownTicks;
            }
        }
    }
}
