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
            tag.SourceProtocolId = AugmentFamilyRegistry.MarksmanId;
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

            // Ambient napalm fire & plasma lighting
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.05f);

            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnPos = Projectile.position + new Vector2(Main.rand.NextFloat(Projectile.width), Main.rand.NextFloat(Projectile.height));
                Dust d = Dust.NewDustPerfect(
                    spawnPos,
                    DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-2.5f, -0.8f)),
                    100,
                    new Color(251, 146, 60),
                    Main.rand.NextFloat(1.1f, 1.6f)
                );
                d.noGravity = true;
            }

            if (Main.rand.NextBool(3))
            {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, -1f, 120, default, 0.8f);
                smoke.velocity.X *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire3, 180); // Hellfire for 3s
            target.AddBuff(BuffID.Slow, 120);    // 25% slow for 2s
        }
    }
}
