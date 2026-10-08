using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    public class EldritchLaserProjectile : ModProjectile
    {
        private const float BeamWidth = 9f;
        private const float BeamLength = 450f;
        private const float RotationSpeed = 0.012f;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "eldritch_covenant";
            tag.CanTriggerOnHitAugments = false;

            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 32;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Projectile eye = FindParentEye();
            if (eye == null)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = eye.Center;
            if (Projectile.localAI[1] == 0f)
            {
                Projectile.rotation = Projectile.ai[1];
                Projectile.localAI[1] = 1f;
            }
            else
            {
                Projectile.rotation += RotationSpeed * Projectile.ai[2];
            }

            Projectile.localAI[0] = BeamLength;
            Vector2 beamEnd = Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength;

            for (int i = 0; i <= 4; i++)
            {
                Vector2 lightPosition = Vector2.Lerp(Projectile.Center, beamEnd, i / 4f);
                Lighting.AddLight(lightPosition, 0.28f, 0.06f, 0.38f);
            }

            if (Projectile.timeLeft % 3 == 0)
            {
                Dust endpoint = Dust.NewDustPerfect(
                    beamEnd + Main.rand.NextVector2Circular(5f, 5f),
                    Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.GemAmethyst,
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    120,
                    default,
                    0.8f
                );
                endpoint.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 lineStart = Projectile.Center;
            Vector2 lineEnd = lineStart + Projectile.rotation.ToRotationVector2() * Projectile.localAI[0];
            Vector2 boxPosition = targetHitbox.TopLeft();
            Vector2 boxSize = targetHitbox.Size();
            float collisionPoint = 0f;

            return Collision.CheckAABBvLineCollision(
                boxPosition,
                boxSize,
                lineStart,
                lineEnd,
                BeamWidth,
                ref collisionPoint
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float length = Projectile.localAI[0];
            if (length <= 0f)
                return false;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, 0.5f);
            Rectangle pixelSource = new Rectangle(0, 0, 1, 1);
            float pulse = 0.5f + 0.5f * System.MathF.Sin(Main.GlobalTimeWrappedHourly * 12f);

            Main.EntitySpriteDraw(
                pixel,
                drawPosition,
                pixelSource,
                new Color(95, 10, 145, (byte)(45 + 30 * pulse)),
                Projectile.rotation,
                origin,
                new Vector2(length, 10f + pulse * 2f),
                SpriteEffects.None
            );

            Main.EntitySpriteDraw(
                pixel,
                drawPosition,
                pixelSource,
                new Color(195, 45, 255, 225),
                Projectile.rotation,
                origin,
                new Vector2(length, 5f + pulse),
                SpriteEffects.None
            );

            Main.EntitySpriteDraw(
                pixel,
                drawPosition,
                pixelSource,
                new Color(245, 185, 255, 215),
                Projectile.rotation,
                origin,
                new Vector2(length, 1.5f),
                SpriteEffects.None
            );

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    i % 2 == 0 ? DustID.GemAmethyst : DustID.Shadowflame,
                    angle.ToRotationVector2() * 2.5f,
                    100,
                    default,
                    0.85f
                );
                dust.noGravity = true;
            }
        }

        private Projectile FindParentEye()
        {
            int parentIdentity = (int)Projectile.ai[0];
            foreach (Projectile projectile in Main.projectile)
            {
                if (projectile.active && projectile.owner == Projectile.owner &&
                    projectile.identity == parentIdentity &&
                    projectile.type == ModContent.ProjectileType<EldritchEyeProjectile>())
                {
                    return projectile;
                }
            }

            return null;
        }

    }
}
