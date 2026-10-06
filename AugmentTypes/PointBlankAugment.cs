using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class PointBlankAugment : Augment
    {
        public override string Id          => "point_blank";
        public override string DisplayName => "Point Blank";
        public override string Description =>
            $"Ranged hits deal {AugmentText.BonusDamage("+50% bonus damage")} " +
            $"when within {AugmentText.Duration("8 tiles")} of the target.";

        public override string GetDetailedDescription(Player player) =>
            $"Ranged hits deal {AugmentText.BonusDamage("+50% bonus damage")} " +
            $"when within {AugmentText.Duration("8 tiles (128 px)")} of target.\n" +
            $"[c/94A3B8:(Bonus = Base Hit Damage × 0.50, dealt as a secondary strike with DPS tracking)]";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass  Class  => AugmentClass.Ranged;

        // 8 tiles x 16 px/tile = 128 px
        private const float RangePixels = 128f;
        private const float DamageBonus = 0.50f;

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!proj.CountsAsClass(DamageClass.Ranged))
                return;

            if (Vector2.Distance(player.Center, target.Center) > RangePixels)
                return;

            ApplyBonus(player, target, hit);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!item.CountsAsClass(DamageClass.Ranged))
                return;

            if (Vector2.Distance(player.Center, target.Center) > RangePixels)
                return;

            ApplyBonus(player, target, hit);
        }

        private static void ApplyBonus(Player player, NPC target, NPC.HitInfo sourceHit)
        {
            int bonus = System.Math.Max(1, (int)(sourceHit.Damage * DamageBonus));

            var bonusHit = new NPC.HitInfo
            {
                Damage       = bonus,
                SourceDamage = bonus,
                HitDirection = sourceHit.HitDirection,
                DamageType   = DamageClass.Ranged,
                HideCombatText = true
            };

            target.StrikeNPC(bonusHit);

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in bonusHit);

            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("point_blank", bonus, false);

            CombatText.NewText(target.Hitbox, AugmentTextColors.BonusDamage, bonus);
        }
    }
}
