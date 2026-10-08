using Terraria;
using Terraria.ID;
using Augments.Core;

namespace Augments
{
    public class TwinStrikeAugment : Augment
    {
        public override string Id => "twin_strike";
        public override string DisplayName => "Twin Strike";
        public override string Description =>
            $"{AugmentText.Crit("Crits")} apply {AugmentText.OnHit("on-hit")} twice. Deal {AugmentText.SpecialDamage("15 damage")} {AugmentText.OnHit("on-hit")}.";

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Universal;

        // The crit-doubling effect itself has no logic of its own - it's
        // entirely this flag, read by AugmentPlayer's OnHitNPCWithItem/Proj
        // dispatchers. The 15-damage proc below is a separate, independent
        // effect on top of that, not part of the doubling.
        public override bool DuplicatesOnHitEffects => true;

        private const int ProcDamage = 15;

        // The 15-damage proc fires on every hit, crit or not - only the
        // CRIT-doubling pass is conditional. Owning Twin Strike guarantees
        // the dispatcher's second pass always fires on a crit
        // (DuplicatesOnHitEffects is this augment's own flag), so on a crit
        // specifically, OnHitNPCWithItem/Proj below is always called exactly
        // twice. Rather than popping two separate +15s on a crit, the first
        // call deals the full doubled amount immediately and the pending
        // flag (per-player, on AugmentPlayer) makes the second call a no-op,
        // so a crit reads as one combined +30.
        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!hit.Crit)
            {
                int dmg = ScaleHitEffect(ProcDamage);
                ap.RecordOnHitDamage(dmg);
                Strike(player, target, dmg);
                return;
            }

            if (ap.TwinStrikeItemProcPending)
            {
                ap.TwinStrikeItemProcPending = false;
                return;
            }

            ap.TwinStrikeItemProcPending = true;
            int doubledDmg = ScaleHitEffect(ProcDamage * 2);
            ap.RecordOnHitDamage(doubledDmg);
            Strike(player, target, doubledDmg);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (!hit.Crit)
            {
                int dmg = ScaleHitEffect(ProcDamage);
                ap.RecordOnHitDamage(dmg);
                Strike(player, target, dmg);
                return;
            }

            if (ap.TwinStrikeProjProcPending)
            {
                ap.TwinStrikeProjProcPending = false;
                return;
            }

            ap.TwinStrikeProjProcPending = true;
            int doubledDmg = ScaleHitEffect(ProcDamage * 2);
            ap.RecordOnHitDamage(doubledDmg);
            Strike(player, target, doubledDmg);
        }

        private static void Strike(Player player, NPC target, int damage)
        {
            var hit = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = player.direction,
                HideCombatText = true
            };

            target.StrikeNPC(hit);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("twin_strike", damage, false);
            // StrikeNPC does not sync itself. In multiplayer the damage would
            // only apply on the local client; the server's authoritative NPC
            // never takes it and re-syncs back to alive (looks like a "respawn").
            // Mirror SimpleStrikeNPC and relay the strike to the server/clients.
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in hit);
            CombatText.NewText(target.Hitbox, AugmentTextColors.SpecialDamage, damage);
        }
    }
}
