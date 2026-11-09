using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Augments.Core;

namespace Augments
{
    public class TurretSyndromeAugment : Augment
    {
        public override string Id => "turret_syndrome";
        public override string DisplayName => "Turret Syndrome";
        public override string Description =>
            $"Permanently grants {AugmentText.Defense("+1 max sentry slot")}. Active sentries become {AugmentText.Trigger("mobile hovering drones")} " +
            $"that follow you in formation, projecting a lethal {AugmentText.SpecialDamage("28 summon damage laser grid")} between them.";

        public override string GetDetailedDescription(Player player) =>
            AugmentScaling.ApplyToText(Description, GetScalingEntries()) + "\n" +
            AugmentText.Note("(Sentries hover in orbit around you. Enemies passing through the connecting laser matrix take continuous tick damage and are slowed by 30%.)") + "\n" +
            AugmentText.Note("(Damage ticks 4x/sec, scaling across world stages and with your Summon damage bonuses.)");

        public override AugmentRarity Rarity => AugmentRarity.Legendary;
        public override AugmentClass Class => AugmentClass.Summon;

        public const int BaseDamage = 28;
        public const int HitCooldownTicks = 15; // 4 hits per second max per enemy
        public const float LineCollisionWidth = 16f;

        public override (string label, int baseValue)[] GetScalingEntries()
        {
            return new[] { ("Laser grid damage", BaseDamage) };
        }

        private static readonly Dictionary<int, int> npcHitCooldown = new();
        private static int soundCooldown = 0;

        public override void UpdateEquips(Player player)
        {
            player.maxTurrets += 1;
        }

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

            List<Projectile> sentries = GetActiveSentries(player);
            if (sentries.Count == 0)
                return;

            // --- 1. Laser Grid Line Segments & Ambient Light ---
            List<(Vector2 start, Vector2 end)> segments = GetLaserSegments(player, sentries);
            if (segments.Count == 0)
                return;

            foreach (var seg in segments)
            {
                Lighting.AddLight((seg.start + seg.end) * 0.5f, 0.45f, 0.22f, 0.05f);

                // Occasional fiery plasma sparks zipping along the beam
                if (Main.rand.NextBool(2))
                {
                    float randT = Main.rand.NextFloat();
                    Vector2 sparkPos = Vector2.Lerp(seg.start, seg.end, randT);
                    Dust d = Dust.NewDustPerfect(
                        sparkPos + Main.rand.NextVector2Circular(2f, 2f),
                        DustID.SolarFlare,
                        (seg.end - seg.start).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3.5f),
                        50,
                        default,
                        Main.rand.NextFloat(0.7f, 1.1f)
                    );
                    d.noGravity = true;
                }
            }

            // --- 2. Collision & Damage Execution (Local player authority only) ---
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

                bool hit = false;
                for (int s = 0; s < segments.Count; s++)
                {
                    if (Collision.CheckAABBvLineCollision(npcPos, npcSize, segments[s].start, segments[s].end, LineCollisionWidth, ref colPoint))
                    {
                        hit = true;
                        break;
                    }
                }

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

