using Terraria;

namespace Augments
{
    public class BrewersInstinctAugment : Augment
    {
        public override string Id => "brewers_instinct";
        public override string DisplayName => "Brewer's Instinct";
        public override string Description =>
            $"Buff potions have a {FormatCurrentChance(ProcChance)} to last {AugmentText.Duration("50% longer")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Buff potions have a {FormatFortuneChance(ProcChance, player)} to last {AugmentText.Duration("50% longer")}.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float ProcChance = 0.4f;
        private const float DurationMultiplier = 1.5f;

        // item.buffType > 0 is exactly how vanilla itself marks a "buff
        // potion" item (Swiftness, Ironskin, Regeneration, etc) - healing
        // potions use Item.healLife instead and never set buffType, so they
        // naturally fall outside this check with no special-casing needed,
        // matching the description's claim about healing potions.
        // By the time GlobalItem.OnConsumeItem fires, the buff has already
        // been added to the player's buff array for this same use -
        // FindBuffIndex is checked defensively rather than assumed, so this
        // safely no-ops if that ordering is ever wrong.
        public override void OnConsumeItem(Player player, Item item)
        {
            if (item.buffType <= 0)
                return;

            if (!RollFortuneChance(player, ProcChance))
                return;

            int buffIndex = player.FindBuffIndex(item.buffType);
            if (buffIndex < 0)
                return;

            player.buffTime[buffIndex] = (int)(player.buffTime[buffIndex] * DurationMultiplier);
        }
    }
}
