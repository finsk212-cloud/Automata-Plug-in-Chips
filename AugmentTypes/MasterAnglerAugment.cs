using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace Augments
{
    public class MasterAnglerAugment : Augment
    {
        public override string Id => "master_angler";
        public override string DisplayName => "Master Angler";
        public override string Description =>
            $"Grants {AugmentText.BonusDamage("+10 fishing power")} and opening crates has a {FormatCurrentChance(CrateBonusChance)} for bonus loot.";

        public override string GetDetailedDescription(Player player) =>
            $"Grants {AugmentText.BonusDamage("+10 fishing power")} and opening crates has a {FormatFortuneChance(CrateBonusChance, player)} for bonus loot.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        private const int FishingLevelBonus = 10;
        private const float CrateBonusChance = 0.2f;
        private const int MinBonusCoins = 1;
        private const int MaxBonusCoins = 3;

        // Adding to player.fishingSkill in UpdateEquips directly increases the player's
        // base fishing power, which is read by Fisherman's Pocket Guide (Player.GetFishingConditions)
        // and automatically applied to all fishing catch calculations.
        public override void UpdateEquips(Player player)
        {
            player.fishingSkill += FishingLevelBonus;
        }

        public override void RightClickItem(Player player, Item item)
        {
            CheckCrateBonus(player, item);
        }

        public override void OnConsumeItem(Player player, Item item)
        {
            CheckCrateBonus(player, item);
        }

        private void CheckCrateBonus(Player player, Item item)
        {
            bool isCrate = ItemID.Sets.IsFishingCrate[item.type] || ItemID.Sets.IsFishingCrateHardmode[item.type];
            if (!isCrate)
                return;

            if (!RollFortuneChance(player, CrateBonusChance))
                return;

            int coinCount = Main.rand.Next(MinBonusCoins, MaxBonusCoins + 1);
            player.QuickSpawnItem(player.GetSource_FromThis(), ItemID.GoldCoin, coinCount);
        }
    }
}
