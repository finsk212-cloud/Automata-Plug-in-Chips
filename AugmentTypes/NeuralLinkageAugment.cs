using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class NeuralLinkageAugment : Augment
    {
        public override string Id => "neural_linkage";
        public override string DisplayName => "Neural Linkage";
        public override string Description =>
            $"While you have an active minion, reduce incoming damage by {AugmentText.Defense("20%")}. " +
            $"Taking damage commands your nearest minion to counter-dash the attacker for {AugmentText.BonusDamage("200% minion damage")}. " +
            $"{AugmentText.Cooldown("2s counter cooldown")}.";

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        public const float DamageReductionPercent = 0.20f;
        public const int CounterCooldownTicks = 120; // 2 seconds
        public const float CounterDashSpeed = 22f;
        public const float CounterDamageMultiplier = 2.0f;

        public override int CooldownRemaining => LocalPlayerState.NeuralLinkageCooldown;

        public override void OnUpdate(Player player)
        {
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.NeuralLinkageCooldown > 0)
                ap.NeuralLinkageCooldown--;
        }

        public override void ModifyHurt(Player player, ref Player.HurtModifiers modifiers)
        {
            if (HasActiveMinion(player))
            {
                modifiers.FinalDamage *= (1f - DamageReductionPercent);
            }
        }

        public override void OnHurt(Player player, Player.HurtInfo info)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (!HasActiveMinion(player))
                return;

            // Track damage absorbed in DPS tracker
            int damageAbsorbed = Math.Max(1, (int)(info.Damage * (DamageReductionPercent / (1f - DamageReductionPercent))));
            AugmentDamageTracker.RecordDamageBlocked(Id, DisplayName, damageAbsorbed, Rarity, Class);

            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.NeuralLinkageCooldown > 0)
                return;

            ap.NeuralLinkageCooldown = CounterCooldownTicks;

            // Find closest minion to player
            Projectile closestMinion = null;
            float minDistanceSq = float.MaxValue;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.minion && proj.damage > 0)
                {
                    float distSq = Vector2.DistanceSquared(proj.Center, player.Center);
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        closestMinion = proj;
                    }
                }
            }

            if (closestMinion == null)
                return;

            // Determine target position or direction
            Vector2 targetPos = player.Center;
            NPC targetNPC = FindNearestHostileNPC(player.Center, 800f);
            if (targetNPC != null)
            {
                targetPos = targetNPC.Center;
            }
            else
            {
                targetPos = player.Center - new Vector2(info.HitDirection * 200f, 0f);
            }

            // Launch minion towards target
            Vector2 dashDir = (targetPos - closestMinion.Center);
            if (dashDir != Vector2.Zero)
            {
                dashDir.Normalize();
                closestMinion.velocity = dashDir * CounterDashSpeed;
                if (Main.netMode != NetmodeID.SinglePlayer)
                    closestMinion.netUpdate = true;
            }

            // Play NieR electric clink audio & laser particle effects
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = 0.2f }, closestMinion.Center);

            // Laser tether particle line from player to minion
            Vector2 start = player.Center;
            Vector2 end = closestMinion.Center;
            float tetherDist = Vector2.Distance(start, end);
            int stepCount = Math.Max(4, (int)(tetherDist / 20f));
            for (int s = 0; s < stepCount; s++)
            {
                Vector2 pos = Vector2.Lerp(start, end, s / (float)stepCount);
                Dust d = Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 100, Color.Cyan, 0.9f);
                d.noGravity = true;
            }

            // Burst particles around minion
            for (int d = 0; d < 8; d++)
            {
                Dust dust = Dust.NewDustDirect(closestMinion.position, closestMinion.width, closestMinion.height, DustID.GemTopaz, 0f, 0f, 100, default, 1.2f);
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                dust.noGravity = true;
            }
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            // If an active minion hits within the counter window, deal 200% damage once
            var ap = player.GetModPlayer<AugmentPlayer>();
            if (ap.NeuralLinkageCooldown >= (CounterCooldownTicks - 30) && proj.minion && proj.owner == player.whoAmI)
            {
                modifiers.SourceDamage *= CounterDamageMultiplier;
            }
        }

        private static bool HasActiveMinion(Player player)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.minion)
                    return true;
            }
            return false;
        }

        private static NPC FindNearestHostileNPC(Vector2 center, float maxRange)
        {
            NPC best = null;
            float maxDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                {
                    float dSq = Vector2.DistanceSquared(center, npc.Center);
                    if (dSq < maxDistSq)
                    {
                        maxDistSq = dSq;
                        best = npc;
                    }
                }
            }

            return best;
        }
    }
}
