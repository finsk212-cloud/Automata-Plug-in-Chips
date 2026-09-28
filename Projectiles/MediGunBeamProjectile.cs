using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments.Projectiles
{
    public class MediGunBeamProjectile : ModProjectile
    {
        private const float MaxAcquireRange = 650f;
        private const float MaxBreakRange = 750f;
        private const int HealPulseInterval = 60; // 1s per pulse (3 HP/sec)
        private const int HealAmount = 3;

        private int targetPlayerWhoAmI = -1;
        private int targetNPCWhoAmI = -1;
        private int healPulseTimer;
        private Vector2 laggedMidPoint = Vector2.Zero;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.channel || player.noItems || player.CCed)
            {
                Projectile.Kill();
                return;
            }

            // Eliminate duplicate projectiles: if another beam projectile exists for this owner, kill self
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.type == Projectile.type && other.whoAmI > Projectile.whoAmI)
                {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.timeLeft = 2;

            Vector2 muzzlePos = player.MountedCenter;
            int socketed = player.HeldItem?.TryGetGlobalItem<Items.MediGunGlobalItem>(out var mediGun) == true ? mediGun.SocketedAccessoryType : 0;

            // ONLY the local owner determines targeting and sends healing requests
            if (Projectile.owner == Main.myPlayer)
            {
                // 1. Maintain or drop current target
                if (targetPlayerWhoAmI >= 0)
                {
                    Player target = Main.player[targetPlayerWhoAmI];
                    if (!target.active || target.dead || Vector2.Distance(muzzlePos, target.Center) > MaxBreakRange ||
                        !SupportEffects.CanBeamPassLine(muzzlePos, target.Center) || !SupportEffects.AreAllies(player, target))
                    {
                        targetPlayerWhoAmI = -1;
                    }
                }

                if (targetNPCWhoAmI >= 0)
                {
                    NPC npc = Main.npc[targetNPCWhoAmI];
                    if (!npc.active || (!npc.townNPC && npc.type != NPCID.TargetDummy && !npc.friendly) ||
                        Vector2.Distance(muzzlePos, npc.Center) > MaxBreakRange ||
                        !SupportEffects.CanBeamPassLine(muzzlePos, npc.Center))
                    {
                        targetNPCWhoAmI = -1;
                    }
                }

                // 2. If no target locked, acquire closest eligible target near cursor
                if (targetPlayerWhoAmI < 0 && targetNPCWhoAmI < 0)
                {
                    float bestScore = float.MaxValue;

                    // Check players first
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        Player candidate = Main.player[i];
                        if (!candidate.active || candidate.dead || candidate.whoAmI == player.whoAmI)
                            continue;
                        if (!SupportEffects.AreAllies(player, candidate))
                            continue;
                        if (Vector2.Distance(muzzlePos, candidate.Center) > MaxAcquireRange)
                            continue;
                        if (!SupportEffects.CanBeamPassLine(muzzlePos, candidate.Center))
                            continue;

                        float cursorDist = Vector2.Distance(Main.MouseWorld, candidate.Center);
                        if (cursorDist < bestScore)
                        {
                            bestScore = cursorDist;
                            targetPlayerWhoAmI = candidate.whoAmI;
                        }
                    }

                    // If no player in range, check town NPCs or target dummies
                    if (targetPlayerWhoAmI < 0)
                    {
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC candidate = Main.npc[i];
                            if (!candidate.active || (!candidate.townNPC && candidate.type != NPCID.TargetDummy && !candidate.friendly))
                                continue;
                            if (Vector2.Distance(muzzlePos, candidate.Center) > MaxAcquireRange)
                                continue;
                            if (!SupportEffects.CanBeamPassLine(muzzlePos, candidate.Center))
                                continue;

                            float cursorDist = Vector2.Distance(Main.MouseWorld, candidate.Center);
                            if (cursorDist < bestScore)
                            {
                                bestScore = cursorDist;
                                targetNPCWhoAmI = candidate.whoAmI;
                            }
                        }
                    }
                }

                // Sync encoded target across network (players: 0..255, NPCs: 1000+, none: -1)
                int encoded = targetPlayerWhoAmI >= 0 ? targetPlayerWhoAmI : (targetNPCWhoAmI >= 0 ? 1000 + targetNPCWhoAmI : -1);
                if ((int)Projectile.ai[0] != encoded)
                {
                    Projectile.ai[0] = encoded;
                    Projectile.netUpdate = true;
                }

                // 3. Healing pulses and Overclock building (owner only)
                var mediPlayer = player.GetModPlayer<MediGunPlayer>();
                mediPlayer.CurrentPatientWhoAmI = targetPlayerWhoAmI;
                mediPlayer.CurrentTargetNPCWhoAmI = targetNPCWhoAmI;

                bool isLocked = targetPlayerWhoAmI >= 0 || targetNPCWhoAmI >= 0;
                if (isLocked)
                {
                    bool isTargetHurt = false;
                    if (targetPlayerWhoAmI >= 0)
                    {
                        Player target = Main.player[targetPlayerWhoAmI];
                        int targetMaxLife = Math.Max(target.statLifeMax, target.statLifeMax2);
                        if (targetMaxLife <= 0) targetMaxLife = 500;
                        isTargetHurt = target.statLife < targetMaxLife;
                    }
                    else if (targetNPCWhoAmI >= 0)
                    {
                        NPC npc = Main.npc[targetNPCWhoAmI];
                        isTargetHurt = npc.life < npc.lifeMax;
                    }

                    // Build Overclock while actively tethered (~35s full charge)
                    if (mediPlayer.OverclockCharge < 100f)
                    {
                        float chargeGain = (isTargetHurt ? (100f / (35f * 60f)) : (100f / (45f * 60f))) * mediPlayer.OverclockChargeRateMultiplier;
                        mediPlayer.OverclockCharge = Math.Min(100f, mediPlayer.OverclockCharge + chargeGain);
                    }

                    SupportEffects.ApplyTetherSocketEffects(player, targetPlayerWhoAmI, targetNPCWhoAmI, socketed);

                    healPulseTimer++;
                    if (healPulseTimer >= HealPulseInterval)
                    {
                        healPulseTimer = 0;

                        if (targetPlayerWhoAmI >= 0)
                        {
                            Player target = Main.player[targetPlayerWhoAmI];
                            int targetMaxLife = Math.Max(target.statLifeMax, target.statLifeMax2);
                            if (targetMaxLife <= 0) targetMaxLife = 500;
                            // Only heal if not already at maximum health
                            if (target.statLife < targetMaxLife)
                            {
                                if (Main.netMode == NetmodeID.SinglePlayer)
                                {
                                    SupportEffects.ServerHealPlayer(target, HealAmount);
                                }
                                else if (Main.netMode == NetmodeID.MultiplayerClient)
                                {
                                    ModPacket packet = ModContent.GetInstance<Augments>().GetPacket();
                                    packet.Write((byte)AugmentPacketType.MediGunHealRequest);
                                    packet.Write((byte)target.whoAmI);
                                    packet.Write(HealAmount);
                                    packet.Send();
                                }
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.10f, Pitch = 0.6f }, target.Center);
                            }
                            SupportEffects.ProcessSocketHealPulse(player, target, null, HealAmount, socketed);
                        }
                        else if (targetNPCWhoAmI >= 0)
                        {
                            NPC npc = Main.npc[targetNPCWhoAmI];
                            if (npc.life < npc.lifeMax)
                            {
                                npc.life = Math.Min(npc.lifeMax, npc.life + HealAmount);
                                npc.HealEffect(HealAmount);
                                if (Main.netMode == NetmodeID.Server)
                                {
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                                }
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.10f, Pitch = 0.6f }, npc.Center);
                            }
                            SupportEffects.ProcessSocketHealPulse(player, null, npc, HealAmount, socketed);
                        }
                    }
                }
                else
                {
                    healPulseTimer = 0;
                }
            }
            else
            {
                // Remote clients read target from synced ai[0]
                int encoded = (int)Projectile.ai[0];
                if (encoded >= 1000 && encoded - 1000 < Main.maxNPCs)
                {
                    targetPlayerWhoAmI = -1;
                    targetNPCWhoAmI = encoded - 1000;
                }
                else if (encoded >= 0 && encoded < Main.maxPlayers)
                {
                    targetPlayerWhoAmI = encoded;
                    targetNPCWhoAmI = -1;
                }
                else
                {
                    targetPlayerWhoAmI = -1;
                    targetNPCWhoAmI = -1;
                }
            }

            // 4. Aim player and projectile position
            Vector2 aimTarget;
            bool targetActive = false;
            if (targetPlayerWhoAmI >= 0 && targetPlayerWhoAmI < Main.maxPlayers && Main.player[targetPlayerWhoAmI].active)
            {
                aimTarget = Main.player[targetPlayerWhoAmI].Center;
                targetActive = true;
            }
            else if (targetNPCWhoAmI >= 0 && targetNPCWhoAmI < Main.maxNPCs && Main.npc[targetNPCWhoAmI].active)
            {
                aimTarget = Main.npc[targetNPCWhoAmI].Center;
                targetActive = true;
            }
            else
            {
                aimTarget = Projectile.owner == Main.myPlayer ? Main.MouseWorld : muzzlePos + player.direction * Vector2.UnitX * 120f;
            }

            Vector2 aimDir = (aimTarget - muzzlePos).SafeNormalize(Vector2.UnitX * player.direction);
            player.ChangeDir(aimDir.X >= 0 ? 1 : -1);
            player.itemRotation = (float)Math.Atan2(aimDir.Y * player.direction, aimDir.X * player.direction);
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.heldProj = Projectile.whoAmI;

            Projectile.Center = muzzlePos + aimDir * 32f;

            // 5. Dust along the beam (Accessories emit signature lighting)
            MediGunVisuals.SpawnSocketBeamDust(player, Projectile.Center, aimTarget, socketed, targetActive);

            if (targetActive)
            {

                // Stream particles forward from gun towards ally
                if (Main.rand.NextBool(2))
                {
                    float t = Main.rand.NextFloat();
                    Vector2 beamPt = Vector2.Lerp(Projectile.Center, aimTarget, t);
                    Dust d = Dust.NewDustDirect(beamPt - new Vector2(2, 2), 4, 4, DustID.GemEmerald);
                    d.noGravity = true;
                    // Velocity points toward ally
                    d.velocity = (aimTarget - beamPt).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 5f);
                    d.scale = Main.rand.NextFloat(0.6f, 0.9f);
                }

                // Healing aura sparkles drifting upward from target
                if (Main.rand.NextBool(3))
                {
                    Vector2 auraOffset = new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-8f, 18f));
                    Dust d = Dust.NewDustDirect(aimTarget + auraOffset, 4, 4, DustID.GreenFairy);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 2.2f));
                    d.scale = Main.rand.NextFloat(0.6f, 0.95f);
                }
            }
            else
            {
                // Searching sparks from gun muzzle
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(2, 2), 4, 4, DustID.Electric);
                    d.noGravity = true;
                    d.velocity = aimDir * Main.rand.NextFloat(3f, 6f) + Main.rand.NextVector2Circular(1f, 1f);
                    d.scale = 0.6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            Vector2 muzzlePos = Projectile.Center;

            Vector2 targetPos;
            bool isLocked = false;
            if (targetPlayerWhoAmI >= 0 && targetPlayerWhoAmI < Main.maxPlayers && Main.player[targetPlayerWhoAmI].active)
            {
                targetPos = Main.player[targetPlayerWhoAmI].Center;
                isLocked = true;
            }
            else if (targetNPCWhoAmI >= 0 && targetNPCWhoAmI < Main.maxNPCs && Main.npc[targetNPCWhoAmI].active)
            {
                targetPos = Main.npc[targetNPCWhoAmI].Center;
                isLocked = true;
            }
            else
            {
                Vector2 fallbackDir = Projectile.owner == Main.myPlayer
                    ? (Main.MouseWorld - muzzlePos).SafeNormalize(Vector2.UnitX * player.direction)
                    : Vector2.UnitX * player.direction;
                targetPos = muzzlePos + fallbackDir * 200f;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 40;

            Vector2 beamDiff = targetPos - muzzlePos;
            float totalLen = Math.Max(1f, beamDiff.Length());
            Vector2 beamDir = beamDiff / totalLen;
            Vector2 normal = new Vector2(-beamDir.Y, beamDir.X);

            // Dynamic Bézier curvature when moving
            Vector2 idealMid = (muzzlePos + targetPos) * 0.5f;
            if (laggedMidPoint == Vector2.Zero || Vector2.DistanceSquared(laggedMidPoint, idealMid) > 800f * 800f)
            {
                laggedMidPoint = idealMid;
            }
            else
            {
                laggedMidPoint = Vector2.Lerp(laggedMidPoint, idealMid, 0.14f);
                Vector2 offset = laggedMidPoint - idealMid;
                float maxBow = Math.Min(45f, totalLen * 0.2f);
                if (offset.Length() > maxBow)
                {
                    laggedMidPoint = idealMid + Vector2.Normalize(offset) * maxBow;
                }
            }

            // Quadratic Bézier control point such that the curve passes through laggedMidPoint at t = 0.5
            Vector2 controlPoint = 2f * laggedMidPoint - idealMid;

            float time = (float)Main.GlobalTimeWrappedHourly;

            // Arrays to store segment data for 3D depth-sorted rendering
            Vector2[] centerPoints = new Vector2[segments + 1];
            Vector2[] ribbon1Points = new Vector2[segments + 1];
            Vector2[] ribbon2Points = new Vector2[segments + 1];
            float[] depth1 = new float[segments + 1];
            float[] depth2 = new float[segments + 1];

            // Speed and wave frequency: POSITIVE waveSpeed travels outward from player toward ally (healing stream)
            // Smooth, calm wave speed (~1.6 rot/sec, ~2.2s per beam transit) with no strobing
            float waveSpeed = 10f;
            float waveFreq = 22f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float invT = 1f - t;

                // 1. Curved Bézier axis
                Vector2 cPt = invT * invT * muzzlePos + 2f * invT * t * controlPoint + t * t * targetPos;
                if (!isLocked)
                {
                    // Subtle search jitter when not locked
                    cPt += normal * ((float)Math.Sin(time * 5f + t * 14f) * 4f);
                }
                centerPoints[i] = cPt;

                // Local curve normal for perpendicular ribbon wrapping
                Vector2 tangent = 2f * invT * (controlPoint - muzzlePos) + 2f * t * (targetPos - controlPoint);
                Vector2 segNormal = tangent.LengthSquared() > 0.001f
                    ? new Vector2(-tangent.Y, tangent.X).SafeNormalize(normal)
                    : normal;

                // 2. 3D Helix rotation angle (flowing outward toward ally)
                float angle1 = time * waveSpeed - t * waveFreq;
                float angle2 = angle1 + MathHelper.Pi; // Ribbon 2 is 180 deg opposite

                // Taper radius at both endpoints
                float envelope = MathHelper.Clamp((float)Math.Sin(t * MathHelper.Pi) * 1.4f, 0.15f, 1f);
                float radius = (isLocked ? 8f : 4f) * envelope;

                // X in screen plane along curve normal, Z along line of sight (depth)
                ribbon1Points[i] = cPt + segNormal * ((float)Math.Sin(angle1) * radius);
                depth1[i] = (float)Math.Cos(angle1);

                ribbon2Points[i] = cPt + segNormal * ((float)Math.Sin(angle2) * radius);
                depth2[i] = (float)Math.Cos(angle2);
            }

            Vector2 origin = new Vector2(0f, 0.5f);

            // ==========================================
            // PASS 1: Draw Ribbon Segments BEHIND Center Line (depth < 0)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                // Ribbon 1 (Neon Cyan) - Behind
                if (depth1[i] < 0 || depth1[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(0, 200, 180, 80) : new Color(40, 140, 220, 50);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2 (Bright Emerald) - Behind
                if (depth2[i] < 0 || depth2[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(80, 200, 110, 80) : new Color(80, 170, 220, 50);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 2: Draw Sleek, Thin Central Beam in the Middle
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                Vector2 segDiff = centerPoints[i] - centerPoints[i - 1];
                float segLen = Math.Max(1f, segDiff.Length());
                float segRot = (float)Math.Atan2(segDiff.Y, segDiff.X);

                // 1. Subtle Outer Glow (Thin: 6px)
                Color outerColor = isLocked
                    ? new Color(30, 220, 130, 45) * 0.7f
                    : new Color(40, 140, 255, 30) * 0.5f;
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 6), outerColor, segRot, origin, 1f, SpriteEffects.None, 0);

                // 2. Focused Bright Core (Thin: 2px)
                Color coreColor = isLocked
                    ? new Color(80, 255, 170, 160)
                    : new Color(90, 190, 255, 120);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 2), coreColor, segRot, origin, 1f, SpriteEffects.None, 0);

                // 3. Central Luminous White Filament (1px)
                Color filamentColor = isLocked
                    ? new Color(240, 255, 250, 220)
                    : new Color(220, 245, 255, 180);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 1), filamentColor, segRot, origin, 1f, SpriteEffects.None, 0);
            }

            // ==========================================
            // PASS 3: Draw Ribbon Segments IN FRONT OF Center Line (depth >= 0)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                // Ribbon 1 (Neon Cyan) - Front
                if (depth1[i] >= 0 || depth1[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(0, 255, 230, 180) : new Color(60, 200, 255, 110);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2 (Bright Emerald) - Front
                if (depth2[i] >= 0 || depth2[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(110, 255, 140, 180) : new Color(100, 220, 255, 110);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 4: Surging Energy Packets (Travelling toward ally)
            // ==========================================
            if (isLocked)
            {
                for (int p = 0; p < 3; p++)
                {
                    // Travelling from muzzle (0) to ally (1)
                    float pulseT = ((float)Main.GlobalTimeWrappedHourly * 1.8f + p * 0.333f) % 1f;
                    float invP = 1f - pulseT;
                    Vector2 pulsePos = invP * invP * muzzlePos + 2f * invP * pulseT * controlPoint + pulseT * pulseT * targetPos;
                    float pulseScale = 0.8f + 0.3f * (float)Math.Sin(pulseT * MathHelper.Pi);

                    Color packetAura = new Color(0, 255, 200, 140);
                    Color packetCore = new Color(250, 255, 255, 240);

                    float packetRot = (float)Main.GlobalTimeWrappedHourly * 6f;
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 7, 7), packetAura, packetRot, new Vector2(3.5f, 3.5f), pulseScale, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 3, 3), packetCore, packetRot, new Vector2(1.5f, 1.5f), pulseScale, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 5: Muzzle Energy Flare
            // ==========================================
            float muzzlePulse = 1f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 16f);
            float muzzleRot = (float)Main.GlobalTimeWrappedHourly * 2.5f;
            Color muzzleColor = isLocked ? new Color(60, 255, 170, 180) : new Color(80, 200, 255, 140);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 10, 10), muzzleColor * 0.7f, muzzleRot, new Vector2(5, 5), muzzlePulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 5, 5), Color.White, muzzleRot + MathHelper.PiOver4, new Vector2(2.5f, 2.5f), muzzlePulse, SpriteEffects.None, 0);

            // ==========================================
            // PASS 6: Holographic Medical Target Reticle
            // ==========================================
            if (isLocked)
            {
                float reticlePulse = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f);
                float reticleRot = (float)Main.GlobalTimeWrappedHourly * 1.5f;
                Color techGreen = new Color(70, 255, 160, 200) * reticlePulse;
                Color coreWhite = Color.White * reticlePulse;

                // Rotating diamond brackets
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 20, 2), techGreen * 0.75f, reticleRot, new Vector2(10, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 20), techGreen * 0.75f, reticleRot, new Vector2(1, 10), 1f, SpriteEffects.None, 0);

                // Solid Central Medical Cross
                // Horizontal bar
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 12, 4), techGreen, 0f, new Vector2(6, 2), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 8, 2), coreWhite, 0f, new Vector2(4, 1), 1f, SpriteEffects.None, 0);
                // Vertical bar
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 4, 12), techGreen, 0f, new Vector2(2, 6), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 8), coreWhite, 0f, new Vector2(1, 4), 1f, SpriteEffects.None, 0);

                // Expanding Radar Ping Ring
                float pingProgress = ((float)Main.GlobalTimeWrappedHourly * 2f) % 1f;
                float pingScale = 0.5f + pingProgress * 1.1f;
                Color pingColor = techGreen * (1f - pingProgress) * 0.5f;
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 18, 2), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(9, 1), pingScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 18), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(1, 9), pingScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            if (Projectile.owner == Main.myPlayer)
            {
                var mediPlayer = Main.player[Projectile.owner].GetModPlayer<MediGunPlayer>();
                mediPlayer.CurrentPatientWhoAmI = -1;
                mediPlayer.CurrentTargetNPCWhoAmI = -1;
            }
        }
    }
}
