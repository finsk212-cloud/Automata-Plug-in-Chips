using Terraria;
using Augments.Core;

namespace Augments
{
    public class QuickfireAugment : Augment
    {
        public override string Id => "quickfire";
        public override string DisplayName => "Quickfire";
        public override string Description =>
            $"Ranged weapons have a {FormatCurrentChance(ProcChance)} to not consume ammo.";

        public override string GetDetailedDescription(Player player) =>
            $"Ranged weapons have a {FormatFortuneChance(ProcChance, player)} to not consume ammo.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override string FamilyId => AugmentFamilyRegistry.GunslingerId;

        private const float ProcChance = 0.25f;

        public override bool CanConsumeAmmo(Player player, Item weapon, Item ammo)
        {
            if (RollFortuneChance(player, ProcChance))
                return false;

            return true;
        }
    }
}
