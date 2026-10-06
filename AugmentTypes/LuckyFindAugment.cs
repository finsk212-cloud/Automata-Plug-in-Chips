using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class LuckyFindAugment : Augment
    {
        public override string Id => "lucky_find";
        public override string DisplayName => "Lucky Find";
        public override string Description =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Defeated enemies have a {FormatCurrentChance(DropChance)} " +
            $"to drop extra coins.\n" +
            AugmentText.Note($"(Total gained: {FormatCoins(LocalPlayerState?.LuckyFindCopperGained ?? 0)})");

        public override string GetDetailedDescription(Player player) =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Defeated enemies have a {FormatFortuneChance(DropChance, player)} " +
            $"to drop extra coins.\n" +
            AugmentText.Note($"(Total gained: {FormatCoins(player?.GetModPlayer<AugmentPlayer>()?.LuckyFindCopperGained ?? 0)})");

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.FortuneId;

        public override bool IsLuckyThemed => true;
        public override float FortuneBonus => 0.05f;

        private const float DropChance = 0.25f;
        private const int MinCoins = 1;
        private const int MaxCoins = 3;

        internal const int CopperPerSilver = 100;
        private const int CopperPerGold = CopperPerSilver * 100;
        private const int CopperPerPlatinum = CopperPerGold * 100;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (target.life <= 0)
                HandleKill(player, target.Center, HitEffectiveness);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (target.life <= 0)
                HandleKill(player, target.Center, HitEffectiveness);
        }

        private void HandleKill(Player player, Vector2 position, float effectiveness)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                AugmentNet.SendLuckyFindDropRequest(player, position, effectiveness);
                return;
            }

            TryDropCoinsServer(player, position, effectiveness);
        }

        internal static void TryDropCoinsServer(Player player, Vector2 position, float effectiveness)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var augmentPlayer = player.GetModPlayer<AugmentPlayer>();
            if (!augmentPlayer.HasAugment("lucky_find"))
                return;

            float chance = DropChance * (1f + augmentPlayer.TotalFortune) * effectiveness;
            if (Main.rand.NextFloat() >= chance)
                return;

            int coinCount = Main.rand.Next(MinCoins, MaxCoins + 1);
            Rectangle dropRect = new Rectangle((int)position.X, (int)position.Y, 16, 16);
            int index = Item.NewItem(player.GetSource_FromThis(), dropRect, ItemID.SilverCoin, coinCount);

            var bonusData = Main.item[index].GetGlobalItem<AugmentBonusCoinItem>();
            bonusData.IsLuckyFindBonus = true;
            bonusData.BonusCoinValue = (long)coinCount * CopperPerSilver;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncItem, number: index);
        }

        internal static string FormatCoins(long copper)
        {
            long platinum = copper / CopperPerPlatinum;
            long gold = copper % CopperPerPlatinum / CopperPerGold;
            long silver = copper % CopperPerGold / CopperPerSilver;

            if (platinum > 0)
                return $"{platinum} Platinum {gold} Gold";
            if (gold > 0)
                return $"{gold} Gold {silver} Silver";
            if (silver > 0)
                return $"{silver} Silver";

            return $"{copper} Copper";
        }
    }
}