                    npc.AddBuff(BuffID.Slow, 120);    // 30% slow for 2s
                    npc.AddBuff(BuffID.OnFire3, 180); // Hellfire for 3s
                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.NPCBuffs, number: npc.whoAmI);

                    // Impact sparks
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.OrangeTorch, 0f, 0f, 100, default, 1.1f);
                    d.noGravity = true;
                    d.velocity *= 0.4f;

                    if (soundCooldown <= 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.2f }, npc.Center);
                        soundCooldown = 15;
                    }
                }
            }
        }

        public static List<Projectile> GetActiveSentries(Player player)
        {
            var list = new List<Projectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.sentry)
                {
                    list.Add(p);
                }
            }
            return list;
        }

        public static void AnchorSentryToFormation(Player player, Projectile sentry)
        {
            List<Projectile> sentries = GetActiveSentries(player);
            int index = sentries.IndexOf(sentry);
            if (index < 0)
                return;

            int count = sentries.Count;
            Vector2 targetPos = CalculateSentrySlot(player, index, count);

            // Neutralize any gravity or movement vanilla AI attempted
            sentry.tileCollide = false;
            sentry.velocity = Vector2.Zero;
            sentry.gfxOffY = 0f;

            // Direct rock-solid anchor with zero lag, jitter, or rubberbanding
            sentry.Center = targetPos;

            // Repulsor drone exhaust particles & light under sentry base
            if (Main.rand.NextBool(3))
            {
                Vector2 exhaustPos = sentry.Bottom;
                Dust d = Dust.NewDustPerfect(
                    exhaustPos + new Vector2(Main.rand.NextFloat(-8f, 8f), 2f),
                    DustID.OrangeTorch,
                    new Vector2(0f, Main.rand.NextFloat(1.5f, 3f)),
                    100,
                    default,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                d.noGravity = true;
                Lighting.AddLight(exhaustPos, 0.35f, 0.20f, 0.05f);
            }
        }

        public static Vector2 CalculateSentrySlot(Player player, int index, int count)
        {
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + index * 1.5f) * 3f;

            if (count == 1)
            {
                return player.Center + new Vector2(-60f * player.direction, -60f + bob);
            }
            else if (count == 2)
            {
                float xOffset = (index == 0) ? -85f : 85f;
                return player.Center + new Vector2(xOffset, -50f + bob);
            }
            else if (count == 3)
            {
                if (index == 0)
                    return player.Center + new Vector2(0f, -85f + bob);
                else if (index == 1)
                    return player.Center + new Vector2(-85f, 25f + bob);
                else
                    return player.Center + new Vector2(85f, 25f + bob);
            }
            else
            {
                float angleStep = MathHelper.TwoPi / count;
                float orbitAngle = angleStep * index + (float)Main.GlobalTimeWrappedHourly * 1.0f;
                float rx = 105f + count * 5f;
                float ry = 70f + count * 3f;
                return player.Center + new Vector2((float)Math.Cos(orbitAngle) * rx, (float)Math.Sin(orbitAngle) * ry - 15f + bob);
            }
        }

        public static List<(Vector2 start, Vector2 end)> GetLaserSegments(Player player, List<Projectile> sentries)
        {
            var segments = new List<(Vector2 start, Vector2 end)>();
            int count = sentries.Count;
            if (count == 0)
                return segments;

            if (count == 1)
            {
                // Single sentry: tether from player's core to sentry
                segments.Add((player.Center + new Vector2(0f, -10f), sentries[0].Center));
            }
            else if (count == 2)
            {
                // Two sentries: bridge cleanly across the two gunships (NO lines crossing player face/mount!)
                segments.Add((sentries[0].Center, sentries[1].Center));
            }
            else
            {
                // 3+ sentries: outer perimeter barrier surrounding player
                for (int i = 0; i < count; i++)
                {
                    segments.Add((sentries[i].Center, sentries[(i + 1) % count].Center));
                }
            }
            return segments;
        }

        public static void DrawAllLaserGrids(SpriteBatch spriteBatch)
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active || player.dead)
                    continue;

                var ap = player.GetModPlayer<AugmentPlayer>();
                if (!ap.HasAugment("turret_syndrome"))
                    continue;

                List<Projectile> sentries = GetActiveSentries(player);
                if (sentries.Count == 0)
                    continue;

                List<(Vector2 start, Vector2 end)> segments = GetLaserSegments(player, sentries);
                foreach (var seg in segments)
                {
                    DrawLaserBeam(spriteBatch, seg.start, seg.end);
                }
            }
        }

        private static void DrawLaserBeam(SpriteBatch spriteBatch, Vector2 start, Vector2 end)
        {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 2f)
                return;

            float rotation = diff.ToRotation();
            Vector2 normal = new Vector2(-diff.Y, diff.X) / length;
            Vector2 origin = new Vector2(0f, 0.5f);
            Rectangle pixelSource = new Rectangle(0, 0, 1, 1);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 drawPos = start - Main.screenPosition;

            float time = (float)Main.GlobalTimeWrappedHourly;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 18f);

            // 1. Broad Outer Laser Bloom (Soft ambient radiation aura)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(255, 110, 20) * (0.28f + pulse * 0.08f),
                rotation,
                origin,
                new Vector2(length, 11f + pulse * 2f),
                SpriteEffects.None,
                0f
            );

            // 2. High-Energy Plasma Sheath (Vivid burning orange)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(255, 75, 10) * 0.75f,
                rotation,
                origin,
                new Vector2(length, 5f + pulse * 1f),
                SpriteEffects.None,
                0f
            );

            // 3. Central Razor Core (White-hot energy filament)
            spriteBatch.Draw(
                pixel,
                drawPos,
                pixelSource,
                new Color(255, 250, 220) * 0.95f,
                rotation,
                origin,
                new Vector2(length, 1.8f),
                SpriteEffects.None,
                0f
            );

            // 4. Dual Helical Energy Ribbons (Spiraling corkscrew plasma filaments)
            int ribbonSegments = Math.Max(12, (int)(length / 14f));
            Vector2 prevPt1 = start;
            Vector2 prevPt2 = start;
            float waveSpeed = 16f;
            float waveFreq = 18f;

            for (int i = 1; i <= ribbonSegments; i++)
            {
                float t = i / (float)ribbonSegments;
                float prevT = (i - 1) / (float)ribbonSegments;
                Vector2 basePt = Vector2.Lerp(start, end, t);

                // Sine envelope tapering at both endpoints
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float prevEnvelope = (float)Math.Sin(prevT * MathHelper.Pi);

                float angle1 = time * waveSpeed - t * waveFreq;
                float prevAngle1 = time * waveSpeed - prevT * waveFreq;

                float radius = (6f + pulse * 1.5f) * envelope;
                float prevRadius = (6f + pulse * 1.5f) * prevEnvelope;

                Vector2 curPt1 = basePt + normal * ((float)Math.Sin(angle1) * radius);
                Vector2 curPt2 = basePt - normal * ((float)Math.Sin(angle1) * radius);

                if (i == 1)
                {
                    prevPt1 = Vector2.Lerp(start, end, prevT) + normal * ((float)Math.Sin(prevAngle1) * prevRadius);
                    prevPt2 = Vector2.Lerp(start, end, prevT) - normal * ((float)Math.Sin(prevAngle1) * prevRadius);
                }

                // Draw segment for Ribbon 1 (Solar Gold)
                Vector2 segDiff1 = curPt1 - prevPt1;
                float segLen1 = Math.Max(1f, segDiff1.Length());
                float segRot1 = segDiff1.ToRotation();
                spriteBatch.Draw(
                    pixel,
                    prevPt1 - Main.screenPosition,
                    pixelSource,
                    new Color(255, 215, 60) * 0.85f,
                    segRot1,
                    origin,
                    new Vector2(segLen1 + 0.5f, 1.8f),
                    SpriteEffects.None,
                    0f
                );

                // Draw segment for Ribbon 2 (Fiery Red-Amber)
                Vector2 segDiff2 = curPt2 - prevPt2;
                float segLen2 = Math.Max(1f, segDiff2.Length());
                float segRot2 = segDiff2.ToRotation();
                spriteBatch.Draw(
                    pixel,
                    prevPt2 - Main.screenPosition,
                    pixelSource,
                    new Color(255, 100, 20) * 0.85f,
                    segRot2,
                    origin,
                    new Vector2(segLen2 + 0.5f, 1.8f),
                    SpriteEffects.None,
                    0f
                );

                prevPt1 = curPt1;
                prevPt2 = curPt2;
            }

            // 5. Energy Emitter Nodes (Pulsing anchor flare rings at start and end)
            DrawEmitterNode(spriteBatch, pixel, pixelSource, start - Main.screenPosition, pulse);
            DrawEmitterNode(spriteBatch, pixel, pixelSource, end - Main.screenPosition, pulse);
        }

        private static void DrawEmitterNode(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pixelSource, Vector2 screenPos, float pulse)
        {
            Vector2 nodeOrigin = new Vector2(0.5f, 0.5f);
            float nodeSize = 12f + pulse * 3f;

            // Outer bloom aura
            spriteBatch.Draw(
                pixel,
                screenPos,
                pixelSource,
                new Color(255, 120, 20) * 0.40f,
                0f,
                nodeOrigin,
                new Vector2(nodeSize, nodeSize),
                SpriteEffects.None,
                0f
            );

            // Inner bright core
            float innerSize = 6f + pulse * 1.5f;
            spriteBatch.Draw(
                pixel,
                screenPos,
                pixelSource,
                new Color(255, 250, 210) * 0.95f,
                (float)Math.PI / 4f, // 45 deg diamond glint
                nodeOrigin,
                new Vector2(innerSize, innerSize),
                SpriteEffects.None,
                0f
            );
        }
    }

    public class TurretSyndromeGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.active && projectile.sentry && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && player.GetModPlayer<AugmentPlayer>().HasAugment("turret_syndrome"))
                {
                    // Disable tile collision and zero out any gravity before vanilla AI can use it
                    projectile.tileCollide = false;
                    projectile.velocity = Vector2.Zero;
                }
            }
            return true;
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.active && projectile.sentry && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player player = Main.player[projectile.owner];
                if (player.active && !player.dead && player.GetModPlayer<AugmentPlayer>().HasAugment("turret_syndrome"))
                {
                    TurretSyndromeAugment.AnchorSentryToFormation(player, projectile);
                }
            }
        }
    }

    public class TurretSyndromeSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer(
                "Augments: Turret Syndrome",
                delegate
                {
                    TurretSyndromeAugment.DrawAllLaserGrids(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }
    }
}
