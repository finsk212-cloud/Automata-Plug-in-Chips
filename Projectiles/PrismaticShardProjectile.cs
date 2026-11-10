using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class PrismaticShardProjectile : ModProjectile
    {
        private const float HomingRange = 450f;
        private const float HomingSpeed = 14f;
        private const float HomingSteer = 0.12f;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "prismatic_fracture";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Subtle prismatic rainbow dust trail
            if (Main.rand.NextBool(2))
            {
                int[] dustTypes = { DustID.Enchanted_Pink, DustID.Electric, DustID.YellowTorch, DustID.RainbowMk2 };
                int dType = dustTypes[Main.rand.Next(dustTypes.Length)];
                Dust d = Dust.NewDustPerfect(Projectile.Center, dType, Projectile.velocity * 0.15f, 100, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.5f, 0.7f);

            // Brief outward scatter burst (6 ticks) before homing locks on
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] < 6f)
                return;

            // Find nearest valid hostile target
            NPC closest = null;
            float closestDistSq = HomingRange * HomingRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = npc;
                }
            }

            if (closest != null)
            {
                Vector2 desiredVel = Vector2.Normalize(closest.Center - Projectile.Center) * HomingSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, HomingSteer);
            }
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = Main.rand.NextFloat(0.1f, 0.4f) }, Projectile.Center);

            int[] dustTypes = { DustID.Enchanted_Pink, DustID.Electric, DustID.YellowTorch, DustID.RainbowMk2 };
            for (int i = 0; i < 8; i++)
            {
                int dType = dustTypes[Main.rand.Next(dustTypes.Length)];
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, dType, vel, 80, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
