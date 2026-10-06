using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class FocusDirectiveAugment : Augment
    {
        public override string Id => "focus_directive";
        public override string DisplayName => "Focus Directive";
        public override string Description =>
            $"Whips grant {AugmentText.BonusDamage("+6 summon tag damage")} to hit enemies.";

        public override AugmentRarity Rarity => AugmentRarity.Common;
        public override AugmentClass Class => AugmentClass.Summon;

        public const int TagDamage = 6;
        private const int TagDurationTicks = 240; // 4 seconds

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (source == AugmentHitSource.AugmentProc)
                return;

            // Whips count as SummonMeleeSpeed
            if (proj.DamageType == DamageClass.SummonMeleeSpeed || proj.CountsAsClass(DamageClass.SummonMeleeSpeed))
            {
                target.GetGlobalNPC<AugmentFocusDirectiveNPC>().ApplyTag(TagDurationTicks);

                for (int i = 0; i < 4; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.GemRuby, 0f, 0f, 100, default, 1.1f);
                    d.noGravity = true;
                }
            }
            else if (proj.minion || proj.DamageType == DamageClass.Summon || proj.sentry)
            {
                var directive = target.GetGlobalNPC<AugmentFocusDirectiveNPC>();
                if (directive.IsTagged && player.whoAmI == Main.myPlayer)
                {
                    AugmentDamageTracker.RecordChipHit("focus_directive", TagDamage, hit.Crit);
                }
            }
        }
    }
}
