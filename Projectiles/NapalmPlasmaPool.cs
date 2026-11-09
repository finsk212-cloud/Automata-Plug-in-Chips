using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Augments.Core;

namespace Augments
{
    public class NapalmPlasmaPool : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Flames;

        public const int PoolWidth = 110; // ~7 tiles wide
        public const int PoolHeight = 32;
        public const int PoolDurationTicks = 240; // 4 seconds

        private bool Settled => Projectile.ai[0] == 1f;
        private bool settledVisualsDone;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceProtocolId = null;
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360; // 6s max fall time before settling
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15; // 4 hits per sec
            Projectile.alpha = 255;
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterCache)
        {
            fallThrough = false; // Catch on wooden platforms and arena platforms
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Settle();
            return false;
        }

        public override void AI()
        {
            if (Settled)
            {
                if (Projectile.width != PoolWidth || Projectile.height != PoolHeight)
                {
                    float bottom = Projectile.position.Y + Projectile.height;
                    float centerX = Projectile.position.X + Projectile.width * 0.5f;
                    Projectile.width = PoolWidth;
                    Projectile.height = PoolHeight;
                    Projectile.position.X = centerX - PoolWidth * 0.5f;
                    Projectile.position.Y = bottom - PoolHeight;
                }

                if (!settledVisualsDone)
                {
                    settledVisualsDone = true;
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.65f, Pitch = 0.10f }, Projectile.Center);
                    SpawnGroundSplash();
                }

                Projectile.velocity = Vector2.Zero;

                // Warm, intense orange/amber ambient glow illuminating terrain and entities across the 7-tile span
                Lighting.AddLight(Projectile.Center, 1.25f, 0.55f, 0.10f);

            // --- 1. Boiling Molten Liquid Bed (Dense carpet of crawling orange plasma hugging the floor) ---
            for (int b = 0; b < 3; b++)
            {
                Vector2 bedPos = new Vector2(
                    Projectile.position.X + Main.rand.NextFloat(Projectile.width),
                    Projectile.position.Y + Projectile.height - Main.rand.NextFloat(10f)
                );
                int bedDustType = Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.OrangeTorch;
                Dust bedDust = Dust.NewDustPerfect(
                    bedPos,
                    bedDustType,
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(-0.3f, 0.1f)),
                    50,
                    default,
                    Main.rand.NextFloat(1.3f, 1.8f)
                );
                bedDust.noGravity = true;
            }

            // --- 2. Roaring Napalm Flame Tongues (Leaping turbulent orange & gold fire) ---
            for (int f = 0; f < 4; f++)
            {
                Vector2 flamePos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.3f, Projectile.height)
                );
                int flameType = Main.rand.NextBool(4) ? DustID.Torch : DustID.OrangeTorch;
                Vector2 flameVel = new Vector2(
                    Main.rand.NextFloat(-0.8f, 0.8f),
                    Main.rand.NextFloat(-3.4f, -1.2f)
                );
                Dust flame = Dust.NewDustPerfect(
                    flamePos,
                    flameType,
                    flameVel,
                    50,
                    default,
                    Main.rand.NextFloat(1.4f, 2.1f)
                );
                flame.noGravity = true;
            }

            // --- 3. Crackling Amber Sparks & High-Heat Micro-Embers (Rising into the draft) ---
            for (int e = 0; e < 2; e++)
            {
                Vector2 sparkPos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.4f, Projectile.height)
                );
                Vector2 sparkVel = new Vector2(
                    Main.rand.NextFloat(-1.5f, 1.5f),
                    Main.rand.NextFloat(-4.2f, -1.8f)
                );
                int sparkType = Main.rand.NextBool(2) ? DustID.SolarFlare : DustID.Flare;
                Dust spark = Dust.NewDustPerfect(
                    sparkPos,
                    sparkType,
                    sparkVel,
                    0,
                    default,
                    Main.rand.NextFloat(0.9f, 1.3f)
                );
                spark.noGravity = true;
            }

            // --- 4. Rising Dark Combustion Smoke Plumes ---
            if (Main.rand.NextBool(2))
            {
                Vector2 smokePos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.3f)
                );
                Vector2 smokeVel = new Vector2(
                    Main.rand.NextFloat(-0.6f, 0.6f),
                    Main.rand.NextFloat(-2.2f, -1.0f)
                );
                Dust smoke = Dust.NewDustPerfect(
                    smokePos,
                    DustID.Smoke,
                    smokeVel,
                    150,
                    new Color(45, 38, 30),
                    Main.rand.NextFloat(1.0f, 1.45f)
                );
                smoke.noGravity = true;
            }

            return;
        }

            // --- Falling State: Molten napalm streaming downward toward ground ---
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                // Nudge up if spawned slightly embedded inside floor tiles
                int safety = 0;
                while (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height) && safety < 16)
                {
                    Projectile.position.Y -= 2f;
                    safety++;
                }
            }

            // Downward gravity acceleration
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.45f, 15f);
            Projectile.velocity.X *= 0.95f;

            // Failsafe: if collision stopped vertical movement, settle
            if (Projectile.velocity.Y == 0f && Projectile.oldVelocity.Y > 0f)
            {
                Settle();
                return;
            }

            // Lighting around falling napalm cluster
            Lighting.AddLight(Projectile.Center, 1.05f, 0.45f, 0.10f);

            // Trailing falling molten napalm particles
            for (int i = 0; i < 2; i++)
            {
                int dType = Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.OrangeTorch;
                Dust drop = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    dType,
                    Projectile.velocity * -0.25f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    50,
                    default,
                    Main.rand.NextFloat(1.2f, 1.7f)
                );
                drop.noGravity = true;
            }

            if (Main.rand.NextBool(3))
            {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.5f, -0.5f)),
                    160,
                    new Color(45, 40, 35),
                    Main.rand.NextFloat(0.9f, 1.3f)
                );
                smoke.noGravity = true;
            }
        }

        private void Settle()
        {
            if (Settled)
                return;

            Projectile.ai[0] = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = PoolDurationTicks;
            Projectile.netUpdate = true;

            float bottom = Projectile.position.Y + Projectile.height;
            float centerX = Projectile.position.X + Projectile.width * 0.5f;
            Projectile.width = PoolWidth;
            Projectile.height = PoolHeight;
            Projectile.position.X = centerX - PoolWidth * 0.5f;
            Projectile.position.Y = bottom - PoolHeight;

            settledVisualsDone = true;
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.65f, Pitch = 0.10f }, Projectile.Center);
            SpawnGroundSplash();
        }

        private void SpawnGroundSplash()
        {
            for (int i = 0; i < 30; i++)
            {
                Vector2 splashVel = new Vector2(
                    Main.rand.NextFloat(-5.5f, 5.5f),
                    Main.rand.NextFloat(-3.2f, -0.6f)
                );
                int dType = Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.OrangeTorch;
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-50f, 50f), 8f),
                    dType,
                    splashVel,
                    60,
                    default,
                    Main.rand.NextFloat(1.5f, 2.3f)
                );
                splash.noGravity = true;
            }

            for (int s = 0; s < 7; s++)
            {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f),
                    DustID.Smoke,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.2f, -0.8f)),
                    150,
                    new Color(50, 45, 40),
                    Main.rand.NextFloat(1.1f, 1.6f)
                );
                smoke.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Inflict Thermite Melt / Molten Slag: ignores 20 defense and +15% ranged damage from all sources
            target.GetGlobalNPC<ThermiteMeltNPC>().ApplyMelt(180); // 3 seconds
            target.AddBuff(BuffID.OnFire3, 240); // 4 seconds of Hellfire
            target.AddBuff(BuffID.Slow, 120);    // 25% slow for 2s

            // Fiery impact sizzle sparks
            for (int i = 0; i < 6; i++)
            {
                Vector2 sparkPos = target.Center + Main.rand.NextVector2Circular(target.width * 0.35f, target.height * 0.35f);
                Dust d = Dust.NewDustPerfect(
                    sparkPos,
                    DustID.OrangeTorch,
                    Main.rand.NextVector2Circular(2.8f, 2.8f) + new Vector2(0f, -1.2f),
                    50,
                    default,
                    Main.rand.NextFloat(1.2f, 1.7f)
                );
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (!Settled)
                return;

            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);

            for (int i = 0; i < 12; i++)
            {
                Vector2 popVel = Main.rand.NextVector2Circular(2.5f, 1.8f) + new Vector2(0f, -1f);
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-45f, 45f), 8f),
                    DustID.Smoke,
                    popVel,
                    180,
                    new Color(40, 35, 30),
                    Main.rand.NextFloat(1.1f, 1.5f)
                );
                d.noGravity = true;
            }
        }
    }
}
