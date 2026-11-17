using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // The explosion itself. It lives a short moment, hits every enemy in its circle once for Projectile.damage,
    // and draws hexagon shock rings in the Pod's colours.
    public class GoingOutBlastProjectile : ModProjectile
    {
        public const float Radius = 420f; // about 26 tiles
        private const float BossHealthFraction = 0.01f;
        private const int LifeTicks = 26;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            AugmentProjectileTag tag = Projectile.GetGlobalProjectile<AugmentProjectileTag>();
            tag.IsAugmentProcDamage = true;
            tag.SourceAugmentId = "death_blast";
            tag.CanTriggerOnHitAugments = false;
            tag.PreventEchoChamberCopy = true;
            tag.PreventRicochetEngineCopy = true;

            Projectile.width = (int)(Radius * 2f);
            Projectile.height = (int)(Radius * 2f);
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTicks;
            // Longer than the blast lives, so each enemy is hit exactly once.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = LifeTicks + 10;
        }

        private int Elapsed => LifeTicks - Projectile.timeLeft;
        private bool burstPlayed;

        // A circle, not the square the projectile is: hit anything whose box touches the circle.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 c = Projectile.Center;
            float nearestX = MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right);
            float nearestY = MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom);
            return Vector2.DistanceSquared(c, new Vector2(nearestX, nearestY)) <= Radius * Radius;
        }

        // Bosses also take 1% of their max health, added on top and after defense so it always lands.
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (target.boss || NPCID.Sets.ShouldBeCountedAsBoss[target.type])
                modifiers.FinalDamage.Flat += (int)(target.lifeMax * BossHealthFraction);
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 1.4f * (1f - Elapsed / (float)LifeTicks), 0.6f, 0.2f);

            if (burstPlayed || Main.dedServ)
                return;

            burstPlayed = true;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

            // A burst of data-sparks and embers, plus a ring of dust out at the edge of the blast.
            for (int i = 0; i < 70; i++)
            {
                int type = Main.rand.NextBool() ? DustID.SilverCoin : DustID.Torch;
                Dust d = Dust.NewDustPerfect(Projectile.Center, type, Main.rand.NextVector2Circular(16f, 16f), 100, default, Main.rand.NextFloat(1.1f, 2f));
                d.noGravity = true;
            }
            for (int i = 0; i < 48; i++)
            {
                Vector2 dir = (MathHelper.TwoPi * i / 48f).ToRotationVector2();
                Dust d = Dust.NewDustPerfect(Projectile.Center + dir * Radius * 0.9f, DustID.Torch, dir * 3f, 100, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float t = Elapsed / (float)LifeTicks;
            float eased = 1f - (1f - t) * (1f - t);
            float fade = 1f - t;

            // Three hexagon rings rolling outward: beige, amber, red.
            DangerCloseVisuals.Hex(Projectile.Center, Radius * eased, t * 0.8f, 6f, DangerCloseVisuals.Beige * fade);
            DangerCloseVisuals.Hex(Projectile.Center, Radius * eased * 0.82f, -t * 0.8f, 4f, DangerCloseVisuals.Amber * (0.9f * fade));
            DangerCloseVisuals.Hex(Projectile.Center, Radius * eased * 0.64f, t * 1.2f, 3f, DangerCloseVisuals.Red * (0.9f * fade));

            // A starburst flash at the middle for the first few ticks.
            if (Elapsed < 9)
            {
                float flash = 1f - Elapsed / 9f;
                for (int i = 0; i < 12; i++)
                {
                    Vector2 dir = (MathHelper.TwoPi * i / 12f).ToRotationVector2();
                    DangerCloseVisuals.Line(Projectile.Center, Projectile.Center + dir * (90f + 160f * eased), 16f * flash, DangerCloseVisuals.Beige * flash);
                }
            }

            return false;
        }
    }
}
