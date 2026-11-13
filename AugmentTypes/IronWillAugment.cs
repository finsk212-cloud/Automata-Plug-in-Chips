using Terraria;

namespace Augments
{
    public class IronWillAugment : Augment
    {
        public override string Id => "iron_will";
        public override string DisplayName => "Iron Will";
        public override string Description =>
            $"After you're hit, gain {AugmentText.Defense("+8 defense")} for {AugmentText.Duration("3 seconds")}. " +
            $"{AugmentText.Cooldown("30 seconds cooldown")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int DurationTicks = 180;
        private const int CooldownTicks = 1800;
        private const int DefenseBonus = 8;

        public override int CooldownRemaining => LocalPlayerState.IronWillCooldown;

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.IronWillCooldown > 0)
                return;

            ap.IronWillDurationRemaining = DurationTicks;
            ap.IronWillCooldown = CooldownTicks;
        }

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.IronWillCooldown > 0)
                ap.IronWillCooldown--;

            if (ap.IronWillDurationRemaining > 0)
                ap.IronWillDurationRemaining--;
        }

        public override void UpdateEquips(Player player)
        {
            if (player.GetModPlayer<AugmentPlayer>().IronWillDurationRemaining > 0)
                player.statDefense += DefenseBonus;
        }
    }
}
