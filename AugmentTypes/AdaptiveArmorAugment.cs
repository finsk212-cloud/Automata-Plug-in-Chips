using Microsoft.Xna.Framework;
using Terraria;
using Augments.Core;

namespace Augments
{
    public class AdaptiveArmorAugment : Augment
    {
        public override string Id => "adaptive_armor";
        public override string DisplayName => "Adaptive Armor";
        public override string Description =>
            $"Defense grows by {AugmentText.Defense("+1")} every {AugmentText.Duration("2 seconds")} you go without taking damage, " +
            $"up to {AugmentText.Defense("+10")} after {AugmentText.Duration("20 seconds")}. Resets to zero the instant you're hit.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.BastionId;

        private const int TicksPerStack = 120;
        private const int MaxBonus = 10;

        public override int? StatusValue => LocalPlayerState.AdaptiveArmorDefenseBonus > 0 ? LocalPlayerState.AdaptiveArmorDefenseBonus : (int?)null;
        public override Color StatusValueColor => AugmentTextColors.Defense;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.AdaptiveArmorDefenseBonus >= MaxBonus)
                return;

            ap.AdaptiveArmorUndamagedTimer++;
            if (ap.AdaptiveArmorUndamagedTimer >= TicksPerStack)
            {
                ap.AdaptiveArmorUndamagedTimer = 0;
                ap.AdaptiveArmorDefenseBonus++;
            }
        }

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.AdaptiveArmorUndamagedTimer = 0;
            ap.AdaptiveArmorDefenseBonus = 0;
        }

        public override void UpdateEquips(Player player)
        {
            player.statDefense += player.GetModPlayer<AugmentPlayer>().AdaptiveArmorDefenseBonus;
        }
    }
}
