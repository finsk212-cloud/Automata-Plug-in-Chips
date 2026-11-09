using System;
using Microsoft.Xna.Framework;
using Terraria;
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

            Projectile.width = 64;
            Projectile.height = 24;
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
            // Rest on solid ground
            Projectile.velocity = Vector2.Zero;

            // Distinct warm orange plasma glow
            Lighting.AddLight(Projectile.Center, 0.7f, 0.32f, 0.05f);

            // Initial splash burst on first frame
            if (Projectile.timeLeft == 180)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector2 burstVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1.5f, -0.2f));
                    Dust initDust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 4f),
                        DustID.OrangeTorch,
                        burstVel,
                        80,
                        default,
                        Main.rand.NextFloat(1.3f, 1.7f)
                    );
                    initDust.noGravity = true;
                }
            }

            // Steady, low-overhead orange napalm flame (single particle per tick keeps frame rates silky smooth)
            Vector2 spawnPos = Projectile.position + new Vector2(Main.rand.NextFloat(Projectile.width), Main.rand.NextFloat(Projectile.height));
            Dust d = Dust.NewDustPerfect(
                spawnPos,
                DustID.OrangeTorch,
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-2.0f, -0.8f)),
                80,
                default,
                Main.rand.NextFloat(1.2f, 1.5f)
            );
            d.noGravity = true;

            // Distinctive solar plasma spark (1 every 3 ticks) for unmistakable plugin identity
            if (Main.rand.NextBool(3))
            {
                Dust spark = Dust.NewDustPerfect(
                    spawnPos,
                    DustID.SolarFlare,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    0,
                    default,
                    Main.rand.NextFloat(0.7f, 1.0f)
                );
                spark.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire3, 180); // Hellfire for 3s
            target.AddBuff(BuffID.Slow, 120);    // 25% slow for 2s
        }
    }
}
