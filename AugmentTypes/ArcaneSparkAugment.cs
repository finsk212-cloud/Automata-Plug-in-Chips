using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ArcaneSparkAugment : Augment
    {
        public override string Id => "arcane_spark";
        public override string DisplayName => "Arcane Spark";
        public override string Description =>
            $"Magic hits deal {AugmentText.SpecialDamage("6 bonus magic damage")} {AugmentText.OnHit("on-hit")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Magic;

        public const int SparkDamage = 6;

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!proj.CountsAsClass(DamageClass.Magic) && proj.DamageType != DamageClass.Magic)
                return;

            ApplySpark(player, target, hit);
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            if (!item.CountsAsClass(DamageClass.Magic) && item.DamageType != DamageClass.Magic)
                return;

            ApplySpark(player, target, hit);
        }

        private static void ApplySpark(Player player, NPC target, NPC.HitInfo sourceHit)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            ap.RecordOnHitDamage(SparkDamage);

            var sparkHit = new NPC.HitInfo
            {
                Damage = SparkDamage,
                SourceDamage = SparkDamage,
                HitDirection = sourceHit.HitDirection,
                DamageType = DamageClass.Magic,
                HideCombatText = true
            };

            target.StrikeNPC(sparkHit);

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in sparkHit);

            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("arcane_spark", SparkDamage, false);

            CombatText.NewText(target.Hitbox, AugmentTextColors.SpecialDamage, SparkDamage);

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Electric, 0f, 0f, 100, default, 0.9f);
                d.noGravity = true;
            }
        }
    }
}
