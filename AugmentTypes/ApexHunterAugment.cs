using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class ApexHunterAugment : Augment
    {
        public override string Id => "apex_hunter";
        public override string DisplayName => "Apex Hunter";
        public override string Description =>
            "Ranged hits against a boss build a mark; once it reaches " +
            $"{AugmentText.Trigger("15 hits")}, the next hit triggers a bonus burst dealing " +
            $"{AugmentText.BonusDamage("50% of the damage you dealt while marking")}, up to " +
            $"{AugmentText.BonusDamage("5% of the boss's max HP")}, then the mark resets. " +
            $"{AugmentText.Cooldown("8 seconds cooldown")}.";

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override int CooldownRemaining => LocalPlayerState.ApexHunterCooldown;

        private const int MaxMarkStacks = 15;
        private const float BurstPercentOfMaxHP = 0.05f;
        // The burst pays out half of the damage dealt while the mark was building, so it grows with your weapon and never ignores your
        // gear the way a plain percent of the boss's health would. The percent of max health stays as a ceiling.
        private const float BurstShareOfMarkedDamage = 0.5f;
        private const int CooldownTicks = 8 * 60; // 480 ticks = 8 seconds

        // Tracked per-player (on AugmentPlayer), not per-target - the mark only
        // ever matters against whichever single boss is currently being fought,
        // so there's no need to key it by target.whoAmI.

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.ApexHunterCooldown > 0)
                ap.ApexHunterCooldown--;
        }

        public override void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit)
        {
            if (IsBossTarget(target) && item.CountsAsClass(DamageClass.Ranged))
                HandleMark(player, target, hit.Damage);
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit)
        {
            if (IsBossTarget(target) && proj.CountsAsClass(DamageClass.Ranged))
                HandleMark(player, target, hit.Damage);
        }

        // A hit counts when it lands on the boss or on any part of the boss fight. Many bosses are built from several NPCs and only one
        // of them has the boss flag (Golem's head and fists, worm bodies and tails, Calamity's Ravager limbs, Slime God paladins and so
        // on), so checking only the boss flag missed every hit on those parts. BossFights already knows which NPCs belong to a fight.
        private static bool IsBossTarget(NPC target)
        {
            if (target.boss || BossFights.TryGetFightKey(target, out _))
                return true;

            // Segments that share the health of a boss-flagged head.
            return target.realLife >= 0 && target.realLife < Main.maxNPCs
                && Main.npc[target.realLife].active && Main.npc[target.realLife].boss;
        }

        // The burst is a percentage of the BOSS's max health, not of the part that happened to be hit (a Golem fist or a worm segment
        // has far less than the whole boss).
        private static int BossMaxLife(NPC target)
        {
            int max = target.lifeMax;

            if (target.realLife >= 0 && target.realLife < Main.maxNPCs)
                max = System.Math.Max(max, Main.npc[target.realLife].lifeMax);

            if (BossFights.TryGetFightKey(target, out int key))
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC other = Main.npc[i];
                    if (other.active && other.type == key)
                        max = System.Math.Max(max, other.lifeMax);
                }
            }

            return max;
        }

        private void HandleMark(Player player, NPC target, int hitDamage)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.ApexHunterCooldown > 0)
                return;

            // Every hit while the mark builds (and the one that triggers the burst) adds its damage to the total the burst is paid from.
            ap.ApexHunterMarkDamage += System.Math.Max(0, hitDamage);

            if (ap.ApexHunterMarkStacks >= MaxMarkStacks)
            {
                float marked = ap.ApexHunterMarkDamage;
                ap.ApexHunterMarkStacks = 0;
                ap.ApexHunterMarkDamage = 0f;
                ap.ApexHunterCooldown = CooldownTicks;
                Strike(player, target, HitEffectiveness, marked);
            }
            else
            {
                ap.ApexHunterMarkStacks += HitEffectiveness;
            }
        }

        // Built manually (instead of SimpleStrikeNPC) so the combat text can be
        // forced to yellow - HideCombatText suppresses the auto popup and we
        // spawn our own via CombatText.NewText, same as MeteorStrikeAugment.
        private static void Strike(Player player, NPC target, float effectiveness, float markedDamage)
        {
            // Half of the damage dealt while marking, but never more than 5% of the boss's max health.
            float ceiling = BossMaxLife(target) * BurstPercentOfMaxHP;
            float payout = markedDamage * BurstShareOfMarkedDamage;
            int damage = System.Math.Max(1, (int)(System.Math.Min(ceiling, payout) * effectiveness));

            var hit = new NPC.HitInfo
            {
                Damage = damage,
                SourceDamage = damage,
                HitDirection = player.direction,
                HideCombatText = true
            };

            target.StrikeNPC(hit);
            if (player.whoAmI == Main.myPlayer)
                AugmentDamageTracker.RecordChipHit("apex_hunter", damage, false);
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
