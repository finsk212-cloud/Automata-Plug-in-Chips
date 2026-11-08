using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class VoltTetherAugment : Augment
    {
        public override string Id => "volt_tether";
        public override string DisplayName => "Volt Tether";
        public override string Description =>
            $"High-voltage arcs {AugmentText.Trigger("tether you to your 2 nearest minions")}. " +
            $"Enemies caught in the electric arcs take continuous {AugmentText.SpecialDamage("22 summon damage")} and are {AugmentText.Trigger("Electrified")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Tether range is 550 pixels. If you have 2 or more minions active, an electric cross-tether also links them together. Deals damage up to 5 times per second, scaling with world progression and summon damage.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Summon;

        public const int BaseDamage = 22;
        public const float MaxTetherDistance = 550f;
        public const int HitCooldownTicks = 12; // 5 hits per second max per enemy
        public const float LineCollisionWidth = 14f;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Tether damage", BaseDamage) };
        }

        private static readonly Dictionary<int, int> npcHitCooldown = new();
        private static int soundCooldown = 0;

        public override void OnUpdate(Player player)
        {
            if (player.dead || !player.active)
                return;

            if (soundCooldown > 0)
                soundCooldown--;

            // Periodically clean up inactive NPCs from the cooldown dictionary
            if (Main.GameUpdateCount % 180 == 0 && npcHitCooldown.Count > 0)
            {
                var dead = new List<int>();
                foreach (var id in npcHitCooldown.Keys)
                {
                    if (id < 0 || id >= Main.maxNPCs || !Main.npc[id].active)
                        dead.Add(id);
                }
                foreach (var id in dead)
                    npcHitCooldown.Remove(id);
            }

            // Locate up to 2 closest active minions/sentries within range
            GetNearestMinions(player, MaxTetherDistance, out Projectile m1, out Projectile m2);
            if (m1 == null)
                return;

            Vector2 playerCenter = player.Center;
            Vector2 m1Center = m1.Center;

            // Draw visual electric particles on all clients
            DrawTether(playerCenter, m1Center);

            bool hasTwo = m2 != null;
            Vector2 m2Center = hasTwo ? m2.Center : Vector2.Zero;

            if (hasTwo)
            {
                DrawTether(playerCenter, m2Center);
                DrawTether(m1Center, m2Center); // Cross-tether between the two minions
            }

            // Damage and debuff application: only local player computes collisions to avoid multiplayer duplicates
            if (player.whoAmI != Main.myPlayer)
                return;

            int currentTick = (int)Main.GameUpdateCount;
            int baseDmg = AugmentScaling.Scale(BaseDamage);
            int finalDmg = Math.Max(1, (int)player.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg));

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
                    continue;

                if (npcHitCooldown.TryGetValue(n, out int lastHit) && currentTick - lastHit < HitCooldownTicks)
                    continue;

                float colPoint = 0f;
                Vector2 npcPos = npc.position;
                Vector2 npcSize = npc.Size;

                bool hit = Collision.CheckAABBvLineCollision(npcPos, npcSize, playerCenter, m1Center, LineCollisionWidth, ref colPoint)
                    || (hasTwo && (Collision.CheckAABBvLineCollision(npcPos, npcSize, playerCenter, m2Center, LineCollisionWidth, ref colPoint)
                                || Collision.CheckAABBvLineCollision(npcPos, npcSize, m1Center, m2Center, LineCollisionWidth, ref colPoint)));

                if (hit)
                {
                    npcHitCooldown[n] = currentTick;

                    var hitInfo = new NPC.HitInfo
                    {
                        Damage = finalDmg,
                        SourceDamage = finalDmg,
                        HitDirection = npc.Center.X >= player.Center.X ? 1 : -1,
                        DamageType = DamageClass.Summon,
                        Knockback = 0.5f,
                        HideCombatText = true
                    };

                    npc.StrikeNPC(hitInfo);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(npc, in hitInfo);

                    CombatText.NewText(npc.Hitbox, AugmentTextColors.SpecialDamage, finalDmg);
                    AugmentDamageTracker.RecordChipHit(Id, finalDmg, false);

                    // Electrified debuff
                    npc.AddBuff(BuffID.Electrified, 120);
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.NPCBuffs, number: npc.whoAmI);

                    // Impact electric dust sparks
                    for (int k = 0; k < 3; k++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, 0.85f);
                        d.noGravity = true;
                        d.velocity *= 0.5f;
                    }

                    if (soundCooldown <= 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.3f }, npc.Center);
                        soundCooldown = 15;
                    }
                }
            }
        }

        private static void GetNearestMinions(Player player, float maxDist, out Projectile m1, out Projectile m2)
        {
            m1 = null;
            m2 = null;
            float dist1Sq = maxDist * maxDist;
            float dist2Sq = maxDist * maxDist;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI)
                    continue;

                bool isMinion = p.minion || p.sentry || ProjectileID.Sets.MinionSacrificable[p.type] || p.minionSlots > 0f;
                if (!isMinion)
                    continue;

                float dSq = Vector2.DistanceSquared(player.Center, p.Center);
                if (dSq < dist1Sq)
                {
                    dist2Sq = dist1Sq;
                    m2 = m1;

                    dist1Sq = dSq;
                    m1 = p;
                }
                else if (dSq < dist2Sq)
                {
                    dist2Sq = dSq;
                    m2 = p;
                }
            }
        }

        private static void DrawTether(Vector2 start, Vector2 end)
        {
            float dist = Vector2.Distance(start, end);
            if (dist <= 8f)
                return;

            Vector2 diff = end - start;
            Vector2 dir = diff / dist;
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            // One dust sample every ~22 pixels for smooth electric arc
            int steps = Math.Max(3, (int)(dist / 22f));
            float time = (float)Main.timeForVisualEffects * 0.35f;

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 pos = Vector2.Lerp(start, end, t);

                // Electric harmonic jitter to produce a crackling arc
                float wave = (float)Math.Sin(time * 12f + s * 1.8f) * 3.5f + Main.rand.NextFloat(-2f, 2f);
                pos += perp * wave;

                Lighting.AddLight(pos, 0.08f, 0.22f, 0.38f);

                // Flickering probability for lively electric discharge
                if (Main.rand.NextFloat() < 0.65f)
                {
                    Dust d = Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 100, default, Main.rand.NextFloat(0.7f, 1.05f));
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }
        }
    }
}
