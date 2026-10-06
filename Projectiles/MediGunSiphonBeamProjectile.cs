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
    public class MediGunSiphonBeamProjectile : ModProjectile
    {
        private int targetNPCWhoAmI = -1;
        private int burstShotsRemaining = 0;
        private int burstShotDelayTimer = 0;
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
            if (!player.active || player.dead || player.noItems || player.CCed)
            {
                Projectile.Kill();
                return;
            }

            // Local owner controls channeling via holding Right-Click with closed inventory
            if (Projectile.owner == Main.myPlayer)
            {
                if (!Main.mouseRight || Main.playerInventory || player.mouseInterface)
                {
                    Projectile.Kill();
                    return;
                }

                player.channel = true;
                player.itemTime = 2;
                player.itemAnimation = 2;
                player.heldProj = Projectile.whoAmI;
            }

            // Eliminate duplicate siphon projectiles for this owner
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

            int tier = (int)Projectile.ai[1];
            if (tier <= 0) tier = 1;

            float maxAcquireRange = 650f;
            float maxBreakRange = 750f;
            if (tier == 2)
            {
                maxAcquireRange = 750f;
                maxBreakRange = 875f;
            }
            else if (tier == 3)
            {
                maxAcquireRange = 850f;
                maxBreakRange = 1000f;
            }
            else if (tier >= 4)
            {
                maxAcquireRange = 950f;
                maxBreakRange = 1150f;
            }

            // ONLY local owner resolves targeting, network syncs, and applies damage
            if (Projectile.owner == Main.myPlayer)
            {
                // 1. Maintain or drop current target
                if (targetNPCWhoAmI >= 0)
                {
                    if (targetNPCWhoAmI >= Main.maxNPCs)
                    {
                        targetNPCWhoAmI = -1;
                    }
                    else
                    {
                        NPC target = Main.npc[targetNPCWhoAmI];
                        bool isValid = target.active && target.life > 0 &&
                            ((!target.friendly && target.CanBeChasedBy()) || target.type == NPCID.TargetDummy) &&
                            Vector2.Distance(muzzlePos, target.Center) <= maxBreakRange &&
                            SupportEffects.CanBeamPassLine(muzzlePos, target.Center);

                        if (!isValid)
                        {
                            targetNPCWhoAmI = -1;
                        }
                    }
                }

                // 2. If no target locked, acquire closest eligible hostile NPC near cursor
                if (targetNPCWhoAmI < 0)
                {
                    float bestScore = float.MaxValue;
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC candidate = Main.npc[i];
                        if (!candidate.active || candidate.life <= 0)
                            continue;

                        bool isValid = (!candidate.friendly && candidate.CanBeChasedBy()) || candidate.type == NPCID.TargetDummy;
                        if (!isValid)
                            continue;

                        if (Vector2.Distance(muzzlePos, candidate.Center) > maxAcquireRange)
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

                // Sync encoded target across network
                if ((int)Projectile.ai[0] != targetNPCWhoAmI)
                {
                    Projectile.ai[0] = targetNPCWhoAmI;
                    Projectile.netUpdate = true;
                }

                // Update ModPlayer state
                var mediPlayer = player.GetModPlayer<MediGunPlayer>();
                mediPlayer.CurrentEnemySiphonWhoAmI = targetNPCWhoAmI;

                if (targetNPCWhoAmI >= 0)
                {
                    // Build Overclock while siphoning life from enemies (scales with MK tier)
                    float chargeSeconds = tier == 4 ? 20f : (tier == 3 ? 25f : (tier == 2 ? 30f : 35f));
                    if (mediPlayer.OverclockCharge < 100f)
                    {
                        float chargeGain = (100f / (chargeSeconds * 60f)) * mediPlayer.OverclockChargeRateMultiplier;
                        mediPlayer.OverclockCharge = Math.Min(100f, mediPlayer.OverclockCharge + chargeGain);
                    }

                    NPC target = Main.npc[targetNPCWhoAmI];

                    if (tier <= 2)
                    {
                        // Continuous DPS mode (MK1: 6 DMG/s, MK2: 20 DMG/s)
                        if (mediPlayer.SiphonContinuousCooldown <= 0)
                        {
                            mediPlayer.SiphonContinuousCooldown = 60; // 1 second cooldown
                            int dmgAmount = tier == 2 ? 20 : 6;
                            DealSiphonDamage(player, target, dmgAmount);
                        }
                    }
                    else
                    {
                        // Burst mode (MK3: 3x 20 DMG every 1.5s, MK4: 3x 50 DMG every 1.5s)
                        int burstDmg = tier >= 4 ? 50 : 20;

                        if (burstShotsRemaining > 0)
                        {
                            burstShotDelayTimer++;
                            if (burstShotDelayTimer >= 7) // ~0.11s between burst ticks
                            {
                                burstShotDelayTimer = 0;
                                burstShotsRemaining--;
                                DealSiphonDamage(player, target, burstDmg);

                                // When the final (3rd) shot fires, lock in the 1.5s (90 ticks) cooldown on player
                                if (burstShotsRemaining == 0)
                                {
                                    mediPlayer.SiphonBurstCooldown = 90;
                                }
                            }
                        }
                        else if (mediPlayer.SiphonBurstCooldown <= 0)
                        {
                            // Cooldown ready! Trigger 3-burst sequence
                            burstShotsRemaining = 3;
                            burstShotDelayTimer = 7; // Immediately fire 1st shot of the burst
                        }
                    }
                }
            }
            else
            {
                targetNPCWhoAmI = (int)Projectile.ai[0];
                if (targetNPCWhoAmI < 0 || targetNPCWhoAmI >= Main.maxNPCs || !Main.npc[targetNPCWhoAmI].active)
                {
                    targetNPCWhoAmI = -1;
                }
            }

            // Aim player and projectile position
            Vector2 aimTarget;
            bool targetActive = false;
            if (targetNPCWhoAmI >= 0 && targetNPCWhoAmI < Main.maxNPCs && Main.npc[targetNPCWhoAmI].active)
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

            // Dynamic environmental lighting along the beam
            float lightPower = tier switch { 4 => 1.4f, 3 => 1.0f, 2 => 0.65f, _ => 0.35f };
            Lighting.AddLight(Projectile.Center, 0.8f * lightPower, 0.1f * lightPower, 0.12f * lightPower);
            if (targetActive)
            {
                Lighting.AddLight(aimTarget, 0.9f * lightPower, 0.12f * lightPower, 0.15f * lightPower);
                Vector2 mid = (Projectile.Center + aimTarget) * 0.5f;
                Lighting.AddLight(mid, 0.7f * lightPower, 0.08f * lightPower, 0.10f * lightPower);
            }

            // Dust along the beam (accessories emit signature lighting if socketed)
            MediGunVisuals.SpawnSocketBeamDust(player, Projectile.Center, aimTarget, socketed, targetActive);

            if (targetActive)
            {
                // Particles streaming forward from gun towards enemy
                int streamCount = tier switch { 4 => 3, 3 => 2, 2 => 1, _ => (Main.rand.NextBool(2) ? 1 : 0) };
                for (int s = 0; s < streamCount; s++)
                {
                    float t = Main.rand.NextFloat();
                    Vector2 beamPt = Vector2.Lerp(Projectile.Center, aimTarget, t);
                    int dType = (tier >= 3 && Main.rand.NextBool(3)) ? DustID.LifeDrain : DustID.CrimsonTorch;
                    Dust d = Dust.NewDustDirect(beamPt - new Vector2(2, 2), 4, 4, dType);
                    d.noGravity = true;
                    // Velocity points FORWARD towards the enemy!
                    d.velocity = (aimTarget - beamPt).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 5.5f + tier * 0.8f);
                    d.scale = Main.rand.NextFloat(0.6f + tier * 0.1f, 0.95f + tier * 0.15f);
                }

                // Leeching sparkles erupting from the enemy
                int leechCount = tier >= 3 ? 2 : 1;
                for (int l = 0; l < leechCount; l++)
                {
                    if (Main.rand.NextBool(3 - Math.Min(2, tier - 1)))
                    {
                        Vector2 auraOffset = new Vector2(Main.rand.NextFloat(-14f - tier * 2f, 14f + tier * 2f), Main.rand.NextFloat(-8f - tier * 2f, 18f + tier * 2f));
                        int dustId = (tier == 4 && Main.rand.NextBool(2)) ? DustID.LifeDrain : DustID.Blood;
                        Dust d = Dust.NewDustDirect(aimTarget + auraOffset, 4, 4, dustId);
                        d.noGravity = true;
                        d.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2f + tier * 0.4f));
                        d.scale = Main.rand.NextFloat(0.7f + tier * 0.1f, 1.1f + tier * 0.15f);
                    }
                }
            }
            else
            {
                // Searching red sparks from gun muzzle
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(2, 2), 4, 4, DustID.CrimsonTorch);
                    d.noGravity = true;
                    d.velocity = aimDir * Main.rand.NextFloat(3f, 6f + tier) + Main.rand.NextVector2Circular(1f, 1f);
                    d.scale = 0.6f + tier * 0.1f;
                }
            }
        }

        private void DealSiphonDamage(Player player, NPC target, int amount)
        {
            if (!target.active || target.life <= 0)
                return;

            int hitDir = player.direction;
            if (target.Center.X != player.Center.X)
                hitDir = target.Center.X > player.Center.X ? 1 : -1;

            target.SimpleStrikeNPC(amount, hitDir, false, 0f, DamageClass.Generic, false);

            if (player.whoAmI == Main.myPlayer)
            {
                string weaponName = player.HeldItem?.Name ?? "Medi Gun";
                AugmentDamageTracker.RecordWeaponHit(weaponName, amount, false, AugmentClass.Support);
            }

            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.28f, Pitch = 0.35f }, target.Center);

            for (int d = 0; d < 8; d++)
            {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.CrimsonTorch, Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f), 80, default, 1.3f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            Vector2 muzzlePos = Projectile.Center;

            int tier = (int)Projectile.ai[1];
            if (tier <= 0) tier = 1;

            Vector2 targetPos;
            bool isLocked = false;
            if (targetNPCWhoAmI >= 0 && targetNPCWhoAmI < Main.maxNPCs && Main.npc[targetNPCWhoAmI].active)
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

            // Subtle, sleek Bézier curvature (taut and laser-focused)
            Vector2 idealMid = (muzzlePos + targetPos) * 0.5f;
            if (laggedMidPoint == Vector2.Zero || Vector2.DistanceSquared(laggedMidPoint, idealMid) > 800f * 800f)
            {
                laggedMidPoint = idealMid;
            }
            else
            {
                laggedMidPoint = Vector2.Lerp(laggedMidPoint, idealMid, 0.16f);
                Vector2 offset = laggedMidPoint - idealMid;
                float maxBow = Math.Min(18f, totalLen * 0.08f);
                if (offset.Length() > maxBow)
                {
                    laggedMidPoint = idealMid + Vector2.Normalize(offset) * maxBow;
                }
            }

            Vector2 controlPoint = 2f * laggedMidPoint - idealMid;
            float time = (float)Main.GlobalTimeWrappedHourly;

            // Tier-scaled clean dimensions: subtle and laser-focused
            int glowWidth = tier switch { 4 => 8, 3 => 6, 2 => 5, _ => 4 };
            int coreWidth = tier >= 4 ? 3 : 2;
            int filamentWidth = 1;
            float ribbonRadius = tier switch { 4 => 6.5f, 3 => 5.5f, 2 => 4.5f, _ => 3.5f };
            int ribbonThickness = tier >= 3 ? 2 : 1;

            Vector2[] centerPoints = new Vector2[segments + 1];
            Vector2[] ribbon1Points = new Vector2[segments + 1];
            Vector2[] ribbon2Points = new Vector2[segments + 1];
            float[] depth1 = new float[segments + 1];
            float[] depth2 = new float[segments + 1];

            float waveSpeed = 9f + tier * 1.5f;
            float waveFreq = 22f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float invT = 1f - t;

                Vector2 cPt = invT * invT * muzzlePos + 2f * invT * t * controlPoint + t * t * targetPos;
                if (!isLocked)
                {
                    cPt += normal * ((float)Math.Sin(time * 5f + t * 14f) * 2.5f);
                }
                centerPoints[i] = cPt;

                Vector2 tangent = 2f * invT * (controlPoint - muzzlePos) + 2f * t * (targetPos - controlPoint);
                Vector2 segNormal = tangent.LengthSquared() > 0.001f
                    ? new Vector2(-tangent.Y, tangent.X).SafeNormalize(normal)
                    : normal;

                float angle1 = time * waveSpeed - t * waveFreq;
                float angle2 = angle1 + MathHelper.Pi;

                float envelope = MathHelper.Clamp((float)Math.Sin(t * MathHelper.Pi) * 1.4f, 0.15f, 1f);
                float radius = (isLocked ? ribbonRadius : ribbonRadius * 0.5f) * envelope;

                ribbon1Points[i] = cPt + segNormal * ((float)Math.Sin(angle1) * radius);
                depth1[i] = (float)Math.Cos(angle1);

                ribbon2Points[i] = cPt + segNormal * ((float)Math.Sin(angle2) * radius);
                depth2[i] = (float)Math.Cos(angle2);
            }

            Vector2 ribbonOrigin = new Vector2(0f, ribbonThickness * 0.5f);

            // ==========================================
            // PASS 1: Draw Helical Ribbons BEHIND (depth < 0)
            // ==========================================
            Color rib1Back = isLocked
                ? new Color(220, 30, 55, 75)
                : new Color(170, 20, 40, 40);
            Color rib2Back = isLocked
                ? new Color(180, 20, 45, 65)
                : new Color(140, 15, 30, 35);

            for (int i = 1; i <= segments; i++)
            {
                if (depth1[i] < 0 || depth1[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, ribbonThickness), rib1Back, rRot, ribbonOrigin, 1f, SpriteEffects.None, 0);
                }
                if (depth2[i] < 0 || depth2[i - 1] < 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, ribbonThickness), rib2Back, rRot, ribbonOrigin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 2: Perfectly Centered Central Beam
            // ==========================================
            Color outerColor = isLocked
                ? (tier switch
                {
                    4 => new Color(255, 30, 55, 45),
                    3 => new Color(255, 25, 50, 38),
                    2 => new Color(245, 20, 45, 30),
                    _ => new Color(230, 20, 40, 24)
                })
                : new Color(180, 20, 35, 18);

            Color coreColor = isLocked
                ? (tier switch
                {
                    4 => new Color(255, 80, 95, 230),
                    3 => new Color(255, 70, 85, 210),
                    2 => new Color(245, 60, 75, 190),
                    _ => new Color(230, 50, 65, 170)
                })
                : new Color(200, 45, 55, 120);

            Color filamentColor = isLocked
                ? new Color(255, 240, 245, 240)
                : new Color(245, 210, 220, 180);

            Vector2 glowOrigin = new Vector2(0f, glowWidth * 0.5f);
            Vector2 coreOrigin = new Vector2(0f, coreWidth * 0.5f);
            Vector2 filamentOrigin = new Vector2(0f, filamentWidth * 0.5f);

            for (int i = 1; i <= segments; i++)
            {
                Vector2 segDiff = centerPoints[i] - centerPoints[i - 1];
                float segLen = Math.Max(1f, segDiff.Length());
                float segRot = (float)Math.Atan2(segDiff.Y, segDiff.X);

                // 1. Soft Outer Crimson Bloom (Perfect Centered Origin)
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, glowWidth), outerColor, segRot, glowOrigin, 1f, SpriteEffects.None, 0);

                // 2. Focused Bright Core (Perfect Centered Origin)
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, coreWidth), coreColor, segRot, coreOrigin, 1f, SpriteEffects.None, 0);

                // 3. Central Luminous White-Rose Filament (Perfect Centered Origin)
                Main.EntitySpriteDraw(pixel, centerPoints[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)segLen + 1, filamentWidth), filamentColor, segRot, filamentOrigin, 1f, SpriteEffects.None, 0);
            }

            // ==========================================
            // PASS 3: Draw Helical Ribbons IN FRONT (depth >= 0)
            // ==========================================
            Color rib1Front = isLocked
                ? new Color(255, 45, 75, 180)
                : new Color(210, 30, 50, 100);
            Color rib2Front = isLocked
                ? new Color(220, 25, 55, 160)
                : new Color(180, 20, 40, 90);

            for (int i = 1; i <= segments; i++)
            {
                if (depth1[i] >= 0 || depth1[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon1Points[i] - ribbon1Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Main.EntitySpriteDraw(pixel, ribbon1Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, ribbonThickness), rib1Front, rRot, ribbonOrigin, 1f, SpriteEffects.None, 0);
                }
                if (depth2[i] >= 0 || depth2[i - 1] >= 0)
                {
                    Vector2 rDiff = ribbon2Points[i] - ribbon2Points[i - 1];
                    float rLen = Math.Max(1f, rDiff.Length());
                    float rRot = (float)Math.Atan2(rDiff.Y, rDiff.X);
                    Main.EntitySpriteDraw(pixel, ribbon2Points[i - 1] - Main.screenPosition, new Rectangle(0, 0, (int)rLen + 1, ribbonThickness), rib2Front, rRot, ribbonOrigin, 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 4: Subtle Energy Pulses (Travelling forward toward enemy)
            // ==========================================
            if (isLocked)
            {
                int pCount = tier >= 3 ? 3 : 2;
                for (int p = 0; p < pCount; p++)
                {
                    // Travelling from muzzle (t = 0) outward to enemy (t = 1)
                    float pulseT = (((float)Main.GlobalTimeWrappedHourly * 2.2f + p * (1f / pCount)) % 1f);
                    float invP = 1f - pulseT;
                    Vector2 pulsePos = invP * invP * muzzlePos + 2f * invP * pulseT * controlPoint + pulseT * pulseT * targetPos;

                    Color packetAura = new Color(255, 50, 75, 160);
                    Color packetCore = new Color(255, 245, 248, 230);

                    float packetRot = (float)Main.GlobalTimeWrappedHourly * 5f;
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 4, 4), packetAura, packetRot, new Vector2(2f, 2f), 1f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(pixel, pulsePos - Main.screenPosition, new Rectangle(0, 0, 2, 2), packetCore, packetRot, new Vector2(1f, 1f), 1f, SpriteEffects.None, 0);
                }
            }

            // ==========================================
            // PASS 5: Clean Muzzle Flare
            // ==========================================
            float muzzlePulse = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 16f);
            float muzzleRot = (float)Main.GlobalTimeWrappedHourly * 2f;
            Color muzzleColor = isLocked ? new Color(255, 45, 65, 180) : new Color(210, 30, 45, 130);
            int mSize = 8 + tier * 2;
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, mSize, mSize), muzzleColor * 0.7f, muzzleRot, new Vector2(mSize * 0.5f, mSize * 0.5f), muzzlePulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, muzzlePos - Main.screenPosition, new Rectangle(0, 0, Math.Max(2, mSize / 2), Math.Max(2, mSize / 2)), Color.White, muzzleRot + MathHelper.PiOver4, new Vector2(Math.Max(1, mSize / 4), Math.Max(1, mSize / 4)), muzzlePulse, SpriteEffects.None, 0);

            // ==========================================
            // PASS 6: Clean Holographic Combat Target Reticle
            // ==========================================
            if (isLocked)
            {
                float reticlePulse = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f);
                float reticleRot = (float)Main.GlobalTimeWrappedHourly * 1.5f;
                Color combatRed = new Color(255, 50, 50, 210) * reticlePulse;
                Color coreWhite = Color.White * reticlePulse;

                int reticleSize = 18 + tier * 2;

                // Diamond brackets
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, reticleSize, 2), combatRed * 0.75f, reticleRot, new Vector2(reticleSize * 0.5f, 1f), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, reticleSize), combatRed * 0.75f, reticleRot, new Vector2(1f, reticleSize * 0.5f), 1f, SpriteEffects.None, 0);

                // Combat crosshair ticks
                int crosshairLen = 10 + tier;
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, crosshairLen, 2), combatRed, 0f, new Vector2(crosshairLen * 0.5f, 1f), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, crosshairLen), combatRed, 0f, new Vector2(1f, crosshairLen * 0.5f), 1f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 2, 2), coreWhite, 0f, new Vector2(1f, 1f), 1f, SpriteEffects.None, 0);

                // Expanding Lock Ping Ring
                float pingProgress = ((float)Main.GlobalTimeWrappedHourly * 2f) % 1f;
                float pingScale = 0.5f + pingProgress * 1f;
                Color pingColor = combatRed * (1f - pingProgress) * 0.5f;
                int pingDiam = reticleSize + 2;
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, pingDiam, 1), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(pingDiam * 0.5f, 0.5f), pingScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, targetPos - Main.screenPosition, new Rectangle(0, 0, 1, pingDiam), pingColor, reticleRot + MathHelper.PiOver4, new Vector2(0.5f, pingDiam * 0.5f), pingScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            if (Projectile.owner == Main.myPlayer)
            {
                var mediPlayer = Main.player[Projectile.owner].GetModPlayer<MediGunPlayer>();
                mediPlayer.CurrentEnemySiphonWhoAmI = -1;

                // If player released right-click during or right after a burst, ensure the 1.5s cooldown is locked in
                if (burstShotsRemaining < 3)
                {
                    mediPlayer.SiphonBurstCooldown = Math.Max(mediPlayer.SiphonBurstCooldown, 90);
                }
            }
        }
    }
}
