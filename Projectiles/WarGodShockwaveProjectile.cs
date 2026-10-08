using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class WarGodShockwaveProjectile : ModProjectile
    {
        private const float ShockwaveRadius = 220f;
        private const int LifetimeTicks = 20;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "war_gods_tempo";
            tag.CanTriggerOnHitAugments = false;

            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifetimeTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float progress = MathHelper.Clamp(Projectile.localAI[0] / LifetimeTicks, 0f, 1f);
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            float radius = ShockwaveRadius * easedProgress;
            float fade = 1f - progress;
            DrawEnergyRing(radius, 9f, new Color(210, 55, 10) * (fade * 0.55f));
            DrawEnergyRing(radius, 4f, new Color(255, 205, 70) * (fade * 0.95f));
            return false;
        }

        public override bool? CanDamage() => Projectile.localAI[1] == 1f;

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0]++;

            if (Projectile.localAI[0] == 1f)
            {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.45f }, Projectile.Center);
                SpawnImpactBurst();
                DamageInRadius();
            }

            SpawnExpandingRing();
        }

        private void DamageInRadius()
        {
            Vector2 center = Projectile.Center;
            Projectile.width = (int)(ShockwaveRadius * 2f);
            Projectile.height = (int)(ShockwaveRadius * 2f);
            Projectile.Center = center;
            Projectile.localAI[1] = 1f;
            Projectile.Damage();
            Projectile.localAI[1] = 0f;
        }

        private void SpawnExpandingRing()
        {
            float progress = Projectile.localAI[0] / LifetimeTicks;
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            float radius = ShockwaveRadius * easedProgress;

            Lighting.AddLight(Projectile.Center, 0.75f * (1f - progress), 0.32f * (1f - progress), 0.08f);

            if ((int)Projectile.localAI[0] % 2 != 0)
                return;

            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 direction = angle.ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + direction * radius,
                    i % 4 == 0 ? DustID.Smoke : DustID.GoldFlame,
                    direction * Main.rand.NextFloat(1.2f, 2.8f),
                    i % 4 == 0 ? 130 : 70,
                    default,
                    Main.rand.NextFloat(0.8f, 1.25f)
                );
                dust.noGravity = true;
            }
        }

        private void SpawnImpactBurst()
        {
            for (int i = 0; i < 36; i++)
            {
                float angle = MathHelper.TwoPi * i / 36f + Main.rand.NextFloat(-0.05f, 0.05f);
                Vector2 direction = angle.ToRotationVector2();
                Dust spark = Dust.NewDustPerfect(
                    Projectile.Center + direction * Main.rand.NextFloat(4f, 18f),
                    i % 5 == 0 ? DustID.Smoke : DustID.GoldFlame,
                    direction * Main.rand.NextFloat(4f, 10f),
                    i % 5 == 0 ? 120 : 35,
                    default,
                    Main.rand.NextFloat(1f, 1.65f)
                );
                spark.noGravity = true;
            }

            for (int i = 0; i < 10; i++)
            {
                Dust flash = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f),
                    40,
                    Color.White,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                flash.noGravity = true;
            }
        }

        private void DrawEnergyRing(float radius, float thickness, Color color)
        {
            const int Segments = 48;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);
            float segmentLength = MathHelper.TwoPi * radius / Segments + 2f;

            for (int i = 0; i < Segments; i++)
            {
                float angle = MathHelper.TwoPi * i / Segments;
                Vector2 drawPosition = Projectile.Center + angle.ToRotationVector2() * radius - Main.screenPosition;
                Main.EntitySpriteDraw(
                    pixel,
                    drawPosition,
                    source,
                    color,
                    angle + MathHelper.PiOver2,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(segmentLength, thickness),
                    SpriteEffects.None
                );
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float nearestX = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float nearestY = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            Vector2 nearestPoint = new Vector2(nearestX, nearestY);
            return Vector2.DistanceSquared(Projectile.Center, nearestPoint) <= ShockwaveRadius * ShockwaveRadius;
        }
    }
}
