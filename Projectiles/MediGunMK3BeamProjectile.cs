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
    public class MediGunMK3BeamProjectile : ModProjectile
    {
        private const float MaxAcquireRange = 850f;
        private const float MaxBreakRange = 1000f;
        private const int HealPulseInterval = 12; // 5 pulses per second (10 HP/sec total)
        private const int HealAmount = 2;

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

                // 3. Rapid Healing pulses and Overclock building (owner only)
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

                    // Build Overclock while actively tethered (~25s full charge)
                    if (mediPlayer.OverclockCharge < 100f)
                    {
                        float chargeGain = (isTargetHurt ? (100f / (25f * 60f)) : (100f / (35f * 60f))) * mediPlayer.OverclockChargeRateMultiplier;
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
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.07f, Pitch = 1.05f }, target.Center);
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
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.07f, Pitch = 1.05f }, npc.Center);
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
                aimTarget = Projectile.owner == Main.myPlayer ? Main.MouseWorld : muzzlePos + player.direction * Vector2.UnitX * 160f;
            }

            Vector2 aimDir = (aimTarget - muzzlePos).SafeNormalize(Vector2.UnitX * player.direction);
            player.ChangeDir(aimDir.X >= 0 ? 1 : -1);
            player.itemRotation = (float)Math.Atan2(aimDir.Y * player.direction, aimDir.X * player.direction);
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.heldProj = Projectile.whoAmI;

            Projectile.Center = muzzlePos + aimDir * 36f;

            // 5. Dust along the beam (Accessories emit signature lighting)
            MediGunVisuals.SpawnSocketBeamDust(player, Projectile.Center, aimTarget, socketed, targetActive);

            if (targetActive)
            {

                // Stream particles forward from gun towards ally (Enchanted Pink & Dungeon Spirit)
                if (Main.rand.NextBool(2))
                {
                    float t = Main.rand.NextFloat();
                    Vector2 beamPt = Vector2.Lerp(Projectile.Center, aimTarget, t);
                    int dustType = Main.rand.NextBool() ? DustID.Enchanted_Pink : DustID.DungeonSpirit;
                    Dust d = Dust.NewDustDirect(beamPt - new Vector2(2, 2), 4, 4, dustType);
                    d.noGravity = true;
                    d.velocity = (aimTarget - beamPt).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f);
                    d.scale = Main.rand.NextFloat(0.7f, 1.1f);
                }

                // Prismatic sparkles drifting upward from target
                if (Main.rand.NextBool(2))
                {
                    Vector2 auraOffset = new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-12f, 22f));
                    int dustType = Main.rand.NextBool() ? DustID.GemDiamond : DustID.Enchanted_Pink;
                    Dust d = Dust.NewDustDirect(aimTarget + auraOffset, 4, 4, dustType);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.6f, 3.0f));
                    d.scale = Main.rand.NextFloat(0.75f, 1.2f);
                }
            }
            else
            {
                // Searching sparks from gun muzzle
                if (Main.rand.NextBool(2))
                {
                    int dustType = Main.rand.NextBool() ? DustID.Enchanted_Pink : DustID.Electric;
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(2, 2), 4, 4, dustType);
                    d.noGravity = true;
                    d.velocity = aimDir * Main.rand.NextFloat(4f, 8f) + Main.rand.NextVector2Circular(1.4f, 1.4f);
                    d.scale = 0.7f;
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
                targetPos = muzzlePos + fallbackDir * 260f;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 48;

            Vector2 beamDiff = targetPos - muzzlePos;
            float totalLen = Math.Max(1f, beamDiff.Length());
            Vector2 beamDir = beamDiff / totalLen;
            Vector2 normal = new Vector2(-beamDir.Y, beamDir.X);

            // Dynamic Bézier curvature when moving
            Vector2 idealMid = (muzzlePos + targetPos) * 0.5f;
            if (laggedMidPoint == Vector2.Zero || Vector2.DistanceSquared(laggedMidPoint, idealMid) > 1000f * 1000f)
            {
                laggedMidPoint = idealMid;
            }
            else
            {
                laggedMidPoint = Vector2.Lerp(laggedMidPoint, idealMid, 0.14f);
                Vector2 offset = laggedMidPoint - idealMid;
                float maxBow = Math.Min(55f, totalLen * 0.22f);
                if (offset.Length() > maxBow)
                {
                    laggedMidPoint = idealMid + Vector2.Normalize(offset) * maxBow;
                }
            }

            // Quadratic Bézier control point
            Vector2 controlPoint = 2f * laggedMidPoint - idealMid;

            float time = (float)Main.GlobalTimeWrappedHourly;

            // Arrays for segment points and depth
            Vector2[] centerPoints = new Vector2[segments + 1];
            Vector2[] ribbon1Points = new Vector2[segments + 1];
            Vector2[] ribbon2Points = new Vector2[segments + 1];
            float[] depth1 = new float[segments + 1];
            float[] depth2 = new float[segments + 1];

            // Wave speed and frequency
            float waveSpeed = 12f;
            float waveFreq = 26f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float invT = 1f - t;

                // 1. Curved Bézier axis
                Vector2 cPt = invT * invT * muzzlePos + 2f * invT * t * controlPoint + t * t * targetPos;
                if (!isLocked)
                {
                    cPt += normal * ((float)Math.Sin(time * 6f + t * 16f) * 4f);
                }
                centerPoints[i] = cPt;

                Vector2 tangent = 2f * invT * (controlPoint - muzzlePos) + 2f * t * (targetPos - controlPoint);
                Vector2 segNormal = tangent.LengthSquared() > 0.001f
                    ? new Vector2(-tangent.Y, tangent.X).SafeNormalize(normal)
                    : normal;

                // 2. 3D Helix rotation angle
                float angle1 = time * waveSpeed - t * waveFreq;
                float angle2 = angle1 + MathHelper.Pi;

                // Taper radius
                float envelope = MathHelper.Clamp((float)Math.Sin(t * MathHelper.Pi) * 1.4f, 0.15f, 1f);
                float radius = (isLocked ? 10.5f : 5f) * envelope;

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
                // Ribbon 1 (Celestial Magenta) - Behind
                if (depth1[i] < 0 || depth1[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(240, 50, 180, 95) : new Color(210, 60, 160, 50);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2 (Prismatic Cyan) - Behind
                if (depth2[i] < 0 || depth2[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(0, 240, 245, 95) : new Color(60, 180, 240, 50);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 2: Draw Sleek Central Beam in the Middle
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                Vector2 segDiff = centerPoints[i] - centerPoints[i - 1];
                float segLen = Math.Max(1f, segDiff.Length());
                float segRot = (float)Math.Atan2(segDiff.Y, segDiff.X);

                // 1. Outer Astral Glow (8px)
                Color outerColor = isLocked
                    ? new Color(220, 80, 255, 60) * 0.8f
                    : new Color(120, 100, 255, 35) * 0.5f;
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 8), outerColor, segRot, origin, 1f, SpriteEffects.None, 0);

                // 2. Focused Bright Core (3px)
                Color coreColor = isLocked
                    ? new Color(255, 170, 240, 195)
                    : new Color(170, 220, 255, 135);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 3), coreColor, segRot, origin, 1f, SpriteEffects.None, 0);

                // 3. Central Luminous White Filament (1px)
                Color filamentColor = isLocked
                    ? new Color(255, 255, 255, 250)
                    : new Color(240, 250, 255, 200);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 1), filamentColor, segRot, origin, 1f, SpriteEffects.None, 0);
            }

            // ==========================================
            // PASS 3: Draw Ribbon Segments IN FRONT OF Center Line (depth >= 0)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                // Ribbon 1 (Celestial Magenta) - Front
                if (depth1[i] >= 0 || depth1[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(255, 65, 200, 215) : new Color(230, 90, 180, 125);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2 (Prismatic Cyan) - Front
                if (depth2[i] >= 0 || depth2[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(0, 255, 240, 215) : new Color(90, 220, 255, 125);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 4: Surging Energy Packets (5 travelling packets with Prismatic flow)
            // ==========================================
            if (isLocked)
            {
                for (int p = 0; p < 5; p++)
                {
                    float pulseT = ((float)Main.GlobalTimeWrappedHourly * 2.5f + p * 0.2f) % 1f;
                    float invP = 1f - pulseT;
                    Vector2 pulsePos = invP * invP * muzzlePos + 2f * invP * pulseT * controlPoint + pulseT * pulseT * targetPos;
                    float pulseScale = 0.9f + 0.35f * (float)Math.Sin(pulseT * MathHelper.Pi);

                    Color packetAura = (p % 2 == 0) ? new Color(255, 70, 210, 175) : new Color(0, 250, 240, 175);
                    Color packetCore = new Color(255, 255, 255, 250);

                    float packetRot = (float)Main.GlobalTimeWrappedHourly * 8f + p * MathHelper.PiOver4;
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 8, 8), packetAura, packetRot, new Vector2(4f, 4f), pulseScale, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 4, 4), packetCore, packetRot, new Vector2(2f, 2f), pulseScale, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 5: Muzzle Prismatic Flare
            // ==========================================
            float muzzlePulse = 1f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f);
            float muzzleRot = (float)Main.GlobalTimeWrappedHourly * 3.5f;
            Color muzzleColor = isLocked ? new Color(255, 80, 220, 200) : new Color(130, 220, 255, 160);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 14, 14), muzzleColor * 0.75f, muzzleRot, new Vector2(7, 7), muzzlePulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 8, 8), new Color(0, 255, 240, 220), muzzleRot + MathHelper.PiOver4, new Vector2(4, 4), muzzlePulse, SpriteEffects.None, 0);

            // ==========================================
            // PASS 6: Tri-Ring Holographic Medical Reticle
            // ==========================================
            if (isLocked)
            {
                float reticlePulse = 1f + 0.14f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
                float reticleRot = (float)Main.GlobalTimeWrappedHourly * 2f;
                Color techMagenta = new Color(255, 75, 210, 220) * reticlePulse;
                Color techCyan = new Color(0, 250, 255, 220) * reticlePulse;
                Color techGold = new Color(255, 225, 90, 220) * reticlePulse;
                Color coreWhite = Color.White * reticlePulse;

                // Outer rotating diamond brackets (Magenta)
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 28, 2), techMagenta * 0.85f, reticleRot, new Vector2(14, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 28), techMagenta * 0.85f, reticleRot, new Vector2(1, 14), 1f, SpriteEffects.None, 0);

                // Mid rotating square brackets (Cyan) counter-rotating
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 22, 2), techCyan * 0.8f, -reticleRot * 1.3f, new Vector2(11, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 22), techCyan * 0.8f, -reticleRot * 1.3f, new Vector2(1, 11), 1f, SpriteEffects.None, 0);

                // Inner rotating ring brackets (Gold)
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 16, 2), techGold * 0.75f, reticleRot * 2f, new Vector2(8, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 16), techGold * 0.75f, reticleRot * 2f, new Vector2(1, 8), 1f, SpriteEffects.None, 0);

                // Central Radiant Medical Cross
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 16, 4), techMagenta, 0f, new Vector2(8, 2), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 12, 2), coreWhite, 0f, new Vector2(6, 1), 1f, SpriteEffects.None, 0);

                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 4, 16), techMagenta, 0f, new Vector2(2, 8), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 12), coreWhite, 0f, new Vector2(1, 6), 1f, SpriteEffects.None, 0);

                // Expanding Dual Radar Ping Rings
                float pingProgress = ((float)Main.GlobalTimeWrappedHourly * 2.4f) % 1f;
                float pingScale = 0.5f + pingProgress * 1.3f;
                Color pingColor = techCyan * (1f - pingProgress) * 0.6f;
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 26, 2), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(13, 1), pingScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 26), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(1, 13), pingScale, SpriteEffects.None, 0);
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
