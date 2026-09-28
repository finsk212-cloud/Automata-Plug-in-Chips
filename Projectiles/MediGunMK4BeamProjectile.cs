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
    public class MediGunMK4BeamProjectile : ModProjectile
    {
        private const float MaxAcquireRange = 1000f;
        private const float MaxBreakRange = 1200f;
        private const int HealPulseInterval = 15; // 4 pulses per second (16 HP/sec total)
        private const int HealAmount = 4;

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

                int encoded = targetPlayerWhoAmI >= 0 ? targetPlayerWhoAmI : (targetNPCWhoAmI >= 0 ? 1000 + targetNPCWhoAmI : -1);
                if ((int)Projectile.ai[0] != encoded)
                {
                    Projectile.ai[0] = encoded;
                    Projectile.netUpdate = true;
                }

                // 3. Clean Divine Healing Pulses and Overclock building (owner only)
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

                    // Build Overclock while actively tethered (~20s full charge)
                    if (mediPlayer.OverclockCharge < 100f)
                    {
                        float chargeGain = (isTargetHurt ? (100f / (20f * 60f)) : (100f / (30f * 60f))) * mediPlayer.OverclockChargeRateMultiplier;
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
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.08f, Pitch = 0.95f }, target.Center);
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
                                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.08f, Pitch = 0.95f }, npc.Center);
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

                // Pure golden healing flecks streaming forward towards ally
                if (Main.rand.NextBool(2))
                {
                    float t = Main.rand.NextFloat();
                    Vector2 beamPt = Vector2.Lerp(Projectile.Center, aimTarget, t);
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GemDiamond;
                    Dust d = Dust.NewDustDirect(beamPt - new Vector2(2, 2), 4, 4, dustType);
                    d.noGravity = true;
                    d.velocity = (aimTarget - beamPt).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f);
                    d.scale = Main.rand.NextFloat(0.7f, 1.1f);
                }

                // Gentle warm golden sparkles rising from the healed ally
                if (Main.rand.NextBool(2))
                {
                    Vector2 auraOffset = new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-12f, 20f));
                    Dust d = Dust.NewDustDirect(aimTarget + auraOffset, 4, 4, DustID.GoldFlame);
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.4f, 2.6f));
                    d.scale = Main.rand.NextFloat(0.7f, 1.15f);
                }
            }
            else
            {
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(2, 2), 4, 4, DustID.GoldFlame);
                    d.noGravity = true;
                    d.velocity = aimDir * Main.rand.NextFloat(4f, 7.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    d.scale = 0.65f;
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
                targetPos = muzzlePos + fallbackDir * 280f;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 48;

            Vector2 beamDiff = targetPos - muzzlePos;
            float totalLen = Math.Max(1f, beamDiff.Length());
            Vector2 beamDir = beamDiff / totalLen;
            Vector2 normal = new Vector2(-beamDir.Y, beamDir.X);

            // Dynamic Bézier curvature when moving
            Vector2 idealMid = (muzzlePos + targetPos) * 0.5f;
            if (laggedMidPoint == Vector2.Zero || Vector2.DistanceSquared(laggedMidPoint, idealMid) > 1100f * 1100f)
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

            Vector2 controlPoint = 2f * laggedMidPoint - idealMid;
            float time = (float)Main.GlobalTimeWrappedHourly;

            // Clean 2-Strand Double Helix (Same beloved structure, but Premium Divine Gold & Pearlescent White)
            Vector2[] centerPoints = new Vector2[segments + 1];
            Vector2[] ribbon1Points = new Vector2[segments + 1];
            Vector2[] ribbon2Points = new Vector2[segments + 1];
            float[] depth1 = new float[segments + 1];
            float[] depth2 = new float[segments + 1];

            float waveSpeed = 12f;
            float waveFreq = 26f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float invT = 1f - t;

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

                float angle1 = time * waveSpeed - t * waveFreq;
                float angle2 = angle1 + MathHelper.Pi;

                float envelope = MathHelper.Clamp((float)Math.Sin(t * MathHelper.Pi) * 1.4f, 0.15f, 1f);
                float radius = (isLocked ? 10f : 5f) * envelope;

                ribbon1Points[i] = cPt + segNormal * ((float)Math.Sin(angle1) * radius);
                depth1[i] = (float)Math.Cos(angle1);

                ribbon2Points[i] = cPt + segNormal * ((float)Math.Sin(angle2) * radius);
                depth2[i] = (float)Math.Cos(angle2);
            }

            Vector2 origin = new Vector2(0f, 0.5f);

            // ==========================================
            // PASS 1: Draw Ribbon Segments Behind Central Line (depth < 0)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                // Ribbon 1: Radiant Divine Gold (Behind)
                if (depth1[i] < 0 || depth1[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(255, 205, 70, 95) : new Color(230, 180, 70, 50);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2: Pearlescent Angelic White (Behind)
                if (depth2[i] < 0 || depth2[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(255, 250, 225, 95) : new Color(240, 240, 220, 50);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 2: Premium Clean Central Medical Beam (Warm Holy Glow)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                Vector2 segDiff = centerPoints[i] - centerPoints[i - 1];
                float segLen = Math.Max(1f, segDiff.Length());
                float segRot = (float)Math.Atan2(segDiff.Y, segDiff.X);

                // 1. Soft Golden Healing Halo (8px)
                Color outerHalo = isLocked
                    ? new Color(255, 210, 80, 55) * 0.8f
                    : new Color(240, 190, 80, 30) * 0.5f;
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 8), outerHalo, segRot, origin, 1f, SpriteEffects.None, 0);

                // 2. Focused Radiant Gold Core (3px)
                Color coreColor = isLocked
                    ? new Color(255, 235, 140, 190)
                    : new Color(245, 215, 120, 130);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 3), coreColor, segRot, origin, 1f, SpriteEffects.None, 0);

                // 3. Central Pure Luminous White Filament (1px)
                Color filament = isLocked
                    ? Color.White
                    : new Color(255, 255, 245, 200);
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, 1), filament, segRot, origin, 1f, SpriteEffects.None, 0);
            }

            // ==========================================
            // PASS 3: Draw Ribbon Segments In Front Of Central Line (depth >= 0)
            // ==========================================
            for (int i = 1; i <= segments; i++)
            {
                // Ribbon 1: Radiant Divine Gold (Front)
                if (depth1[i] >= 0 || depth1[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(255, 215, 80, 215) : new Color(240, 190, 80, 120);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }

                // Ribbon 2: Pearlescent Angelic White (Front)
                if (depth2[i] >= 0 || depth2[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Color col = isLocked ? new Color(255, 255, 240, 215) : new Color(245, 245, 230, 120);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, 2), col, rRot, origin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 4: Clean Travelling Life Packets (Golden Orbs flowing towards ally)
            // ==========================================
            if (isLocked)
            {
                for (int p = 0; p < 4; p++)
                {
                    float pulseT = ((float)Main.GlobalTimeWrappedHourly * 2.2f + p * 0.25f) % 1f;
                    float invP = 1f - pulseT;
                    Vector2 pulsePos = invP * invP * muzzlePos + 2f * invP * pulseT * controlPoint + pulseT * pulseT * targetPos;
                    float pulseScale = 0.85f + 0.35f * (float)Math.Sin(pulseT * MathHelper.Pi);

                    Color packetAura = (p % 2 == 0) ? new Color(255, 215, 80, 170) : new Color(255, 245, 180, 170);
                    Color packetCore = Color.White;

                    float packetRot = (float)Main.GlobalTimeWrappedHourly * 7f + p * MathHelper.PiOver2;
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 8, 8), packetAura, packetRot, new Vector2(4f, 4f), pulseScale, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 4, 4), packetCore, packetRot, new Vector2(2f, 2f), pulseScale, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 5: Clean Muzzle Energy Flare (Warm Gold & White Star)
            // ==========================================
            float muzzlePulse = 1f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);
            float muzzleRot = (float)Main.GlobalTimeWrappedHourly * 3f;
            Color muzzleColor = isLocked ? new Color(255, 215, 80, 200) : new Color(240, 195, 100, 150);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 13, 13), muzzleColor * 0.75f, muzzleRot, new Vector2(6.5f, 6.5f), muzzlePulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, 7, 7), Color.White, muzzleRot + MathHelper.PiOver4, new Vector2(3.5f, 3.5f), muzzlePulse, SpriteEffects.None, 0);

            // ==========================================
            // PASS 6: Premium Divine Medical Halo Reticle at Target Ally
            // ==========================================
            if (isLocked)
            {
                float reticlePulse = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f);
                float reticleRot = (float)Main.GlobalTimeWrappedHourly * 1.8f;

                Color divineGold = new Color(255, 220, 80, 220) * reticlePulse;
                Color holyWhite = Color.White * reticlePulse;

                // 1. Elegant Rotating Outer Halo Ring (Gold)
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 26, 2), divineGold * 0.8f, reticleRot, new Vector2(13, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 26), divineGold * 0.8f, reticleRot, new Vector2(1, 13), 1f, SpriteEffects.None, 0);

                // 2. Corner Bracket Accents (Clean Medical Framing)
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 20, 2), divineGold * 0.7f, -reticleRot, new Vector2(10, 1), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 20), divineGold * 0.7f, -reticleRot, new Vector2(1, 10), 1f, SpriteEffects.None, 0);

                // 3. Pristine Sovereign Medical Cross (Bold, Pure & Clean)
                // Horizontal bar
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 16, 4), divineGold, 0f, new Vector2(8, 2), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 12, 2), holyWhite, 0f, new Vector2(6, 1), 1f, SpriteEffects.None, 0);
                // Vertical bar
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 4, 16), divineGold, 0f, new Vector2(2, 8), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 12), holyWhite, 0f, new Vector2(1, 6), 1f, SpriteEffects.None, 0);

                // 4. Gentle Warm Healing Pulse Ring
                float pingProgress = ((float)Main.GlobalTimeWrappedHourly * 2.2f) % 1f;
                float pingScale = 0.5f + pingProgress * 1.2f;
                Color pingColor = divineGold * (1f - pingProgress) * 0.55f;
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 24, 2), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(12, 1), pingScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 24), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(1, 12), pingScale, SpriteEffects.None, 0);
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
