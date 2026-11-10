using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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

        private Color GetPrismaticColor()
        {
            float hue = (float)((Main.GlobalTimeWrappedHourly * 1.5f + Projectile.ai[0] + Projectile.identity * 0.15f) % 1f);
            if (hue < 0f) hue += 1f;
            return Main.hslToRgb(hue, 1f, 0.65f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color rainbowColor = GetPrismaticColor();
            Color glowColor = rainbowColor;
            glowColor.A = 0; // Additive self-illuminating glow

            // 1. Prismatic chromatic aura (4 soft radial passes)
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = (MathHelper.TwoPi / 4f * i).ToRotationVector2() * 2.5f;
                Main.EntitySpriteDraw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor * 0.55f,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 1.25f,
                    SpriteEffects.None
                );
            }

            // 2. Main crystal body tinted in brilliant prismatic rainbow color
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                glowColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None
            );

            // 3. Bright white specular highlight core
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                new Color(255, 255, 255, 0) * 0.8f,
                Projectile.rotation,
                origin,
                Projectile.scale * 0.6f,
                SpriteEffects.None
            );

            return false;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Color rainbowColor = GetPrismaticColor();
            Lighting.AddLight(Projectile.Center, rainbowColor.ToVector3() * 0.85f);

            // Luminous prismatic rainbow dust trail
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.RainbowRod,
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    0,
                    rainbowColor,
                    0.95f
                );
                d.noGravity = true;
            }

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

            // Full 360-degree rainbow spectrum burst upon impact
            for (int i = 0; i < 12; i++)
            {
                float burstHue = (float)i / 12f;
                Color burstColor = Main.hslToRgb(burstHue, 1f, 0.7f);
                Vector2 vel = (MathHelper.TwoPi / 12f * i).ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowRod, vel, 0, burstColor, 1.25f);
                d.noGravity = true;
            }
        }
    }
}
