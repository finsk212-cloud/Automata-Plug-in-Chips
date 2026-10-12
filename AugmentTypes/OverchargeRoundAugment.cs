using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class OverchargeRoundAugment : Augment
    {
        public override string Id => "overcharge_round";
        public override string DisplayName => "Overcharge Round";
        public override string Description =>
            $"Consecutive ranged hits build a stacking {AugmentText.OnHit("on-hit")} proc, +1 stack per hit up to 10, " +
            $"resetting after {AugmentText.Duration("1 second")} without a hit. The proc deals {AugmentText.SpecialDamage("5")} " +
            $"plus {AugmentText.SpecialDamage("3")} per stack, up to {AugmentText.SpecialDamage("35")} at full charge.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;

        private const int MaxStacks = 10;
        private const int ResetWindowTicks = 60;
        private const int BaseDamage = 5;
        private const int DamagePerStack = 3;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Base damage", BaseDamage), ("Damage per stack", DamagePerStack), ("Full charge", BaseDamage + MaxStacks * DamagePerStack) };
        }


        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.OverchargeRoundResetTimer <= 0)
                return;

            ap.OverchargeRoundResetTimer--;
            if (ap.OverchargeRoundResetTimer == 0)
                ap.OverchargeRoundHitStacks = 0;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (item.DamageType == DamageClass.Ranged)
                ApplyStack(player, target);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (proj.DamageType == DamageClass.Ranged)
                ApplyStack(player, target);
        }

        private void ApplyStack(Player player, NPC target)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.OverchargeRoundResetTimer > 0)
            {
                if (ap.OverchargeRoundHitStacks < MaxStacks)
                    ap.OverchargeRoundHitStacks += HitEffectiveness;
            }
            else
            {
                ap.OverchargeRoundHitStacks = HitEffectiveness;
            }

            ap.OverchargeRoundResetTimer = ResetWindowTicks;

            int damage = (int)(AugmentScaling.Scale((int)(BaseDamage + ap.OverchargeRoundHitStacks * DamagePerStack)) * HitEffectiveness);
            Strike(player, target, System.Math.Max(1, damage));
        }

        // Built manually (instead of plain SimpleStrikeNPC) so the combat
        // text can be forced to the same blue used for Iron Rhythm/Twin
        // Strike's popups - HideCombatText suppresses the auto popup and
        // CombatText.NewText spawns our own.
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
                AugmentDamageTracker.RecordChipHit("overcharge_round", damage, false);
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
