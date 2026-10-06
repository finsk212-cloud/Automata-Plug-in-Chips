using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class SentrySalvoAugment : Augment
    {
        public override string Id => "sentry_salvo";
        public override string DisplayName => "Sentry Salvo";
        public override string Description =>
            $"Sentry attacks have a {FormatCurrentChance(ProcChance)} to strike a {AugmentText.BonusDamage("second time")}.";

        public override string GetDetailedDescription(Player player) =>
            $"Sentry attacks have a {FormatFortuneChance(ProcChance, player)} to strike a {AugmentText.BonusDamage("second time")}.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Summon;

        private const float ProcChance = 0.25f;

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            bool isSentry = proj.sentry || (proj.type >= 0 && proj.type < ProjectileID.Sets.SentryShot.Length && ProjectileID.Sets.SentryShot[proj.type]);
            if (!isSentry)
                return;

            if (RollFortuneChance(player, ProcChance))
            {
                ApplyDoubleStrike(player, target, hit);
            }
        }

        private static void ApplyDoubleStrike(Player player, NPC target, NPC.HitInfo sourceHit)
        {
            int echoDamage = System.Math.Max(1, sourceHit.Damage);

            var echoHit = new NPC.HitInfo
            {
                Damage = echoDamage,
                SourceDamage = echoDamage,
                HitDirection = sourceHit.HitDirection,
                DamageType = DamageClass.Summon,
                HideCombatText = true
            };

            target.StrikeNPC(echoHit);

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in echoHit);

            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("sentry_salvo", echoDamage, false);

            CombatText.NewText(target.Hitbox, AugmentTextColors.SpecialDamage, echoDamage);

            // Electric spark burst on double strike
            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Electric, 0f, 0f, 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
