using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Augments.Core;

namespace Augments
{
    public class MeteorStrikeAugment : Augment
    {
        public override string Id => "meteor_strike";
        public override string DisplayName => "Meteor Strike";
        public override string Description =>
            $"{AugmentText.Crit("Crits")} against bosses have a {FormatCurrentChance(ProcChance)} to call down a bonus strike " +
            $"dealing {AugmentText.BonusDamage("2.5%")} of the boss's {AugmentText.HP("maximum HP")}.";

        public override string GetDetailedDescription(Player player) =>
            $"{AugmentText.Crit("Crits")} against bosses have a {FormatFortuneChance(ProcChance, player)} to call down a bonus strike " +
            $"dealing {AugmentText.BonusDamage("2.5%")} of the boss's {AugmentText.HP("maximum HP")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Universal;

        private const float ProcChance = 0.15f;
        private const float BonusDamagePercentOfMaxHP = 0.025f;

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && target.boss && RollFortuneChance(player, ProcChance))
                Strike(player, target, HitEffectiveness);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (hit.Crit && target.boss && RollFortuneChance(player, ProcChance))
                Strike(player, target, HitEffectiveness);
        }

        // Built manually (instead of SimpleStrikeNPC) so the combat text can be
        // forced to yellow - HideCombatText suppresses the auto popup and we
        // spawn our own via CombatText.NewText.
        private static void Strike(Player player, NPC target, float effectiveness)
        {
            int damage = System.Math.Max(1, (int)(target.lifeMax * BonusDamagePercentOfMaxHP * effectiveness));

            var hit = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = player.direction,
                HideCombatText = true
            };

            target.StrikeNPC(hit);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("meteor_strike", damage, false);
            // StrikeNPC does not sync itself. In multiplayer the damage would
            // only apply on the local client; the server's authoritative NPC
            // never takes it and re-syncs back to alive (looks like a "respawn").
            // Mirror SimpleStrikeNPC and relay the strike to the server/clients.
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in hit);
            CombatText.NewText(target.Hitbox, Color.Yellow, damage);
        }
    }
}
