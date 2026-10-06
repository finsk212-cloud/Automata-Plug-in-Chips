using Terraria;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class LuckyStrikeAugment : Augment
    {
        public override string Id => "lucky_strike";
        public override string DisplayName => "Lucky Strike";
        public override string Description =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Any {AugmentText.Crit("crit")} has a {FormatCurrentChance(ProcChance)} " +
            $"to deal a second strike for the same {AugmentText.BonusDamage("damage")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Grants {AugmentText.Crit("+5% Fortune")} (World Luck & lucky trigger chance). Any {AugmentText.Crit("crit")} has a {FormatFortuneChance(ProcChance, player)} " +
            $"to deal a second strike for the same {AugmentText.BonusDamage("damage")}.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;
        public override string FamilyId => AugmentFamilyRegistry.FortuneId;

        public override bool IsLuckyThemed => true;
        public override float FortuneBonus => 0.05f;

        private const float ProcChance = 0.15f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && Main.rand.NextFloat() < ProcChance * (1f + player.GetModPlayer<AugmentPlayer>().TotalFortune))
            {
                int dmg = ScaleHitEffect(hit.Damage);
                target.SimpleStrikeNPC(dmg, player.direction);
                if (player.whoAmI == Main.myPlayer)
                    AugmentDamageTracker.RecordChipHit("lucky_strike", dmg, false);
            }
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && Main.rand.NextFloat() < ProcChance * (1f + player.GetModPlayer<AugmentPlayer>().TotalFortune))
            {
                int dmg = ScaleHitEffect(hit.Damage);
                target.SimpleStrikeNPC(dmg, player.direction);
                if (player.whoAmI == Main.myPlayer)
                    AugmentDamageTracker.RecordChipHit("lucky_strike", dmg, false);
            }
        }
    }
}
