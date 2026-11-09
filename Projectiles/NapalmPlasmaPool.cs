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

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceProtocolId = null;
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 72;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180; // 3 seconds
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20; // 3 hits per sec
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;

            // Rich fiery orange/amber ambient glow illuminating terrain and entities
            Lighting.AddLight(Projectile.Center, 1.05f, 0.48f, 0.08f);

            // Initial roaring napalm splash burst on spawn
            if (Projectile.timeLeft == 180)
            {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = 0.15f }, Projectile.Center);

                for (int i = 0; i < 22; i++)
                {
                    Vector2 splashVel = new Vector2(
                        Main.rand.NextFloat(-4.5f, 4.5f),
                        Main.rand.NextFloat(-2.8f, -0.6f)
                    );
                    int dType = Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.OrangeTorch;
                    Dust splash = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-28f, 28f), 6f),
                        dType,
                        splashVel,
                        70,
                        default,
                        Main.rand.NextFloat(1.4f, 2.1f)
                    );
                    splash.noGravity = true;
                }

                // Initial burst of smoke
                for (int s = 0; s < 5; s++)
                {
                    Dust smoke = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                        DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-2.0f, -0.8f)),
                        150,
                        new Color(50, 45, 40),
                        Main.rand.NextFloat(1.0f, 1.4f)
                    );
                    smoke.noGravity = true;
                }
            }

            // --- 1. Boiling Molten Liquid Bed (Dense bottom layer of sizzling orange plasma) ---
            for (int b = 0; b < 2; b++)
            {
                Vector2 bedPos = new Vector2(
                    Projectile.position.X + Main.rand.NextFloat(Projectile.width),
                    Projectile.position.Y + Projectile.height - Main.rand.NextFloat(8f)
                );
                int bedDustType = Main.rand.NextBool(3) ? DustID.SolarFlare : DustID.OrangeTorch;
                Dust bedDust = Dust.NewDustPerfect(
                    bedPos,
                    bedDustType,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.3f, 0.1f)),
                    60,
                    default,
                    Main.rand.NextFloat(1.2f, 1.7f)
                );
                bedDust.noGravity = true;
            }

            // --- 2. Roaring Napalm Flame Tongues (Dynamic leaping orange/gold fire) ---
            for (int f = 0; f < 3; f++)
            {
                Vector2 flamePos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.4f, Projectile.height)
                );
                int flameType = Main.rand.NextBool(4) ? DustID.Torch : DustID.OrangeTorch;
                Vector2 flameVel = new Vector2(
                    Main.rand.NextFloat(-0.7f, 0.7f),
                    Main.rand.NextFloat(-3.2f, -1.2f)
                );
                Dust flame = Dust.NewDustPerfect(
                    flamePos,
                    flameType,
                    flameVel,
                    60,
                    default,
                    Main.rand.NextFloat(1.4f, 2.0f)
                );
                flame.noGravity = true;
            }

            // --- 3. Crackling Amber Sparks & High-Heat Micro-Embers (Rising & swirling) ---
            if (Main.rand.NextBool(2))
            {
                Vector2 sparkPos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.5f, Projectile.height)
                );
                Vector2 sparkVel = new Vector2(
                    Main.rand.NextFloat(-1.4f, 1.4f),
                    Main.rand.NextFloat(-4.0f, -1.8f)
                );
                int sparkType = Main.rand.NextBool(2) ? DustID.SolarFlare : DustID.Flare;
                Dust spark = Dust.NewDustPerfect(
                    sparkPos,
                    sparkType,
                    sparkVel,
                    0,
                    default,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                spark.noGravity = true;
            }

            // --- 4. Rising Dark Combustion Smoke Plumes (Billowing heat draft) ---
            if (Main.rand.NextBool(3))
            {
                Vector2 smokePos = Projectile.position + new Vector2(
                    Main.rand.NextFloat(Projectile.width),
                    Main.rand.NextFloat(Projectile.height * 0.3f)
                );
                Vector2 smokeVel = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-2.0f, -0.9f)
                );
                Dust smoke = Dust.NewDustPerfect(
                    smokePos,
                    DustID.Smoke,
                    smokeVel,
                    160,
                    new Color(45, 38, 30),
                    Main.rand.NextFloat(0.9f, 1.35f)
                );
                smoke.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire3, 180); // Hellfire for 3s
            target.AddBuff(BuffID.Slow, 120);    // 25% slow for 2s

            // Fiery impact sizzle sparks
            for (int i = 0; i < 5; i++)
            {
                Vector2 sparkPos = target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f);
                Dust d = Dust.NewDustPerfect(
                    sparkPos,
                    DustID.OrangeTorch,
                    Main.rand.NextVector2Circular(2.5f, 2.5f) + new Vector2(0f, -1.2f),
                    60,
                    default,
                    Main.rand.NextFloat(1.1f, 1.6f)
                );
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Sizzle and pop when the pool finally extinguishes
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);

            for (int i = 0; i < 8; i++)
            {
                Vector2 popVel = Main.rand.NextVector2Circular(2f, 1.5f) + new Vector2(0f, -1f);
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-25f, 25f), 6f),
                    DustID.Smoke,
                    popVel,
                    180,
                    new Color(40, 35, 30),
                    Main.rand.NextFloat(1.0f, 1.4f)
                );
                d.noGravity = true;
            }
        }
    }
}
