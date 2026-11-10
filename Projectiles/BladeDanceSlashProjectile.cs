using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class BladeDanceSlashProjectile : ModProjectile
    {
        private const int LifetimeTicks = 14;
        private const int FadeTicks = 6;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "blade_dance";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifetimeTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Emit radiant crimson & silver slice sparks along the blade path
            if (Main.rand.NextBool(2))
            {
                int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(24f, 24f);
                Dust d = Dust.NewDustPerfect(dustPos, dType, Projectile.velocity * 0.2f, 100, default, 1.2f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.25f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float progress = 1f - (Projectile.timeLeft / (float)LifetimeTicks);
            float fade = Math.Min(1f, Projectile.timeLeft / (float)FadeTicks);

            // Draw glowing phantom crescent slices
            DrawCrescent(pixel, Projectile.Center, 42f, 8f, new Color(230, 30, 60, 0) * (fade * 0.7f));
            DrawCrescent(pixel, Projectile.Center, 40f, 4f, new Color(255, 240, 245, 0) * (fade * 0.95f));

            return false;
        }

        private void DrawCrescent(Texture2D pixel, Vector2 center, float radius, float thickness, Color color)
        {
            const int Segments = 14;
            const float HalfSweep = 1.15f;

            Vector2 direction = Projectile.rotation.ToRotationVector2();
            float baseAngle = Projectile.rotation;
            Rectangle source = new Rectangle(0, 0, 1, 1);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            float segmentLength = (radius * HalfSweep * 2f / Segments) + 2f;

            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1);
                float angleOffset = MathHelper.Lerp(-HalfSweep, HalfSweep, t);
                float angle = baseAngle + angleOffset;

                // Center is thickest, tips taper cleanly
                float taper = (float)Math.Sin(t * Math.PI);
                float currentThickness = Math.Max(1.5f, thickness * taper);

                Vector2 segCenter = center + angle.ToRotationVector2() * radius;
                Vector2 drawPos = segCenter - Main.screenPosition;

                Main.EntitySpriteDraw(
                    pixel,
                    drawPos,
                    source,
                    color,
                    angle + MathHelper.PiOver2,
                    origin,
                    new Vector2(segmentLength, currentThickness),
                    SpriteEffects.None
                );
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                int dType = Main.rand.NextBool() ? DustID.CrimsonTorch : DustID.SilverCoin;
                Dust d = Dust.NewDustPerfect(Projectile.Center, dType, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
