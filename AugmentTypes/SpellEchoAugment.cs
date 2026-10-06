using Terraria;
using Augments.Core;

namespace Augments
{
    public class SpellEchoAugment : Augment
    {
        public override string Id => "spell_echo";
        public override string DisplayName => "Spell Echo";
        public override string Description =>
            $"{FormatCurrentChance(ProcChance)} per cast to not consume any mana.";

        public override string GetDetailedDescription(Player player) =>
            $"{FormatFortuneChance(ProcChance, player)} per cast to not consume any mana.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Magic;
        public override string FamilyId => AugmentFamilyRegistry.ArcaneSurgeId;

        private const float ProcChance = 0.25f;

        public override void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult)
        {
            if (RollFortuneChance(player, ProcChance))
                mult = 0f;
        }
    }
}
