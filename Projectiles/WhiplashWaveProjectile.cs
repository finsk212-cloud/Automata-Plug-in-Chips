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
    public class WhiplashWaveProjectile : ModProjectile
    {
        private const int LifetimeTicks = 30;
        private const int FadeTicks = 8;
        private const int TrailCopies = 4;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.CanTriggerOnHitAugments = true;
            tag.OnHitEffectiveness = 0.5f;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;
            tag.SourceAugmentId = "whiplash_wave";

            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifetimeTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.3f, Pitch = 0.1f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, 0.55f, 0.38f, 0.1f);

            Vector2 perpendicular = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            for (int side = -1; side <= 1; side += 2)
            {
                if (!Main.rand.NextBool(2))
                    continue;

                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + perpendicular * (side * 20f),
                    Main.rand.NextBool(3) ? DustID.Smoke : DustID.GoldFlame,
                    -Projectile.velocity * 0.12f,
                    80,
                    default,
                    Main.rand.NextFloat(0.8f, 1.15f)
                );
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            for (int i = 0; i < 5; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    DustID.GoldFlame,
                    Main.rand.NextVector2Circular(3f, 3f),
                    60,
                    default,
                    Main.rand.NextFloat(0.9f, 1.3f)
                );
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    i % 3 == 0 ? DustID.Smoke : DustID.GoldFlame,
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    80,
                    default,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 direction = Projectile.rotation.ToRotationVector2();
            float fade = Math.Min(1f, Projectile.timeLeft / (float)FadeTicks);

            // Faint copies behind the crescent give it a motion trail.
            for (int i = TrailCopies - 1; i >= 0; i--)
            {
                Vector2 center = Projectile.Center - direction * (i * 10f);
                float strength = fade * (1f - i * 0.22f);
                float scale = 1f - i * 0.12f;
                DrawCrescent(pixel, center, 26f * scale, 7f * scale, new Color(255, 140, 35, 0) * (strength * 0.5f));
                DrawCrescent(pixel, center, 26f * scale, 3f * scale, new Color(255, 235, 170, 0) * strength);
            }

            return false;
        }

        // Draws a crescent opening backwards. The arc is thickest at the front and tapers to points.
        private void DrawCrescent(Texture2D pixel, Vector2 center, float radius, float thickness, Color color)
        {
            const int Segments = 12;
            const float HalfSweep = 1.0f;

            Vector2 direction = Projectile.rotation.ToRotationVector2();
            Vector2 arcCenter = center - direction * (radius * 0.55f);
            Vector2 previous = Vector2.Zero;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = MathHelper.Lerp(-HalfSweep, HalfSweep, i / (float)Segments);
                Vector2 point = arcCenter + (Projectile.rotation + angle).ToRotationVector2() * radius;

                if (i > 0)
                {
                    float midAngle = MathHelper.Lerp(-HalfSweep, HalfSweep, (i - 0.5f) / Segments);
                    float taper = 1f - 0.8f * Math.Abs(midAngle) / HalfSweep;
                    Vector2 delta = point - previous;

                    Main.EntitySpriteDraw(
                        pixel,
                        (previous + point) * 0.5f - Main.screenPosition,
                        new Rectangle(0, 0, 1, 1),
                        color,
                        delta.ToRotation(),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(delta.Length() + 1.5f, thickness * taper),
                        SpriteEffects.None
                    );
                }

                previous = point;
            }
        }
    }
}
